using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.AssignIssue;
using PrIMA.Api.Features.Issues.DismissIssue;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Infrastructure.Webhooks;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

/// <summary>
/// Consolidated Stage 4 Quality Gate suite enforcing all 10 canonical domain business rules
/// (BR-PRIMA-001 through BR-PRIMA-010) against the live SQL Server integration database (DEC-008).
/// </summary>
[Collection("ApiTestCollection")]
public class DomainBusinessRulesQualityGateTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DomainBusinessRulesQualityGateTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    #region Shared Helpers

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _factory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private MonitoringSupervisorWorker CreateSupervisorWorker(int slaTimeoutMinutes = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supervisor:PollingIntervalSeconds"] = "60",
                ["Supervisor:UnacknowledgedSlaTimeoutMinutes"] = slaTimeoutMinutes.ToString()
            })
            .Build();

        return new MonitoringSupervisorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<MonitoringSupervisorWorker>.Instance);
    }

    private async Task<Guid> SeedConfiguredDetectorAsync(
        string name,
        int expectedCadenceSeconds = 300,
        DateTime? lastHealthyHeartbeatUtc = null,
        string? webhookUrl = null,
        string scope = "DataIntegrity",
        string targetModule = "Billing")
    {
        var id = Guid.NewGuid();
        var heartbeat = lastHealthyHeartbeatUtc ?? DateTime.UtcNow;

        const string sql = """
            INSERT INTO dbo.Detectors (
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
            ) VALUES (
                @Id, @Name, 'Quality gate test detector', @Scope, @TargetModule, @Cadence,
                @WebhookUrl, 'Active', 'Healthy', @Heartbeat, @Heartbeat, @Heartbeat
            );
            """;

        await _factory.ExecuteSqlAsync(sql, new
        {
            Id = id,
            Name = name,
            Scope = scope,
            TargetModule = targetModule,
            Cadence = expectedCadenceSeconds,
            WebhookUrl = webhookUrl,
            Heartbeat = heartbeat
        });

        return id;
    }

    private async Task<IngestTelemetryResponse> IngestFindingAsync(
        Guid detectorId,
        string targetEntityReference,
        string title = "Quality Gate Anomaly",
        string baseSeverity = "Medium",
        DateTime? executionTimestampUtc = null,
        FindingEvidenceItem? evidence = null)
    {
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTimestampUtc ?? DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 110,
            Findings =
            [
                new FindingItem
                {
                    TargetEntityReference = targetEntityReference,
                    Title = title,
                    BaseSeverity = baseSeverity,
                    Evidence = evidence ?? new FindingEvidenceItem
                    {
                        Module = "Billing",
                        SourceTable = "dbo.BillingJournals",
                        RecordKeys = targetEntityReference,
                        DebitTotal = 1_500_000m,
                        CreditTotal = 1_250_000m,
                        VarianceDelta = 250_000m,
                        Trace = $"Detected variance on {targetEntityReference}"
                    }
                }
            ]
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(ApiKeyEndpointFilter.HeaderName, PrIMAApiFactory.TestApiKey);

        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private async Task<IngestTelemetryResponse> IngestFailureAsync(
        Guid detectorId,
        string failureReason,
        DateTime? executionTimestampUtc = null)
    {
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTimestampUtc ?? DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 420,
            FailureReason = failureReason
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(ApiKeyEndpointFilter.HeaderName, PrIMAApiFactory.TestApiKey);

        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private Task<IssueRow?> GetIssueByIdAsync(Guid issueId) =>
        _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            """
            SELECT Id, CorrelationKey, Scope, BaseSeverity, CurrentSeverity, SeverityRank, Status,
                   EscalationLevel, OccurrenceCount, CurrentAssignee, AcknowledgedAtUtc,
                   ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Id = @Id;
            """,
            new { Id = issueId });

    private Task<IssueRow?> GetIssueByCorrelationKeyAsync(string correlationKey) =>
        _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            """
            SELECT Id, CorrelationKey, Scope, BaseSeverity, CurrentSeverity, SeverityRank, Status,
                   EscalationLevel, OccurrenceCount, CurrentAssignee, AcknowledgedAtUtc,
                   ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE CorrelationKey = @Key;
            """,
            new { Key = correlationKey });

    private sealed record IssueRow(
        Guid Id,
        string CorrelationKey,
        string Scope,
        string BaseSeverity,
        string CurrentSeverity,
        byte SeverityRank,
        string Status,
        int EscalationLevel,
        int OccurrenceCount,
        string? CurrentAssignee,
        DateTime? AcknowledgedAtUtc,
        DateTime? ResolvedAtUtc,
        DateTime? DismissedAtUtc,
        DateTime? ReopenedAtUtc);

    private sealed record OccurrenceSnapshotRow(
        Guid Id,
        Guid IssueId,
        Guid InspectionRunId,
        int OccurrenceNumber,
        DateTime TimestampUtc,
        string Module,
        string? SourceTable,
        string? RecordKeys,
        decimal? DebitTotal,
        decimal? CreditTotal,
        decimal? VarianceDelta,
        string? TraceString);

    private sealed record ActivityLogRow(
        string ActivityType,
        string ActorName,
        string Summary);

    private sealed record InspectionRunSummaryRow(
        string Outcome,
        string TriggerSource,
        string? FailureReason);

    private sealed class TimeoutSimulationHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing.",
                new TimeoutException());
        }
    }

    #endregion

    [Fact]
    public async Task BR_PRIMA_001_IngestingDuplicateFindingWithMatchingCorrelationKey_AppendsOccurrenceWithoutDuplicatingIssue()
    {
        // Arrange
        var detectorId = await SeedConfiguredDetectorAsync("BR001-Deduplication-Detector");
        var now = DateTime.UtcNow;

        // Act 1: Initial finding ingestion creates Issue + Occurrence #1
        var firstIngest = await IngestFindingAsync(
            detectorId,
            "INV-DUP-001",
            title: "Unbalanced Invoice Journal",
            baseSeverity: "High",
            executionTimestampUtc: now.AddMinutes(-10));

        firstIngest.NewIssuesCreated.Should().Be(1);
        firstIngest.RecurrencesRecorded.Should().Be(0);
        firstIngest.Results.Should().ContainSingle();
        firstIngest.Results[0].Action.Should().Be("Created");
        firstIngest.Results[0].OccurrenceNumber.Should().Be(1);

        var issueId = firstIngest.Results[0].IssueId;
        var expectedCorrelationKey = $"{detectorId:D}:INV-DUP-001";
        firstIngest.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Act 2: Duplicate finding (different casing/whitespace normalizing to identical CorrelationKey)
        var secondIngest = await IngestFindingAsync(
            detectorId,
            "  inv-dup-001  ",
            title: "Unbalanced Invoice Journal",
            baseSeverity: "High",
            executionTimestampUtc: now.AddMinutes(-5));

        secondIngest.NewIssuesCreated.Should().Be(0);
        secondIngest.RecurrencesRecorded.Should().Be(1);
        secondIngest.Results[0].Action.Should().Be("Appended");
        secondIngest.Results[0].IssueId.Should().Be(issueId);
        secondIngest.Results[0].OccurrenceNumber.Should().Be(2);

        // Act 3: Acknowledge issue and ingest 3rd duplicate finding on active Acknowledged issue
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var ackResponse = await authClient.PostAsync($"/api/issues/{issueId}/acknowledge", null);
        ackResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var thirdIngest = await IngestFindingAsync(
            detectorId,
            "INV-DUP-001",
            title: "Unbalanced Invoice Journal",
            baseSeverity: "High",
            executionTimestampUtc: now);

        thirdIngest.NewIssuesCreated.Should().Be(0);
        thirdIngest.RecurrencesRecorded.Should().Be(1);
        thirdIngest.Results[0].Action.Should().Be("Appended");
        thirdIngest.Results[0].IssueId.Should().Be(issueId);
        thirdIngest.Results[0].OccurrenceNumber.Should().Be(3);

        // Assert: Single row in dbo.Issues with OccurrenceCount = 3 and 3 sequential rows in dbo.Occurrences
        var issueCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.Issues WHERE CorrelationKey = @Key;",
            new { Key = expectedCorrelationKey });
        issueCount.Should().Be(1);

        var issue = await GetIssueByIdAsync(issueId);

        issue.Should().NotBeNull();
        issue!.OccurrenceCount.Should().Be(3);
        issue.Status.Should().Be("Acknowledged");

        var occurrenceNumbers = (await _factory.QuerySqlAsync<int>(
            "SELECT OccurrenceNumber FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC;",
            new { IssueId = issueId })).ToList();
        occurrenceNumbers.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task BR_PRIMA_002_IngestingFindingOnResolvedOrDismissedIssue_TransitionsToReopenedSupersedesGovernanceAndResetsAssignee()
    {
        // Branch A: Resolved issue reopened by recurring finding
        var resolvedDetectorId = await SeedConfiguredDetectorAsync("BR002-Resolved-Detector");
        var ahmadClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var now = DateTime.UtcNow;

        var resIngest1 = await IngestFindingAsync(
            resolvedDetectorId,
            "REOPEN-RESOLVED-01",
            title: "Resolved Variance Anomaly",
            baseSeverity: "High",
            executionTimestampUtc: now.AddMinutes(-20));
        var resolvedIssueId = resIngest1.Results[0].IssueId;

        (await ahmadClient.PostAsync($"/api/issues/{resolvedIssueId}/take-it", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ahmadClient.PostAsJsonAsync($"/api/issues/{resolvedIssueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "Adjusted unbalanced journal entry JRN-202.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ahmadClient.PostAsJsonAsync($"/api/issues/{resolvedIssueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Verified zero variance remaining in ledger.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ahmadClient.PostAsync($"/api/issues/{resolvedIssueId}/resolve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act A: Ingest recurring finding on Resolved issue
        var resIngest2 = await IngestFindingAsync(
            resolvedDetectorId,
            "REOPEN-RESOLVED-01",
            title: "Resolved Variance Anomaly",
            baseSeverity: "High",
            executionTimestampUtc: now.AddMinutes(-5));

        // Assert A
        resIngest2.Results[0].Action.Should().Be("Reopened");
        resIngest2.Results[0].IssueId.Should().Be(resolvedIssueId);
        resIngest2.Results[0].OccurrenceNumber.Should().Be(2);

        var reopenedFromResolved = await GetIssueByIdAsync(resolvedIssueId);

        reopenedFromResolved.Should().NotBeNull();
        reopenedFromResolved!.Status.Should().Be("Reopened");
        reopenedFromResolved.CurrentAssignee.Should().BeNull();
        reopenedFromResolved.AcknowledgedAtUtc.Should().BeNull();
        reopenedFromResolved.ResolvedAtUtc.Should().BeNull();
        reopenedFromResolved.ReopenedAtUtc.Should().NotBeNull();
        reopenedFromResolved.OccurrenceCount.Should().Be(2);

        (await _factory.QuerySqlAsync<bool>(
            "SELECT IsSuperseded FROM dbo.RemediationRecords WHERE IssueId = @IssueId;",
            new { IssueId = resolvedIssueId })).Should().AllSatisfy(s => s.Should().BeTrue());

        (await _factory.QuerySqlAsync<bool>(
            "SELECT IsSuperseded FROM dbo.VerificationRecords WHERE IssueId = @IssueId;",
            new { IssueId = resolvedIssueId })).Should().AllSatisfy(s => s.Should().BeTrue());

        (await _factory.QuerySqlAsync<int>(
            "SELECT OccurrenceNumber FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber;",
            new { IssueId = resolvedIssueId })).Should().Equal(1, 2);

        // Branch B: Dismissed issue reopened by recurring finding
        var dismissedDetectorId = await SeedConfiguredDetectorAsync("BR002-Dismissed-Detector");
        var edpLeadClient = await CreateAuthenticatedClientAsync("edp.lead");

        var disIngest1 = await IngestFindingAsync(
            dismissedDetectorId,
            "REOPEN-DISMISSED-01",
            title: "Dismissed Sync Exception",
            baseSeverity: "Medium",
            executionTimestampUtc: now.AddMinutes(-15));
        var dismissedIssueId = disIngest1.Results[0].IssueId;

        var ahmadId = await _factory.QuerySingleOrDefaultSqlAsync<Guid>(
            "SELECT Id FROM dbo.Officers WHERE Username = 'ahmad.budi';");
        (await edpLeadClient.PostAsJsonAsync($"/api/issues/{dismissedIssueId}/assign", new AssignIssueRequest(ahmadId)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await edpLeadClient.PostAsJsonAsync($"/api/issues/{dismissedIssueId}/dismiss",
            new DismissIssueRequest("FalsePositive", "Confirmed transient batch window exception with finance team.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act B: Ingest recurring finding on Dismissed issue
        var disIngest2 = await IngestFindingAsync(
            dismissedDetectorId,
            "REOPEN-DISMISSED-01",
            title: "Dismissed Sync Exception",
            baseSeverity: "Medium",
            executionTimestampUtc: now);

        // Assert B
        disIngest2.Results[0].Action.Should().Be("Reopened");
        disIngest2.Results[0].IssueId.Should().Be(dismissedIssueId);
        disIngest2.Results[0].OccurrenceNumber.Should().Be(2);

        var reopenedFromDismissed = await GetIssueByIdAsync(dismissedIssueId);

        reopenedFromDismissed.Should().NotBeNull();
        reopenedFromDismissed!.Status.Should().Be("Reopened");
        reopenedFromDismissed.CurrentAssignee.Should().BeNull();
        reopenedFromDismissed.AcknowledgedAtUtc.Should().BeNull();
        reopenedFromDismissed.DismissedAtUtc.Should().BeNull();
        reopenedFromDismissed.ReopenedAtUtc.Should().NotBeNull();
        reopenedFromDismissed.OccurrenceCount.Should().Be(2);

        (await _factory.QuerySqlAsync<bool>(
            "SELECT IsSuperseded FROM dbo.DismissalRecords WHERE IssueId = @IssueId;",
            new { IssueId = dismissedIssueId })).Should().AllSatisfy(s => s.Should().BeTrue());

        (await _factory.QuerySqlAsync<int>(
            "SELECT OccurrenceNumber FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber;",
            new { IssueId = dismissedIssueId })).Should().Equal(1, 2);

        var reopenLogs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId IN (@Id1, @Id2) AND ActivityType = 'Reopened';",
            new { Id1 = resolvedIssueId, Id2 = dismissedIssueId })).ToList();
        reopenLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task BR_PRIMA_003_DynamicSeverityEscalation_ElevatesUnacknowledgedOpenPastSlaAndFivePlusOccurrencesCappedAtCritical()
    {
        // Arrange: Detector with fresh heartbeat so silence phase does not trigger
        var detectorId = await SeedConfiguredDetectorAsync("BR003-Escalation-Detector", expectedCadenceSeconds: 300);
        var now = DateTime.UtcNow;

        // 1. Unacknowledged Open issue past 30-minute SLA timeout (Low -> Medium)
        var slaIngest = await IngestFindingAsync(
            detectorId,
            "SLA-BREACH-01",
            title: "Unacknowledged Billing Delay",
            baseSeverity: "Low",
            executionTimestampUtc: now.AddMinutes(-35));
        var slaIssueId = slaIngest.Results[0].IssueId;

        // 2. Acknowledged High issue reaching >= 5 occurrences (High -> Critical)
        var recurIngest = await IngestFindingAsync(
            detectorId,
            "RECUR-ESCALATE-01",
            title: "Chronic Sync Mismatch",
            baseSeverity: "High",
            executionTimestampUtc: now.AddMinutes(-10));
        var recurIssueId = recurIngest.Results[0].IssueId;

        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        (await authClient.PostAsync($"/api/issues/{recurIssueId}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        for (var i = 2; i <= 5; i++)
        {
            await IngestFindingAsync(
                detectorId,
                "RECUR-ESCALATE-01",
                title: "Chronic Sync Mismatch",
                baseSeverity: "High",
                executionTimestampUtc: now.AddMinutes(-10 + i));
        }

        // 3. Unacknowledged Critical issue past SLA + 5 occurrences (must remain capped at Critical)
        var capIngest = await IngestFindingAsync(
            detectorId,
            "CRITICAL-CAP-01",
            title: "Already Critical Outage",
            baseSeverity: "Critical",
            executionTimestampUtc: now.AddMinutes(-90));
        var capIssueId = capIngest.Results[0].IssueId;

        for (var i = 2; i <= 5; i++)
        {
            await IngestFindingAsync(
                detectorId,
                "CRITICAL-CAP-01",
                title: "Already Critical Outage",
                baseSeverity: "Critical",
                executionTimestampUtc: now.AddMinutes(-5 + i));
        }

        var worker = CreateSupervisorWorker(slaTimeoutMinutes: 30);

        // Act: Cycle 1 evaluates SLA + recurrence escalations
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        // Assert Cycle 1
        var slaIssue = await GetIssueByIdAsync(slaIssueId);
        slaIssue!.CurrentSeverity.Should().Be("Medium");
        slaIssue.SeverityRank.Should().Be(2);
        slaIssue.EscalationLevel.Should().Be(1);

        var recurIssue = await GetIssueByIdAsync(recurIssueId);
        recurIssue!.OccurrenceCount.Should().Be(5);
        recurIssue.CurrentSeverity.Should().Be("Critical");
        recurIssue.SeverityRank.Should().Be(4);
        recurIssue.EscalationLevel.Should().Be(1);

        var capIssue = await GetIssueByIdAsync(capIssueId);
        capIssue!.CurrentSeverity.Should().Be("Critical");
        capIssue.SeverityRank.Should().Be(4);
        capIssue.EscalationLevel.Should().Be(0);

        // Act: Cycle 2 well past SLA steps slaIssue again (Medium -> High) while leaving Critical issues capped at Critical
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now.AddHours(2));

        var slaAfterSecondPass = await GetIssueByIdAsync(slaIssueId);
        slaAfterSecondPass!.CurrentSeverity.Should().Be("High");
        slaAfterSecondPass.SeverityRank.Should().Be(3);
        slaAfterSecondPass.EscalationLevel.Should().Be(2);

        var recurAfterSecondPass = await GetIssueByIdAsync(recurIssueId);
        recurAfterSecondPass!.CurrentSeverity.Should().Be("Critical");
        recurAfterSecondPass.SeverityRank.Should().Be(4);
        recurAfterSecondPass.EscalationLevel.Should().Be(1);

        var capAfterSecondPass = await GetIssueByIdAsync(capIssueId);
        capAfterSecondPass!.CurrentSeverity.Should().Be("Critical");
        capAfterSecondPass.SeverityRank.Should().Be(4);
        capAfterSecondPass.EscalationLevel.Should().Be(0);

        var slaLogs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'SeverityEscalated';",
            new { Id = slaIssueId })).ToList();
        slaLogs.Should().HaveCount(2);

        var recurLogs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'SeverityEscalated';",
            new { Id = recurIssueId })).ToList();
        recurLogs.Should().HaveCount(1);

        var capLogs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'SeverityEscalated';",
            new { Id = capIssueId })).ToList();
        capLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task BR_PRIMA_004_MandatoryVerificationGate_BlocksResolveWith409UntilCleanPassedVerificationRecorded()
    {
        // Arrange
        var detectorId = await SeedConfiguredDetectorAsync("BR004-VerificationGate-Detector");
        var ingest = await IngestFindingAsync(detectorId, "VER-GATE-01", baseSeverity: "High");
        var issueId = ingest.Results[0].IssueId;
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");

        (await authClient.PostAsync($"/api/issues/{issueId}/take-it", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Attempt 1: Resolve without remediation or verification -> 409 Conflict
        var resolveNoRecords = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveNoRecords.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem1 = await resolveNoRecords.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem1!.Detail.Should().Contain("verification gate");

        // Record remediation only
        (await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "Reconciled duplicate invoice posting.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Attempt 2: Resolve with remediation but no verification -> 409 Conflict
        var resolveNoVerification = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveNoVerification.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Attempt 3: Record failed verification (CleanPassed = false) -> Resolve still 409 Conflict
        (await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(false, "Re-query still detected 1 unmatched posting.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolveFailedVerification = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveFailedVerification.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Act: Record clean passed verification (CleanPassed = true) -> Resolve succeeds
        (await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Re-query confirmed zero variance rows remaining.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolveSuccess = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        // Assert
        resolveSuccess.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolvedIssue = await GetIssueByIdAsync(issueId);

        resolvedIssue.Should().NotBeNull();
        resolvedIssue!.Status.Should().Be("Resolved");
        resolvedIssue.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task BR_PRIMA_005_FailSafeInfrastructureSynthesis_CreatesSilentAndFailIssues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var silentDetectorId = await SeedConfiguredDetectorAsync(
            "BR005-Silent-Detector",
            expectedCadenceSeconds: 60,
            lastHealthyHeartbeatUtc: now.AddSeconds(-150),
            scope: "DataIntegrity",
            targetModule: "Pharmacy");

        var failingDetectorId = await SeedConfiguredDetectorAsync(
            "BR005-Failing-Detector",
            expectedCadenceSeconds: 300,
            lastHealthyHeartbeatUtc: now,
            scope: "DataSync",
            targetModule: "HL7Bridge");

        var worker = CreateSupervisorWorker();

        // Act 1: Missed heartbeat triggers SILENT:{id}
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        // Act 2: ExecutionFailed telemetry triggers FAIL:{id}
        var failIngest = await IngestFailureAsync(
            failingDetectorId,
            "SIMRS HL7 endpoint connection refused during scheduled sweep.",
            executionTimestampUtc: now);

        // Assert SILENT:{id}
        var silentIssue = await GetIssueByCorrelationKeyAsync($"SILENT:{silentDetectorId}");

        silentIssue.Should().NotBeNull();
        silentIssue!.CorrelationKey.Should().Be($"SILENT:{silentDetectorId}");
        silentIssue.Scope.Should().Be("Infrastructure");
        silentIssue.Status.Should().Be("Open");
        silentIssue.OccurrenceCount.Should().Be(1);

        var silentHealth = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = silentDetectorId });
        silentHealth.Should().Be("Silent");

        // Assert FAIL:{id}
        failIngest.NewIssuesCreated.Should().Be(1);
        failIngest.Results[0].CorrelationKey.Should().Be($"FAIL:{failingDetectorId}");
        failIngest.Results[0].Action.Should().Be("Created");

        var failIssue = await GetIssueByCorrelationKeyAsync($"FAIL:{failingDetectorId}");

        failIssue.Should().NotBeNull();
        failIssue!.CorrelationKey.Should().Be($"FAIL:{failingDetectorId}");
        failIssue.Scope.Should().Be("Infrastructure");
        failIssue.Status.Should().Be("Open");
        failIssue.OccurrenceCount.Should().Be(1);

        var failingHealth = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = failingDetectorId });
        failingHealth.Should().Be("Failing");
    }

    [Fact]
    public async Task BR_PRIMA_006_DismissalJustification_FailsWith422Under20CharsAndSucceedsAt20OrMore()
    {
        // Arrange
        var detectorId = await SeedConfiguredDetectorAsync("BR006-Dismissal-Detector");
        var ingest = await IngestFindingAsync(detectorId, "DISMISS-VAL-01", baseSeverity: "Low");
        var issueId = ingest.Results[0].IssueId;
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Act 1: 19 characters (< 20) -> 422 Unprocessable Entity
        const string nineteenCharRationale = "1234567890123456789";
        nineteenCharRationale.Length.Should().Be(19);
        var tooShortResp = await authClient.PostAsJsonAsync(
            $"/api/issues/{issueId}/dismiss",
            new DismissIssueRequest("FalsePositive", nineteenCharRationale));
        tooShortResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Act 2: Whitespace-padded short rationale (trimmed length < 20) -> 422 Unprocessable Entity
        var paddedShortResp = await authClient.PostAsJsonAsync(
            $"/api/issues/{issueId}/dismiss",
            new DismissIssueRequest("FalsePositive", "   too short text    "));
        paddedShortResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Assert issue remains Open with 0 dismissal records
        var statusBeforeValid = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT Status FROM dbo.Issues WHERE Id = @Id;",
            new { Id = issueId });
        statusBeforeValid.Should().Be("Open");

        var dismissalCountBefore = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.DismissalRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        dismissalCountBefore.Should().Be(0);

        // Act 3: Exact 20-character rationale (>= 20) -> 204 NoContent
        const string twentyCharRationale = "Valid 20-char reason";
        twentyCharRationale.Length.Should().Be(20);
        var validResp = await authClient.PostAsJsonAsync(
            $"/api/issues/{issueId}/dismiss",
            new DismissIssueRequest("AcceptedOperationalException", twentyCharRationale));

        // Assert
        validResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dismissedIssue = await GetIssueByIdAsync(issueId);

        dismissedIssue!.Status.Should().Be("Dismissed");
        dismissedIssue.DismissedAtUtc.Should().NotBeNull();

        var dismissalCountAfter = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.DismissalRecords WHERE IssueId = @IssueId AND IsSuperseded = 0;",
            new { IssueId = issueId });
        dismissalCountAfter.Should().Be(1);
    }

    [Fact]
    public async Task BR_PRIMA_007_WebhookClient_Enforces15SecondTimeoutRecordsExecutionFailedAndReturns504ProblemDetails()
    {
        // Arrange: Verify DetectorWebhookClient enforces a strict 15-second timeout on its HttpClient
        var timeoutHttpClient = new HttpClient(new TimeoutSimulationHttpMessageHandler());
        timeoutHttpClient.Timeout.Should().Be(TimeSpan.FromSeconds(100)); // default before client enforcement

        using var baseScope = _factory.Services.CreateScope();
        var webhookClient = new DetectorWebhookClient(
            timeoutHttpClient,
            baseScope.ServiceProvider.GetRequiredService<IDetectorRepository>(),
            baseScope.ServiceProvider.GetRequiredService<IIssueRepository>(),
            baseScope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(),
            NullLogger<DetectorWebhookClient>.Instance);

        timeoutHttpClient.Timeout.Should().Be(TimeSpan.FromSeconds(15));

        var detectorId = await SeedConfiguredDetectorAsync(
            "BR007-Timeout-Detector",
            webhookUrl: "https://probes.internal/api/slow-simrs-probe");

        await using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IDetectorWebhookClient>(_ => webhookClient);
            });
        });

        using var client = customFactory.CreateClient();
        var token = await _factory.GetAuthTokenAsync(client, "ahmad.budi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: Trigger manual run through API endpoint -> hits real DetectorWebhookClient timeout handling
        var response = await client.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);

        // Assert 1: 504 Gateway Timeout ProblemDetails returned
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status504GatewayTimeout);
        problem.Title.Should().Be("Gateway Timeout");
        problem.Detail.Should().Contain("timed out after 15 seconds");

        // Assert 2: ExecutionFailed recorded in dbo.InspectionRuns + FAIL:{id} issue created + detector Failing
        var run = await _factory.QuerySingleOrDefaultSqlAsync<InspectionRunSummaryRow>(
            "SELECT Outcome, TriggerSource, FailureReason FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId;",
            new { DetectorId = detectorId });

        run.Should().NotBeNull();
        run!.Outcome.Should().Be("ExecutionFailed");
        run.TriggerSource.Should().Be("Manual");
        run.FailureReason.Should().Contain("timed out after 15 seconds");

        var health = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = detectorId });
        health.Should().Be("Failing");

        var failIssue = await GetIssueByCorrelationKeyAsync($"FAIL:{detectorId}");
        failIssue.Should().NotBeNull();
    }

    [Fact]
    public async Task BR_PRIMA_008_OperationalAttribution_DeterministicallyRecordsOfficerAcrossLogsRemediationVerificationAndDismissal()
    {
        // Arrange: Three distinct authenticated officers
        var sitiClient = await CreateAuthenticatedClientAsync("siti.rahma"); // FullName: "Siti Rahma"
        var ahmadClient = await CreateAuthenticatedClientAsync("ahmad.budi"); // FullName: "Ahmad Budi"
        var leadClient = await CreateAuthenticatedClientAsync("edp.lead");    // FullName: "EDP Lead"

        var detectorId = await SeedConfiguredDetectorAsync("BR008-Attribution-Detector");
        var ahmadId = await _factory.QuerySingleOrDefaultSqlAsync<Guid>(
            "SELECT Id FROM dbo.Officers WHERE Username = 'ahmad.budi';");

        // Issue 1: Resolution path across Siti Rahma & Ahmad Budi
        var issue1Id = (await IngestFindingAsync(detectorId, "ATTR-RESOLVE-01", baseSeverity: "High")).Results[0].IssueId;

        (await sitiClient.PostAsync($"/api/issues/{issue1Id}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sitiClient.PostAsJsonAsync($"/api/issues/{issue1Id}/assign", new AssignIssueRequest(ahmadId)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ahmadClient.PostAsync($"/api/issues/{issue1Id}/start-investigation", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ahmadClient.PostAsJsonAsync($"/api/issues/{issue1Id}/notes",
            new AddNoteRequest("Verified unbalanced billing entry JRN-8802.")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await ahmadClient.PostAsJsonAsync($"/api/issues/{issue1Id}/remediation",
            new RecordRemediationRequest("DataCorrection", "Corrected billing entry JRN-8802 per SOP.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sitiClient.PostAsJsonAsync($"/api/issues/{issue1Id}/verification/manual",
            new RecordManualVerificationRequest(true, "Peer verification confirmed zero variance.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sitiClient.PostAsync($"/api/issues/{issue1Id}/resolve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Issue 2: Initial assignment + Dismissal path by EDP Lead
        var sitiId = await _factory.QuerySingleOrDefaultSqlAsync<Guid>(
            "SELECT Id FROM dbo.Officers WHERE Username = 'siti.rahma';");
        var issue2Id = (await IngestFindingAsync(detectorId, "ATTR-DISMISS-01", baseSeverity: "Low")).Results[0].IssueId;
        (await leadClient.PostAsJsonAsync($"/api/issues/{issue2Id}/assign", new AssignIssueRequest(sitiId)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await leadClient.PostAsJsonAsync($"/api/issues/{issue2Id}/dismiss",
            new DismissIssueRequest("AcceptedOperationalException",
                "Approved month-end reconciliation deferral by EDP Lead.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert Issue 1 activity logs
        var issue1Logs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC;",
            new { IssueId = issue1Id })).ToList();

        issue1Logs.Should().Contain(l => l.ActivityType == "Created" && l.ActorName == "System");
        issue1Logs.Should().Contain(l => l.ActivityType == "Acknowledged" && l.ActorName == "Siti Rahma");
        issue1Logs.Should().Contain(l => l.ActivityType == "Reassigned" && l.ActorName == "Siti Rahma");
        issue1Logs.Should().Contain(l => l.ActivityType == "InvestigationStarted" && l.ActorName == "Ahmad Budi");
        issue1Logs.Should().Contain(l => l.ActivityType == "NoteAdded" && l.ActorName == "Ahmad Budi");
        issue1Logs.Should().Contain(l => l.ActivityType == "RemediationRecorded" && l.ActorName == "Ahmad Budi");
        issue1Logs.Should().Contain(l => l.ActivityType == "VerificationRecorded" && l.ActorName == "Siti Rahma");
        issue1Logs.Should().Contain(l => l.ActivityType == "Resolved" && l.ActorName == "Siti Rahma");

        // Assert governance tables capture exact officer FullName
        var noteOfficer = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT OfficerName FROM dbo.InvestigationNotes WHERE IssueId = @IssueId;",
            new { IssueId = issue1Id });
        noteOfficer.Should().Be("Ahmad Budi");

        var remediationPerformer = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT PerformedBy FROM dbo.RemediationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issue1Id });
        remediationPerformer.Should().Be("Ahmad Budi");

        var verificationActor = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT VerifiedBy FROM dbo.VerificationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issue1Id });
        verificationActor.Should().Be("Siti Rahma");

        // Assert Issue 2 assignment + dismissal attribution & activity logs
        var issue2Logs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC;",
            new { IssueId = issue2Id })).ToList();
        issue2Logs.Should().Contain(l => l.ActivityType == "Assigned" && l.ActorName == "EDP Lead");
        issue2Logs.Should().Contain(l => l.ActivityType == "Dismissed" && l.ActorName == "EDP Lead");

        var dismissalActor = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT DismissedBy FROM dbo.DismissalRecords WHERE IssueId = @IssueId;",
            new { IssueId = issue2Id });
        dismissalActor.Should().Be("EDP Lead");
    }

    [Fact]
    public async Task BR_PRIMA_009_OccurrenceImmutability_PreservesSequentialSnapshotsAndTelemetryEvidenceIntact()
    {
        // Arrange
        var detectorId = await SeedConfiguredDetectorAsync("BR009-Immutability-Detector", targetModule: "Billing");
        var baseTime = DateTime.UtcNow;
        var t1 = new DateTime(baseTime.Year, baseTime.Month, baseTime.Day, baseTime.Hour, baseTime.Minute, baseTime.Second, DateTimeKind.Utc).AddMinutes(-20);
        var t2 = t1.AddMinutes(10);
        var t3 = t1.AddMinutes(20);

        var evidence1 = new FindingEvidenceItem
        {
            Module = "Billing",
            SourceTable = "dbo.BillingJournals",
            RecordKeys = "JRN-1001",
            DebitTotal = 1_000_000m,
            CreditTotal = 900_000m,
            VarianceDelta = 100_000m,
            Trace = "Snapshot #1: initial IDR 100k imbalance"
        };
        var evidence2 = new FindingEvidenceItem
        {
            Module = "Billing",
            SourceTable = "dbo.BillingJournals",
            RecordKeys = "JRN-1001;JRN-1002",
            DebitTotal = 2_500_000m,
            CreditTotal = 2_000_000m,
            VarianceDelta = 500_000m,
            Trace = "Snapshot #2: escalated IDR 500k imbalance"
        };
        var evidence3 = new FindingEvidenceItem
        {
            Module = "Billing",
            SourceTable = "dbo.BillingJournals",
            RecordKeys = "JRN-1001;JRN-1002;JRN-1003",
            DebitTotal = 3_900_000m,
            CreditTotal = 3_000_000m,
            VarianceDelta = 900_000m,
            Trace = "Snapshot #3: persisted IDR 900k imbalance"
        };

        // Act: Ingest sequential occurrences #1, #2, #3
        var ingest1 = await IngestFindingAsync(detectorId, "GL-IMMUTABLE-01", "Sequential Imbalance", "High", t1, evidence1);
        var issueId = ingest1.Results[0].IssueId;

        var originalOcc1 = await _factory.QuerySingleOrDefaultSqlAsync<OccurrenceSnapshotRow>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId AND OccurrenceNumber = 1;",
            new { IssueId = issueId });
        originalOcc1.Should().NotBeNull();

        var ingest2 = await IngestFindingAsync(detectorId, "GL-IMMUTABLE-01", "Sequential Imbalance", "High", t2, evidence2);
        var ingest3 = await IngestFindingAsync(detectorId, "GL-IMMUTABLE-01", "Sequential Imbalance", "High", t3, evidence3);

        ingest2.Results[0].OccurrenceNumber.Should().Be(2);
        ingest3.Results[0].OccurrenceNumber.Should().Be(3);

        // Assert 1: Database snapshots #1, #2, #3 remain intact and prior #1 was never mutated
        var allOccurrences = (await _factory.QuerySqlAsync<OccurrenceSnapshotRow>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC;",
            new { IssueId = issueId })).ToList();

        allOccurrences.Should().HaveCount(3);

        allOccurrences[0].Should().BeEquivalentTo(originalOcc1);
        allOccurrences[0].InspectionRunId.Should().Be(ingest1.InspectionRunId);
        allOccurrences[0].DebitTotal.Should().Be(1_000_000m);
        allOccurrences[0].CreditTotal.Should().Be(900_000m);
        allOccurrences[0].VarianceDelta.Should().Be(100_000m);
        allOccurrences[0].RecordKeys.Should().Be("JRN-1001");
        allOccurrences[0].TraceString.Should().Be("Snapshot #1: initial IDR 100k imbalance");

        allOccurrences[1].OccurrenceNumber.Should().Be(2);
        allOccurrences[1].InspectionRunId.Should().Be(ingest2.InspectionRunId);
        allOccurrences[1].DebitTotal.Should().Be(2_500_000m);
        allOccurrences[1].VarianceDelta.Should().Be(500_000m);
        allOccurrences[1].RecordKeys.Should().Be("JRN-1001;JRN-1002");
        allOccurrences[1].TraceString.Should().Be("Snapshot #2: escalated IDR 500k imbalance");

        allOccurrences[2].OccurrenceNumber.Should().Be(3);
        allOccurrences[2].InspectionRunId.Should().Be(ingest3.InspectionRunId);
        allOccurrences[2].DebitTotal.Should().Be(3_900_000m);
        allOccurrences[2].VarianceDelta.Should().Be(900_000m);
        allOccurrences[2].RecordKeys.Should().Be("JRN-1001;JRN-1002;JRN-1003");
        allOccurrences[2].TraceString.Should().Be("Snapshot #3: persisted IDR 900k imbalance");

        // Assert 2: Read-model API returns all 3 historical evidence snapshots intact
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var detail = await authClient.GetFromJsonAsync<IssueDetailDto>($"/api/issues/{issueId}", JsonOptions);
        detail.Should().NotBeNull();
        detail!.Occurrences.Should().HaveCount(3);
        detail.Occurrences.Select(o => o.OccurrenceNumber).Should().Equal(3, 2, 1);
        detail.Occurrences.Single(o => o.OccurrenceNumber == 1).VarianceDelta.Should().Be(100_000m);
        detail.Occurrences.Single(o => o.OccurrenceNumber == 2).VarianceDelta.Should().Be(500_000m);
        detail.Occurrences.Single(o => o.OccurrenceNumber == 3).VarianceDelta.Should().Be(900_000m);
    }

    [Fact]
    public async Task BR_PRIMA_010_SilenceDetectionCadence_TransitionsToSilentAfter2xAndAddsOccurrencesAtCadenceMultiples()
    {
        // Arrange: ExpectedCadenceSeconds = 60 => silence threshold = 2.0 * 60 = 120 seconds
        var now = DateTime.UtcNow;
        var detectorId = await SeedConfiguredDetectorAsync(
            "BR010-SilenceCadence-Detector",
            expectedCadenceSeconds: 60,
            lastHealthyHeartbeatUtc: now.AddSeconds(-100), // 100s < 120s (below 2.0x threshold)
            scope: "DataIntegrity",
            targetModule: "Pharmacy");

        var worker = CreateSupervisorWorker();

        // Act 1: Before 2.0 * ExpectedCadenceSeconds -> remains Healthy, zero SILENT issues
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        var healthBefore = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = detectorId });
        healthBefore.Should().Be("Healthy");

        var issueCountBefore = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.Issues WHERE CorrelationKey = @Key;",
            new { Key = $"SILENT:{detectorId}" });
        issueCountBefore.Should().Be(0);

        // Arrange prolonged silence baseline 10 minutes ago (past SQL SYSUTCDATETIME - 120s cutoff)
        var baseline = now.AddMinutes(-10);
        await _factory.ExecuteSqlAsync(
            "UPDATE dbo.Detectors SET LastHealthyHeartbeatUtc = @Baseline, LastRunAtUtc = @Baseline WHERE Id = @Id;",
            new { Baseline = baseline, Id = detectorId });

        // Act 2: Tick at 2.17x cadence (130s > 120s) -> transitions to Silent, creates Occurrence #1
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: baseline.AddSeconds(130));

        var healthAfter2x = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = detectorId });
        healthAfter2x.Should().Be("Silent");

        var issueAfter2x = await GetIssueByCorrelationKeyAsync($"SILENT:{detectorId}");
        issueAfter2x.Should().NotBeNull();
        issueAfter2x!.OccurrenceCount.Should().Be(1);

        // Act 3: Prolonged silence at 3.17x cadence (190s) -> adds Occurrence #2
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: baseline.AddSeconds(190));

        // Act 4: Prolonged silence at 4.17x cadence (250s) -> adds Occurrence #3
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: baseline.AddSeconds(250));

        // Assert: OccurrenceCount = 3 with occurrences 1, 2, 3 and recurrence logs at 3x and 4x
        var issueAfter4x = await GetIssueByIdAsync(issueAfter2x.Id);
        issueAfter4x!.OccurrenceCount.Should().Be(3);

        var occurrenceNumbers = (await _factory.QuerySqlAsync<int>(
            "SELECT OccurrenceNumber FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC;",
            new { IssueId = issueAfter2x.Id })).ToList();
        occurrenceNumbers.Should().Equal(1, 2, 3);

        var recurrenceLogs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'RecurrenceObserved' ORDER BY TimestampUtc ASC;",
            new { IssueId = issueAfter2x.Id })).ToList();
        recurrenceLogs.Should().HaveCount(2);
        recurrenceLogs[0].Summary.Should().Contain("3x");
        recurrenceLogs[1].Summary.Should().Contain("4x");
    }
}
