using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.History.GetReliabilityKpi;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class HistoryKpiEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HistoryKpiEndpointTests(PrIMAApiFactory factory)
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

    private static string BuildKpiUrl(DateTime fromUtc, DateTime toUtc)
    {
        var fromIso = Uri.EscapeDataString(fromUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        var toIso = Uri.EscapeDataString(toUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        return $"/api/history/reliability-kpi?fromUtc={fromIso}&toUtc={toIso}";
    }

    private async Task SeedIssueAsync(
        Guid detectorId,
        string detectorName,
        string title,
        string correlationKey,
        string scope = "DataIntegrity",
        string baseSeverity = "High",
        string currentSeverity = "High",
        byte severityRank = 3,
        string status = "Open",
        DateTime? createdAtUtc = null,
        DateTime? acknowledgedAtUtc = null,
        DateTime? resolvedAtUtc = null,
        DateTime? dismissedAtUtc = null,
        DateTime? reopenedAtUtc = null)
    {
        var created = createdAtUtc ?? DateTime.UtcNow;
        var lastOccurrence = reopenedAtUtc ?? created;

        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope, BaseSeverity,
                CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc, AcknowledgedAtUtc,
                ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope, @BaseSeverity,
                @CurrentSeverity, @SeverityRank, @Status, 0, 1,
                NULL, @CreatedAtUtc, @LastOccurrenceAtUtc, @AcknowledgedAtUtc,
                @ResolvedAtUtc, @DismissedAtUtc, @ReopenedAtUtc
            );
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Id", Guid.NewGuid());
        parameters.Add("CorrelationKey", correlationKey);
        parameters.Add("Title", title);
        parameters.Add("DetectorId", detectorId);
        parameters.Add("DetectorName", detectorName);
        parameters.Add("Scope", scope);
        parameters.Add("BaseSeverity", baseSeverity);
        parameters.Add("CurrentSeverity", currentSeverity);
        parameters.Add("SeverityRank", severityRank);
        parameters.Add("Status", status);
        parameters.Add("CreatedAtUtc", created, DbType.DateTime2);
        parameters.Add("LastOccurrenceAtUtc", lastOccurrence, DbType.DateTime2);
        parameters.Add("AcknowledgedAtUtc", acknowledgedAtUtc, DbType.DateTime2);
        parameters.Add("ResolvedAtUtc", resolvedAtUtc, DbType.DateTime2);
        parameters.Add("DismissedAtUtc", dismissedAtUtc, DbType.DateTime2);
        parameters.Add("ReopenedAtUtc", reopenedAtUtc, DbType.DateTime2);

        await _factory.ExecuteSqlAsync(sql, parameters);
    }

    private async Task SeedInspectionRunAsync(
        Guid detectorId,
        DateTime executionTimestampUtc,
        string outcome = "Passed",
        string triggerSource = "Scheduled")
    {
        const string sql = """
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc
            ) VALUES (
                @Id, @DetectorId, @ExecutionTimestampUtc, @TriggerSource, @Outcome, 120, @FailureReason, @ExecutionTimestampUtc
            );
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Id", Guid.NewGuid());
        parameters.Add("DetectorId", detectorId);
        parameters.Add("ExecutionTimestampUtc", executionTimestampUtc, DbType.DateTime2);
        parameters.Add("TriggerSource", triggerSource);
        parameters.Add("Outcome", outcome);
        parameters.Add("FailureReason", outcome == "ExecutionFailed" ? "Probe timeout" : null);

        await _factory.ExecuteSqlAsync(sql, parameters);
    }

    [Fact]
    public async Task GetReliabilityKpi_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        // Act
        var response = await _client.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReliabilityKpi_WhenFromUtcAfterToUtc_Returns422ValidationProblemDetails()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var fromUtc = new DateTime(2026, 11, 22, 17, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Errors.Should().ContainKey("FromUtc");
        problem.Errors["FromUtc"].Should().ContainMatch("*FromUtc*ToUtc*");
    }

    [Fact]
    public async Task GetReliabilityKpi_WithMissingOrMalformedDateQuery_Returns400BadRequest()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        // Act
        var missingResponse = await authClient.GetAsync("/api/history/reliability-kpi");
        var invalidFormatResponse = await authClient.GetAsync("/api/history/reliability-kpi?fromUtc=not-a-date&toUtc=2026-11-22T00:00:00Z");

        // Assert
        missingResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        missingProblem.Should().NotBeNull();
        missingProblem!.Status.Should().Be(StatusCodes.Status400BadRequest);

        invalidFormatResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidProblem = await invalidFormatResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        invalidProblem.Should().NotBeNull();
        invalidProblem!.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetReliabilityKpi_WithZeroIssuesAndZeroRuns_ReturnsZeroMetrics100PassRateAndExplicitJsonNulls()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();
        var jsonNode = JsonNode.Parse(rawJson)!.AsObject();
        jsonNode.ContainsKey("avgTimeToAcknowledgeHours").Should().BeTrue();
        jsonNode["avgTimeToAcknowledgeHours"].Should().BeNull();
        jsonNode.ContainsKey("avgTimeToResolveHours").Should().BeTrue();
        jsonNode["avgTimeToResolveHours"].Should().BeNull();

        var kpi = JsonSerializer.Deserialize<ReliabilityKpiResponse>(rawJson, JsonOptions);
        kpi.Should().NotBeNull();
        kpi!.TotalIssuesCreated.Should().Be(0);
        kpi.ResolutionRatePercentage.Should().Be(0.0);
        kpi.AvgTimeToAcknowledgeHours.Should().BeNull();
        kpi.AvgTimeToResolveHours.Should().BeNull();
        kpi.DetectorPassRatePercentage.Should().Be(100.0);
        kpi.ReopenedIssuesCount.Should().Be(0);
        kpi.ScopeBreakdown.Should().BeEmpty();
        kpi.SeverityBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReliabilityKpi_WithMixedLifecycleCohorts_ComputesAllSixMetricsAndBreakdownsAccurately()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync(name: "Billing Integrity Watchdog");
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc); // 2026-11-22 00:00:00 WIB
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        // Issue 1: Created, acknowledged (1.0h MTTA), and resolved (4.0h MTTR) inside period
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "Ledger imbalance", "KEY-1",
            scope: "DataIntegrity", currentSeverity: "Critical", severityRank: 4, status: "Resolved",
            createdAtUtc: fromUtc.AddHours(1),
            acknowledgedAtUtc: fromUtc.AddHours(2),
            resolvedAtUtc: fromUtc.AddHours(5));

        // Issue 2: Created, acknowledged (2.0h MTTA), and dismissed inside period
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "False alarm mismatch", "KEY-2",
            scope: "DataIntegrity", currentSeverity: "High", severityRank: 3, status: "Dismissed",
            createdAtUtc: fromUtc.AddHours(2),
            acknowledgedAtUtc: fromUtc.AddHours(4),
            dismissedAtUtc: fromUtc.AddHours(6));

        // Issue 3: Created inside period, still Open (unacknowledged, unresolved)
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "Queue worker lag", "KEY-3",
            scope: "Infrastructure", currentSeverity: "Medium", severityRank: 2, status: "Open",
            createdAtUtc: fromUtc.AddHours(3));

        // Issue 4: Created BEFORE period (5 days prior = 120h before fromUtc), resolved inside period (+10h => 130.0h MTTR)
        // Must NOT affect TotalIssuesCreated or breakdowns, MUST affect ResolutionRate and MTTR
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "Prior backlog resolved", "KEY-4",
            scope: "DataSync", currentSeverity: "Low", severityRank: 1, status: "Resolved",
            createdAtUtc: fromUtc.AddDays(-5),
            acknowledgedAtUtc: fromUtc.AddDays(-5).AddHours(1),
            resolvedAtUtc: fromUtc.AddHours(10));

        // Issue 5: Created BEFORE period, reopened inside period (ReopenedIssuesCount = 1)
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "Recurred sync anomaly", "KEY-5",
            scope: "ProcessIntegrity", currentSeverity: "High", severityRank: 3, status: "Reopened",
            createdAtUtc: fromUtc.AddDays(-3),
            reopenedAtUtc: fromUtc.AddHours(7));

        // Issue 6: Outside period (after toUtc) — completely excluded
        await SeedIssueAsync(
            detectorId, "Billing Integrity Watchdog", "Future issue", "KEY-6",
            scope: "Infrastructure", currentSeverity: "Critical", severityRank: 4, status: "Resolved",
            createdAtUtc: toUtc.AddMinutes(5),
            acknowledgedAtUtc: toUtc.AddMinutes(10),
            resolvedAtUtc: toUtc.AddMinutes(20));

        // Runs inside period: 3 Passed, 1 FindingDiscovered, 1 ExecutionFailed => 3 / 5 = 60.0%
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(1), "Passed");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(2), "Passed");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(3), "Passed");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(4), "FindingDiscovered");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(5), "ExecutionFailed");

        // Runs outside period — excluded
        await SeedInspectionRunAsync(detectorId, fromUtc.AddMinutes(-1), "Passed");
        await SeedInspectionRunAsync(detectorId, toUtc.AddMinutes(1), "ExecutionFailed");

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpi = await response.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        kpi.Should().NotBeNull();

        // 3 created in period (KEY-1, KEY-2, KEY-3)
        kpi!.TotalIssuesCreated.Should().Be(3);

        // 2 resolved (KEY-1, KEY-4) / (2 resolved + 1 dismissed (KEY-2)) = 2/3 = 66.7%
        kpi.ResolutionRatePercentage.Should().Be(66.7);

        // MTTA for issues acknowledged in period (KEY-1 = 1.0h, KEY-2 = 2.0h) => avg 1.5h
        kpi.AvgTimeToAcknowledgeHours.Should().BeApproximately(1.5, 0.001);

        // MTTR for issues resolved in period (KEY-1 = 4.0h, KEY-4 = 130.0h) => avg 67.0h
        kpi.AvgTimeToResolveHours.Should().BeApproximately(67.0, 0.001);

        // Detector pass rate: 3 Passed / 5 Total = 60.0%
        kpi.DetectorPassRatePercentage.Should().Be(60.0);

        // Reopened in period: KEY-5 = 1
        kpi.ReopenedIssuesCount.Should().Be(1);

        // Scope breakdown (of 3 created in period: 2 DataIntegrity, 1 Infrastructure)
        kpi.ScopeBreakdown.Should().Equal(
            new BreakdownItemDto("DataIntegrity", 2, 66.7),
            new BreakdownItemDto("Infrastructure", 1, 33.3));

        // Severity breakdown (ordered Critical -> High -> Medium: 1 each = 33.3%)
        kpi.SeverityBreakdown.Should().Equal(
            new BreakdownItemDto("Critical", 1, 33.3),
            new BreakdownItemDto("High", 1, 33.3),
            new BreakdownItemDto("Medium", 1, 33.3));
    }

    [Fact]
    public async Task GetReliabilityKpi_ReopenedIssueResolvedInPeriod_ComputesMttrFromReopenedAtUtc()
    {
        // Arrange (DEC-002)
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync(name: "Recurrence Turnaround Detector");
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        // Created 10 days before period, reopened at from + 1h, resolved at from + 3h30m => MTTR must be 2.5h (NOT 243.5h)
        await SeedIssueAsync(
            detectorId, "Recurrence Turnaround Detector", "Recurring anomaly", "REC-001",
            status: "Resolved",
            createdAtUtc: fromUtc.AddDays(-10),
            reopenedAtUtc: fromUtc.AddHours(1),
            resolvedAtUtc: fromUtc.AddHours(3).AddMinutes(30));

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpi = await response.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        kpi.Should().NotBeNull();
        kpi!.TotalIssuesCreated.Should().Be(0);
        kpi.ReopenedIssuesCount.Should().Be(1);
        kpi.ResolutionRatePercentage.Should().Be(100.0);
        kpi.AvgTimeToResolveHours.Should().BeApproximately(2.5, 0.001);
        kpi.AvgTimeToAcknowledgeHours.Should().BeNull();
    }

    [Fact]
    public async Task GetReliabilityKpi_BoundaryConditions_AreInclusiveAtPeriodEndpointsAndRespectUtcZNotation()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync(name: "Boundary Precision Detector");
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc); // Midnight WIB
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc); // 23:59:59.999 WIB

        // Exactly at fromUtc (included)
        await SeedIssueAsync(
            detectorId, "Boundary Precision Detector", "At exact start", "BND-START",
            status: "Open", createdAtUtc: fromUtc);

        // Exactly at toUtc (included)
        await SeedIssueAsync(
            detectorId, "Boundary Precision Detector", "At exact end", "BND-END",
            status: "Open", createdAtUtc: toUtc);

        // 1 millisecond before fromUtc (excluded)
        await SeedIssueAsync(
            detectorId, "Boundary Precision Detector", "Just before start", "BND-BEFORE",
            status: "Open", createdAtUtc: fromUtc.AddMilliseconds(-1));

        // 1 millisecond after toUtc (excluded)
        await SeedIssueAsync(
            detectorId, "Boundary Precision Detector", "Just after end", "BND-AFTER",
            status: "Open", createdAtUtc: toUtc.AddMilliseconds(1));

        // Runs exactly on boundaries vs 1ms outside
        await SeedInspectionRunAsync(detectorId, fromUtc, "Passed");
        await SeedInspectionRunAsync(detectorId, toUtc, "ExecutionFailed");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddMilliseconds(-1), "ExecutionFailed");
        await SeedInspectionRunAsync(detectorId, toUtc.AddMilliseconds(1), "ExecutionFailed");

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpi = await response.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        kpi.Should().NotBeNull();
        kpi!.TotalIssuesCreated.Should().Be(2);
        kpi.DetectorPassRatePercentage.Should().Be(50.0);

        // Equal FromUtc == ToUtc boundary check
        var pointResponse = await authClient.GetAsync(BuildKpiUrl(fromUtc, fromUtc));
        pointResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pointKpi = await pointResponse.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        pointKpi!.TotalIssuesCreated.Should().Be(1);
        pointKpi.DetectorPassRatePercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task GetReliabilityKpi_WhenOnlyDismissedIssuesAndAllRunsFailed_ReturnsZeroRates()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync(name: "Zero Rate Edge Case Detector");
        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        await SeedIssueAsync(
            detectorId, "Zero Rate Edge Case Detector", "Dismissed 1", "DIS-1",
            status: "Dismissed", createdAtUtc: fromUtc.AddHours(1), dismissedAtUtc: fromUtc.AddHours(2));
        await SeedIssueAsync(
            detectorId, "Zero Rate Edge Case Detector", "Dismissed 2", "DIS-2",
            status: "Dismissed", createdAtUtc: fromUtc.AddHours(2), dismissedAtUtc: fromUtc.AddHours(3));

        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(1), "ExecutionFailed");
        await SeedInspectionRunAsync(detectorId, fromUtc.AddHours(2), "FindingDiscovered");

        // Act
        var response = await authClient.GetAsync(BuildKpiUrl(fromUtc, toUtc));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpi = await response.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        kpi.Should().NotBeNull();
        kpi!.TotalIssuesCreated.Should().Be(2);
        kpi.ResolutionRatePercentage.Should().Be(0.0);
        kpi.AvgTimeToAcknowledgeHours.Should().BeNull();
        kpi.AvgTimeToResolveHours.Should().BeNull();
        kpi.DetectorPassRatePercentage.Should().Be(0.0);
    }

    [Fact]
    public async Task SwaggerDocument_IncludesReliabilityKpiEndpointAndExpectedSchemas()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var rootNode = JsonNode.Parse(content)!;

        var paths = rootNode["paths"]!.AsObject();
        paths.ContainsKey("/api/history/reliability-kpi").Should().BeTrue();
        var getOp = paths["/api/history/reliability-kpi"]!["get"]!;
        getOp["tags"]!.AsArray().Select(t => t!.GetValue<string>()).Should().Contain("History");

        var schemas = rootNode["components"]!["schemas"]!.AsObject();
        schemas.ContainsKey("ReliabilityKpiResponse").Should().BeTrue();
        schemas.ContainsKey("BreakdownItemDto").Should().BeTrue();
    }
}
