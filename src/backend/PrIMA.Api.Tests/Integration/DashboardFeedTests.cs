using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Issues.GetDashboardFeed;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DashboardFeedTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DashboardFeedTests(PrIMAApiFactory factory)
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

    private async Task SeedIssueAsync(
        Guid id,
        Guid detectorId,
        string detectorName,
        string title,
        string targetEntity,
        string scope = "DataIntegrity",
        string baseSeverity = "High",
        string currentSeverity = "High",
        byte severityRank = 3,
        string status = "Open",
        int occurrenceCount = 1,
        DateTime? lastOccurrenceAtUtc = null,
        DateTime? resolvedAtUtc = null,
        string? currentAssignee = null)
    {
        var lastOcc = lastOccurrenceAtUtc ?? DateTime.UtcNow;
        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope, BaseSeverity,
                CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc, ResolvedAtUtc
            ) VALUES (
                @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope, @BaseSeverity,
                @CurrentSeverity, @SeverityRank, @Status, 0, @OccurrenceCount,
                @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc, @ResolvedAtUtc
            );
            """;

        await _factory.ExecuteSqlAsync(sql, new
        {
            Id = id,
            CorrelationKey = $"{detectorId:D}:{targetEntity}",
            Title = title,
            DetectorId = detectorId,
            DetectorName = detectorName,
            Scope = scope,
            BaseSeverity = baseSeverity,
            CurrentSeverity = currentSeverity,
            SeverityRank = severityRank,
            Status = status,
            OccurrenceCount = occurrenceCount,
            CurrentAssignee = currentAssignee,
            CreatedAtUtc = lastOcc,
            LastOccurrenceAtUtc = lastOcc,
            ResolvedAtUtc = resolvedAtUtc
        });
    }

    [Fact]
    public async Task GetDashboardFeed_UnauthenticatedRequest_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/issues/dashboard-feed");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Resolved")]
    [InlineData("resolved")]
    [InlineData("Dismissed")]
    [InlineData("dismissed")]
    public async Task GetDashboardFeed_QueryingClosedStatus_Returns422ProblemDetails(string closedStatus)
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/api/issues/dashboard-feed?status={closedStatus}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Dashboard feed displays active issues only");
    }

    [Fact]
    public async Task GetDashboardFeed_SummaryCounters_CalculateCorrectlyAcrossWibMidnightBoundary()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync();

        // Determine current WIB day boundaries
        TimeZoneInfo wibZone;
        try
        {
            wibZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
        }
        catch (TimeZoneNotFoundException)
        {
            wibZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        var utcNow = DateTime.UtcNow;
        var wibNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, wibZone);
        var startOfTodayWibUtc = TimeZoneInfo.ConvertTimeToUtc(wibNow.Date, wibZone);

        // 1. Active Open Critical issue (Rank 4) -> CriticalCount++, OpenCount++
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "Critical Issue", "ENT-CRIT",
            baseSeverity: "Critical", currentSeverity: "Critical", severityRank: 4, status: "Open");

        // 2. Active Reopened High issue (Rank 3) -> HighCount++, ReopenedCount++
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "High Issue", "ENT-HIGH",
            baseSeverity: "High", currentSeverity: "High", severityRank: 3, status: "Reopened");

        // 3. Active Investigating Medium issue (Rank 2) -> does not touch critical/high/open/reopened counters
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "Medium Issue", "ENT-MED",
            baseSeverity: "Medium", currentSeverity: "Medium", severityRank: 2, status: "Investigating");

        // 4. Closed Resolved issue resolved TODAY in WIB (+1 hour after WIB midnight) -> ResolvedTodayCount++
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "Resolved Today Issue", "ENT-RES-TODAY",
            status: "Resolved", resolvedAtUtc: startOfTodayWibUtc.AddHours(1));

        // 5. Closed Resolved issue resolved YESTERDAY in WIB (-1 hour before WIB midnight) -> NOT in ResolvedTodayCount
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "Resolved Yesterday Issue", "ENT-RES-YEST",
            status: "Resolved", resolvedAtUtc: startOfTodayWibUtc.AddHours(-1));

        // 6. Dismissed issue -> does not touch counters
        await SeedIssueAsync(
            Guid.NewGuid(), detectorId, "Test Detector", "Dismissed Issue", "ENT-DISM",
            status: "Dismissed");

        // Act
        var response = await client.GetAsync("/api/issues/dashboard-feed");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Summary.CriticalCount.Should().Be(1);
        body.Summary.HighCount.Should().Be(1);
        body.Summary.OpenCount.Should().Be(1);
        body.Summary.ReopenedCount.Should().Be(1);
        body.Summary.ResolvedTodayCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardFeed_ActiveFeedItems_StrictlyOrderedBySeverityRankDescThenLastOccurrenceDesc()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync();
        var baseTime = DateTime.UtcNow;

        var idMediumRecent = Guid.NewGuid(); // Rank 2, 5 min ago
        var idCritOlder = Guid.NewGuid();    // Rank 4, 20 min ago
        var idCritRecent = Guid.NewGuid();   // Rank 4, 5 min ago
        var idHigh = Guid.NewGuid();         // Rank 3, 1 min ago

        await SeedIssueAsync(
            idMediumRecent, detectorId, "Test Detector", "Medium Recent", "MED-REC",
            currentSeverity: "Medium", severityRank: SeverityRank.Medium,
            lastOccurrenceAtUtc: baseTime.AddMinutes(-5));

        await SeedIssueAsync(
            idCritOlder, detectorId, "Test Detector", "Critical Older", "CRIT-OLD",
            currentSeverity: "Critical", severityRank: SeverityRank.Critical,
            lastOccurrenceAtUtc: baseTime.AddMinutes(-20));

        await SeedIssueAsync(
            idCritRecent, detectorId, "Test Detector", "Critical Recent", "CRIT-REC",
            currentSeverity: "Critical", severityRank: SeverityRank.Critical,
            lastOccurrenceAtUtc: baseTime.AddMinutes(-5));

        await SeedIssueAsync(
            idHigh, detectorId, "Test Detector", "High Issue", "HIGH-REC",
            currentSeverity: "High", severityRank: SeverityRank.High,
            lastOccurrenceAtUtc: baseTime.AddMinutes(-1));

        // Act
        var response = await client.GetAsync("/api/issues/dashboard-feed");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        body.Should().NotBeNull();

        var itemIds = body!.Items.Select(x => x.Id).ToList();
        // Strict ordering:
        // 1. Critical Recent (Rank 4, -5m)
        // 2. Critical Older (Rank 4, -20m)
        // 3. High (Rank 3, -1m)
        // 4. Medium Recent (Rank 2, -5m)
        itemIds.Should().ContainInOrder(idCritRecent, idCritOlder, idHigh, idMediumRecent);
    }

    [Fact]
    public async Task GetDashboardFeed_Filters_ReturnAccurateMatchingItems()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync(name: "Hospital Ledger Auditing");

        var issueA = Guid.NewGuid();
        var issueB = Guid.NewGuid();
        var issueC = Guid.NewGuid();

        await SeedIssueAsync(
            issueA, detectorId, "Hospital Ledger Auditing", "Billing Ledger Out of Balance", "BILL-001",
            scope: "DataIntegrity", currentSeverity: "Critical", severityRank: 4, status: "Open");

        await SeedIssueAsync(
            issueB, detectorId, "Patient ADT Prober", "Admission Discharge Sync Error", "ADT-002",
            scope: "ProcessIntegrity", currentSeverity: "High", severityRank: 3, status: "Investigating");

        await SeedIssueAsync(
            issueC, detectorId, "Lab Order Bridge", "Lab Order Message Drop", "LAB-003",
            scope: "DataSync", currentSeverity: "Critical", severityRank: 4, status: "Reopened");

        // Act & Assert 1: Filter by Status = Open
        var openResp = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?status=Open", JsonOptions);
        openResp!.Items.Should().ContainSingle(x => x.Id == issueA);

        // Act & Assert 2: Filter by Severity = Critical
        var critResp = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?severity=Critical", JsonOptions);
        critResp!.Items.Select(x => x.Id).Should().BeEquivalentTo(new[] { issueA, issueC });

        // Act & Assert 3: Filter by Scope = ProcessIntegrity
        var scopeResp = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?scope=ProcessIntegrity", JsonOptions);
        scopeResp!.Items.Should().ContainSingle(x => x.Id == issueB);

        // Act & Assert 4: Search filter by title substring "Discharge"
        var searchResp1 = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?search=Discharge", JsonOptions);
        searchResp1!.Items.Should().ContainSingle(x => x.Id == issueB);

        // Act & Assert 5: Search filter by detector name substring "Ledger"
        var searchResp2 = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?search=Ledger", JsonOptions);
        searchResp2!.Items.Should().ContainSingle(x => x.Id == issueA);

        // Act & Assert 6: Search filter by correlation key substring "LAB-003"
        var searchResp3 = await client.GetFromJsonAsync<GetDashboardFeedResponse>("/api/issues/dashboard-feed?search=LAB-003", JsonOptions);
        searchResp3!.Items.Should().ContainSingle(x => x.Id == issueC);
    }
}
