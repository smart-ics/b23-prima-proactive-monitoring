using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Detectors.GetDetectorList;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorPortfolioEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DetectorPortfolioEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private async Task InsertDetectorAsync(
        Guid id,
        string name,
        string description = "Test description",
        string scope = "DataIntegrity",
        string targetModule = "Billing",
        int expectedCadenceSeconds = 300,
        string? webhookUrl = null,
        string adminStatus = "Active",
        string healthStatus = "Healthy",
        DateTime? lastRunAtUtc = null,
        DateTime? lastHealthyHeartbeatUtc = null)
    {
        using var connection = _factory.CreateDbConnection();
        const string sql = """
            INSERT INTO dbo.Detectors (
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
            )
            VALUES (
                @Id, @Name, @Description, @Scope, @TargetModule, @ExpectedCadenceSeconds,
                @WebhookUrl, @AdminStatus, @HealthStatus, @LastRunAtUtc, @LastHealthyHeartbeatUtc, SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            Name = name,
            Description = description,
            Scope = scope,
            TargetModule = targetModule,
            ExpectedCadenceSeconds = expectedCadenceSeconds,
            WebhookUrl = webhookUrl,
            AdminStatus = adminStatus,
            HealthStatus = healthStatus,
            LastRunAtUtc = lastRunAtUtc,
            LastHealthyHeartbeatUtc = lastHealthyHeartbeatUtc
        });
    }

    private async Task InsertInspectionRunAsync(
        Guid id,
        Guid detectorId,
        DateTime executionTimestampUtc,
        string outcome = "Passed",
        int durationMs = 150)
    {
        using var connection = _factory.CreateDbConnection();
        const string sql = """
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc
            )
            VALUES (
                @Id, @DetectorId, @ExecutionTimestampUtc, 'Scheduled', @Outcome, @DurationMs, SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTimestampUtc,
            Outcome = outcome,
            DurationMs = durationMs
        });
    }

    private async Task InsertIssueAsync(
        Guid id,
        Guid detectorId,
        string detectorName,
        string status,
        string correlationKey)
    {
        using var connection = _factory.CreateDbConnection();
        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                CreatedAtUtc, LastOccurrenceAtUtc
            )
            VALUES (
                @Id, @CorrelationKey, 'Test Issue', @DetectorId, @DetectorName, 'DataIntegrity',
                'High', 'High', 3, @Status, 0, 1,
                SYSUTCDATETIME(), SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            CorrelationKey = correlationKey,
            DetectorId = detectorId,
            DetectorName = detectorName,
            Status = status
        });
    }

    [Fact]
    public async Task GetDetectors_WithoutAuth_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/detectors");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDetectorSummary_WithoutAuth_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/detectors/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDetectors_ReturnsPortfolioOrderedByHealthPriority_ThenNameAscending()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        var d3 = Guid.NewGuid();
        var d4 = Guid.NewGuid();
        var d5 = Guid.NewGuid();

        await InsertDetectorAsync(d1, "Zulu Detector", healthStatus: "Healthy");
        await InsertDetectorAsync(d2, "Beta Detector", healthStatus: "Silent");
        await InsertDetectorAsync(d3, "Charlie Detector", healthStatus: "Failing");
        await InsertDetectorAsync(d4, "Alpha Detector", healthStatus: "Healthy");
        await InsertDetectorAsync(d5, "Echo Detector", adminStatus: "Paused", healthStatus: "Healthy");

        var response = await authClient.GetAsync("/api/detectors");
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: content);

        var detectors = await response.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        detectors.Should().NotBeNull();
        detectors!.Count.Should().Be(5);

        // Priority order: Failing -> Silent -> Healthy -> Others, then Name ASC
        detectors[0].Name.Should().Be("Charlie Detector");
        detectors[0].HealthStatus.Should().Be("Failing");

        detectors[1].Name.Should().Be("Beta Detector");
        detectors[1].HealthStatus.Should().Be("Silent");

        detectors[2].Name.Should().Be("Alpha Detector");
        detectors[2].HealthStatus.Should().Be("Healthy");

        detectors[3].Name.Should().Be("Echo Detector");
        detectors[3].HealthStatus.Should().Be("Healthy");

        detectors[4].Name.Should().Be("Zulu Detector");
        detectors[4].HealthStatus.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetDetectors_Calculates30DayPassRate_And_ActiveIssuesCountCorrectly()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var detectorId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;
        await InsertDetectorAsync(detectorId, "Calculation Detector", healthStatus: "Healthy", lastRunAtUtc: nowUtc);

        // 3 runs in last 30 days: 2 Passed, 1 FindingDiscovered -> 2 * 100.0 / 3 = 66.7%
        await InsertInspectionRunAsync(Guid.NewGuid(), detectorId, nowUtc.AddDays(-2), "Passed");
        await InsertInspectionRunAsync(Guid.NewGuid(), detectorId, nowUtc.AddDays(-5), "Passed");
        await InsertInspectionRunAsync(Guid.NewGuid(), detectorId, nowUtc.AddDays(-10), "FindingDiscovered");

        // 1 run older than 30 days -> should be ignored
        await InsertInspectionRunAsync(Guid.NewGuid(), detectorId, nowUtc.AddDays(-35), "Passed");

        // Issues: 2 active (Open, Investigating), 1 resolved (Resolved), 1 dismissed (Dismissed)
        await InsertIssueAsync(Guid.NewGuid(), detectorId, "Calculation Detector", "Open", "KEY:1");
        await InsertIssueAsync(Guid.NewGuid(), detectorId, "Calculation Detector", "Investigating", "KEY:2");
        await InsertIssueAsync(Guid.NewGuid(), detectorId, "Calculation Detector", "Resolved", "KEY:3");
        await InsertIssueAsync(Guid.NewGuid(), detectorId, "Calculation Detector", "Dismissed", "KEY:4");

        // Detector without runs
        var zeroRunsDetectorId = Guid.NewGuid();
        await InsertDetectorAsync(zeroRunsDetectorId, "Zero Runs Detector");

        var response = await authClient.GetAsync("/api/detectors");
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: content);

        var detectors = await response.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        detectors.Should().NotBeNull();

        var target = detectors!.Single(d => d.Id == detectorId);
        target.TotalRuns30d.Should().Be(3);
        target.PassRate30d.Should().Be(66.7m);
        target.ActiveIssuesCount.Should().Be(2);

        var zeroRuns = detectors!.Single(d => d.Id == zeroRunsDetectorId);
        zeroRuns.TotalRuns30d.Should().Be(0);
        zeroRuns.PassRate30d.Should().Be(100.0m);
        zeroRuns.ActiveIssuesCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDetectors_SupportsFilters_Scope_AdminStatus_HealthStatus_Search()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        var d3 = Guid.NewGuid();

        await InsertDetectorAsync(d1, "Accounting Ledger Check", scope: "DataIntegrity", adminStatus: "Active", healthStatus: "Healthy", targetModule: "Accounting");
        await InsertDetectorAsync(d2, "Inpatient Bed Check", scope: "ProcessIntegrity", adminStatus: "Paused", healthStatus: "Silent", targetModule: "Inpatient");
        await InsertDetectorAsync(d3, "Database Replication Heartbeat", scope: "Infrastructure", adminStatus: "Active", healthStatus: "Failing", targetModule: "Infrastructure");

        // 1. Filter by Scope
        var scopeResp = await authClient.GetAsync("/api/detectors?scope=ProcessIntegrity");
        var scopeItems = await scopeResp.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        scopeItems.Should().ContainSingle(d => d.Id == d2);

        // 2. Filter by AdminStatus
        var adminResp = await authClient.GetAsync("/api/detectors?adminStatus=Paused");
        var adminItems = await adminResp.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        adminItems.Should().ContainSingle(d => d.Id == d2);

        // 3. Filter by HealthStatus
        var healthResp = await authClient.GetAsync("/api/detectors?healthStatus=Failing");
        var healthItems = await healthResp.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        healthItems.Should().ContainSingle(d => d.Id == d3);

        // 4. Filter by Search (Name substring)
        var searchResp = await authClient.GetAsync("/api/detectors?search=Accounting");
        var searchItems = await searchResp.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        searchItems.Should().ContainSingle(d => d.Id == d1);

        // 5. Filter with "All" ignores filter
        var allResp = await authClient.GetAsync("/api/detectors?adminStatus=All&healthStatus=All&scope=All");
        var allItems = await allResp.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        allItems!.Where(d => d.Id == d1 || d.Id == d2 || d.Id == d3).Count().Should().Be(3);
    }

    [Fact]
    public async Task GetDetectorSummary_ReturnsAccurateGlobalHealthCounts()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var initialResp = await authClient.GetAsync("/api/detectors/summary");
        initialResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var initial = await initialResp.Content.ReadFromJsonAsync<DetectorSummaryDto>(JsonOptions);
        initial.Should().NotBeNull();

        // 2 Active Healthy
        await InsertDetectorAsync(Guid.NewGuid(), $"Healthy 1 {Guid.NewGuid():N}", adminStatus: "Active", healthStatus: "Healthy");
        await InsertDetectorAsync(Guid.NewGuid(), $"Healthy 2 {Guid.NewGuid():N}", adminStatus: "Active", healthStatus: "Healthy");

        // 1 Active Failing
        await InsertDetectorAsync(Guid.NewGuid(), $"Failing 1 {Guid.NewGuid():N}", adminStatus: "Active", healthStatus: "Failing");

        // 1 Active Silent
        await InsertDetectorAsync(Guid.NewGuid(), $"Silent 1 {Guid.NewGuid():N}", adminStatus: "Active", healthStatus: "Silent");

        // 2 Paused
        await InsertDetectorAsync(Guid.NewGuid(), $"Paused 1 {Guid.NewGuid():N}", adminStatus: "Paused", healthStatus: "Healthy");
        await InsertDetectorAsync(Guid.NewGuid(), $"Paused 2 {Guid.NewGuid():N}", adminStatus: "Paused", healthStatus: "Silent");

        // 1 Retired
        await InsertDetectorAsync(Guid.NewGuid(), $"Retired 1 {Guid.NewGuid():N}", adminStatus: "Retired", healthStatus: "Healthy");

        var response = await authClient.GetAsync("/api/detectors/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<DetectorSummaryDto>(JsonOptions);
        summary.Should().NotBeNull();

        summary!.TotalCount.Should().Be(initial!.TotalCount + 7);
        summary.HealthyCount.Should().Be(initial.HealthyCount + 2);
        summary.FailingCount.Should().Be(initial.FailingCount + 1);
        summary.SilentCount.Should().Be(initial.SilentCount + 1);
        summary.PausedCount.Should().Be(initial.PausedCount + 2);
    }
}
