using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Features.Detectors.RegisterDetector;
using PrIMA.Api.Features.Detectors.TriggerManualRun;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues.GetDashboardFeed;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Features.Issues.TriggerVerificationRun;
using PrIMA.Api.Infrastructure.Webhooks;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class EndToEndAutonomousSupervisionSmokeTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _baseFactory;
    private readonly ConfigurableWebhookClient _webhookClient;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndToEndAutonomousSupervisionSmokeTests(PrIMAApiFactory factory)
    {
        _baseFactory = factory;
        _webhookClient = new ConfigurableWebhookClient();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IDetectorWebhookClient>(_ => _webhookClient);
            });
        });

        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => _baseFactory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _baseFactory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private MonitoringSupervisorWorker CreateWorker(int slaTimeoutMinutes = 30)
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

    private static HttpRequestMessage CreateTelemetryMessage(IngestTelemetryRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Detector-API-Key", PrIMAApiFactory.TestApiKey);
        return message;
    }

    [Fact]
    public async Task EndToEnd_AutonomousSupervisionAndWebhookVerification_CompleteLoop_Succeeds()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // =========================================================================
        // STEP 1: Register new detector via POST /api/detectors with webhook URL
        // =========================================================================
        var registerRequest = new RegisterDetectorRequest(
            Name: "Autonomous Billing Audit Probe",
            Description: "Supervised autonomous billing and ledger audit probe",
            Scope: "DataIntegrity",
            TargetModule: "Billing",
            ExpectedCadenceSeconds: 60,
            WebhookUrl: "https://probes.internal/api/billing/audit");

        var regResp = await authClient.PostAsJsonAsync("/api/detectors", registerRequest);
        regResp.StatusCode.Should().Be(HttpStatusCode.Created, "Detector registration should succeed");

        var createdDetector = await regResp.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        createdDetector.Should().NotBeNull();
        var detectorId = createdDetector!.Id;
        detectorId.Should().NotBeEmpty();
        createdDetector.Name.Should().Be("Autonomous Billing Audit Probe");
        createdDetector.AdminStatus.Should().Be("Active");
        createdDetector.HealthStatus.Should().Be("Healthy");
        createdDetector.WebhookUrl.Should().Be("https://probes.internal/api/billing/audit");

        // =========================================================================
        // STEP 2: Execute manual run via POST /api/detectors/{id}/trigger-run
        //         Verify run appears in GET /api/detectors/{id}/runs
        // =========================================================================
        _webhookClient.CustomHandler = (_, _, _, _, _) => Task.FromResult(Result<WebhookExecutionResult>.Success(
            new WebhookExecutionResult(
                Outcome: "Passed",
                DurationMs: 125,
                FailureReason: null,
                Findings: Array.Empty<WebhookFindingDto>())));

        var triggerResp = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        triggerResp.StatusCode.Should().Be(HttpStatusCode.OK, "Manual probe run should succeed");

        var manualRunResult = await triggerResp.Content.ReadFromJsonAsync<InspectionRunResultDto>(JsonOptions);
        manualRunResult.Should().NotBeNull();
        manualRunResult!.Outcome.Should().Be("Passed");
        manualRunResult.DurationMs.Should().Be(125);
        manualRunResult.RunId.Should().NotBeEmpty();

        var runsResp = await authClient.GetAsync($"/api/detectors/{detectorId}/runs?limit=10");
        runsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var runLog = await runsResp.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        runLog.Should().NotBeNull();
        runLog!.TotalCount.Should().BeGreaterOrEqualTo(1);
        var loggedRun = runLog.Runs.Should().ContainSingle(r => r.Id == manualRunResult.RunId).Which;
        loggedRun.TriggerSource.Should().Be("Manual");
        loggedRun.Outcome.Should().Be("Passed");
        loggedRun.DurationMs.Should().Be(125);

        // =========================================================================
        // STEP 3: Simulate silence breach past 2× cadence (60s * 2 = 120s)
        //         Execute supervisor pass -> Detector transitions to Silent,
        //         synthetic SILENT:{id} issue created
        // =========================================================================
        var baselineNow = DateTime.UtcNow;
        // Set LastHealthyHeartbeatUtc to 150 seconds ago (> 120s silence threshold)
        await _baseFactory.ExecuteSqlAsync(
            """
            UPDATE dbo.Detectors
            SET LastHealthyHeartbeatUtc = @T, LastRunAtUtc = @T
            WHERE Id = @Id;
            """,
            new { T = baselineNow.AddSeconds(-150), Id = detectorId });

        var supervisorWorker = CreateWorker(slaTimeoutMinutes: 30);
        await supervisorWorker.ExecuteSupervisionCycleAsync(simulatedUtcNow: baselineNow);

        // Verify detector health transitioned to Silent
        var detAfterSilence = await authClient.GetFromJsonAsync<DetectorDetailDto>($"/api/detectors/{detectorId}", JsonOptions);
        detAfterSilence!.HealthStatus.Should().Be("Silent");

        // Verify synthetic SILENT:{id} issue created in database
        var silentIssue = await _baseFactory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key",
            new { Key = $"SILENT:{detectorId}" });
        ((object?)silentIssue).Should().NotBeNull();
        Guid silentIssueId = (Guid)silentIssue!.Id;
        ((string)silentIssue.Scope).Should().Be("Infrastructure");
        ((string)silentIssue.BaseSeverity).Should().Be("High");
        ((string)silentIssue.CurrentSeverity).Should().Be("High");
        ((string)silentIssue.Status).Should().Be("Open");
        ((int)silentIssue.EscalationLevel).Should().Be(0);
        ((int)silentIssue.OccurrenceCount).Should().Be(1);

        // =========================================================================
        // STEP 4: Simulate SLA breach on unacknowledged issue
        //         Execute supervisor pass -> Severity elevates by +1 step (High -> Critical)
        //         with SeverityEscalated activity log
        // =========================================================================
        var oldCreatedTime = baselineNow.AddMinutes(-35); // Breaches 30m SLA window
        await _baseFactory.ExecuteSqlAsync(
            """
            UPDATE dbo.Issues
            SET CreatedAtUtc = @OldTime, LastOccurrenceAtUtc = @OldTime
            WHERE Id = @Id;
            """,
            new { OldTime = oldCreatedTime, Id = silentIssueId });

        await supervisorWorker.ExecuteSupervisionCycleAsync(simulatedUtcNow: baselineNow);

        // Verify issue severity escalated to Critical (+1 step)
        var escalatedIssue = await _baseFactory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT CurrentSeverity, SeverityRank, EscalationLevel FROM dbo.Issues WHERE Id = @Id",
            new { Id = silentIssueId });
        ((string)escalatedIssue!.CurrentSeverity).Should().Be("Critical");
        ((byte)escalatedIssue.SeverityRank).Should().Be(4);
        ((int)escalatedIssue.EscalationLevel).Should().Be(1);

        // Verify SeverityEscalated activity log inserted by System
        var escalationLog = await _baseFactory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'SeverityEscalated'",
            new { IssueId = silentIssueId });
        ((object?)escalationLog).Should().NotBeNull();
        ((string)escalationLog!.ActorName).Should().Be("System");
        ((string)escalationLog.Summary).Should().Contain("unacknowledged");

        // =========================================================================
        // STEP 5: Ingest clean telemetry
        //         Detector health recovers to Healthy,
        //         recovery activity log appended to open infrastructure issue (DEC-004)
        // =========================================================================
        await Task.Delay(25);
        var cleanTelemetry = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = baselineNow.AddMinutes(1),
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 80,
            Findings = new List<FindingItem>()
        };

        var cleanResp = await _client.SendAsync(CreateTelemetryMessage(cleanTelemetry));
        cleanResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify detector health recovered to Healthy
        var recoveredDetector = await authClient.GetFromJsonAsync<DetectorDetailDto>($"/api/detectors/{detectorId}", JsonOptions);
        recoveredDetector!.HealthStatus.Should().Be("Healthy");

        // Verify recovery activity log appended to SILENT issue (DEC-004)
        var recoveryLog = await _baseFactory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'DetectorRecovered'",
            new { IssueId = silentIssueId });
        ((object?)recoveryLog).Should().NotBeNull();
        ((string)recoveryLog!.ActorName).Should().Be("System");
        ((string)recoveryLog.Summary).Should().Contain("Detector resumed healthy reporting");

        // Verify infrastructure issue remains active (Open) for human EDP verification
        var silentIssueAfterRecovery = await _baseFactory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT Status FROM dbo.Issues WHERE Id = @Id",
            new { Id = silentIssueId });
        ((string)silentIssueAfterRecovery!.Status).Should().Be("Open");

        // =========================================================================
        // STEP 6: Ingest finding on Investigating issue, records remediation,
        //         triggers automated verification re-run via webhook,
        //         verifies Verification Gate unlocks on clean pass, and resolves issue
        // =========================================================================
        await Task.Delay(25);
        var anomalyRef = $"SMOKE-TX-{Guid.NewGuid():N}";
        var findingTelemetry = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = baselineNow.AddMinutes(2),
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 140,
            Findings = new List<FindingItem>
            {
                new()
                {
                    TargetEntityReference = anomalyRef,
                    Title = "Smoke Test Ledger Debit-Credit Imbalance",
                    BaseSeverity = "High",
                    Evidence = new FindingEvidenceItem
                    {
                        Module = "Billing",
                        SourceTable = "BillingTransactions",
                        RecordKeys = anomalyRef,
                        DebitTotal = 25_000_000m,
                        CreditTotal = 23_500_000m,
                        VarianceDelta = 1_500_000m,
                        Trace = "Unbalanced debit credit variance delta of IDR 1.500.000 in journal entry"
                    }
                }
            }
        };

        var findingResp = await _client.SendAsync(CreateTelemetryMessage(findingTelemetry));
        findingResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var findingDto = await findingResp.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        findingDto.Should().NotBeNull();
        findingDto!.ProcessedFindingsCount.Should().Be(1);
        Guid domainIssueId = findingDto.Results[0].IssueId;

        // Triage: Acknowledge & Start Investigation
        var ackResp = await authClient.PostAsync($"/api/issues/{domainIssueId}/acknowledge", null);
        ackResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var startResp = await authClient.PostAsync($"/api/issues/{domainIssueId}/start-investigation", null);
        startResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var investigatingDetail = await authClient.GetFromJsonAsync<IssueDetailDto>($"/api/issues/{domainIssueId}", JsonOptions);
        investigatingDetail!.Status.Should().Be("Investigating");

        // Verify Verification Gate locks resolve prior to remediation
        var prematureResolve1 = await authClient.PostAsync($"/api/issues/{domainIssueId}/resolve", null);
        prematureResolve1.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Record remediation action (M-02)
        var remRequest = new RecordRemediationRequest(
            ActionCategory: "DataCorrection",
            SummaryDescription: "Applied balancing credit voucher adjusting billing ledger variance according to SOP-FIN-02.");
        var remResp = await authClient.PostAsJsonAsync($"/api/issues/{domainIssueId}/remediation", remRequest);
        remResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Verification Gate locks resolve prior to verification
        var prematureResolve2 = await authClient.PostAsync($"/api/issues/{domainIssueId}/resolve", null);
        prematureResolve2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Trigger automated verification re-run via webhook
        _webhookClient.CustomHandler = (_, _, _, _, _) => Task.FromResult(Result<WebhookExecutionResult>.Success(
            new WebhookExecutionResult(
                Outcome: "Passed",
                DurationMs: 115,
                FailureReason: null,
                Findings: Array.Empty<WebhookFindingDto>())));

        var verRunResp = await authClient.PostAsync($"/api/issues/{domainIssueId}/verification/trigger-run", null);
        verRunResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var verResult = await verRunResp.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        verResult.Should().NotBeNull();
        verResult!.CleanPassed.Should().BeTrue();
        verResult.GateUnlocked.Should().BeTrue();
        verResult.VerificationMethod.Should().Be("AutomatedReRun");

        // Resolve issue (Verification Gate is unlocked!)
        await Task.Delay(25);
        var resolveResp = await authClient.PostAsync($"/api/issues/{domainIssueId}/resolve", null);
        resolveResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolvedDetail = await authClient.GetFromJsonAsync<IssueDetailDto>($"/api/issues/{domainIssueId}", JsonOptions);
        resolvedDetail!.Status.Should().Be("Resolved");

        // =========================================================================
        // STEP 7: Verify dashboard feed includes escalated counts
        //         and escalatedOnly filter returns expected issues
        // =========================================================================
        // 1. General active feed
        var feedResp = await authClient.GetAsync("/api/issues/dashboard-feed");
        feedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var feed = await feedResp.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        feed.Should().NotBeNull();
        feed!.Summary.EscalatedCount.Should().BeGreaterOrEqualTo(1, "Escalated SILENT issue should increment escalated count");
        feed.Items.Any(i => i.Id == domainIssueId).Should().BeFalse("Resolved domain issue must not appear in active feed");
        feed.Items.Any(i => i.Id == silentIssueId).Should().BeTrue("Escalated SILENT issue should appear in active feed");

        // 2. EscalatedOnly filtered feed
        var escalatedResp = await authClient.GetAsync("/api/issues/dashboard-feed?escalatedOnly=true");
        escalatedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var escalatedFeed = await escalatedResp.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        escalatedFeed.Should().NotBeNull();
        escalatedFeed!.Summary.EscalatedCount.Should().BeGreaterOrEqualTo(1);
        escalatedFeed.Items.Should().NotBeEmpty();
        escalatedFeed.Items.Any(i => i.Id == silentIssueId).Should().BeTrue("Escalated SILENT issue must match escalatedOnly filter");
        escalatedFeed.Items.All(i => i.CurrentSeverity == "Critical" || i.EscalationLevel > 0).Should().BeTrue("Only escalated or critical issues returned");
        escalatedFeed.Items.Any(i => i.Id == domainIssueId).Should().BeFalse("Resolved issue must never match feed");
    }

    private sealed class ConfigurableWebhookClient : IDetectorWebhookClient
    {
        public Func<Guid, string, string, Guid?, CancellationToken, Task<Result<WebhookExecutionResult>>>? CustomHandler { get; set; }

        public Task<Result<WebhookExecutionResult>> ExecuteRunAsync(
            Guid detectorId,
            string webhookUrl,
            string triggerSource,
            Guid? issueId = null,
            CancellationToken cancellationToken = default)
        {
            if (CustomHandler != null)
            {
                return CustomHandler(detectorId, webhookUrl, triggerSource, issueId, cancellationToken);
            }

            return Task.FromResult(Result<WebhookExecutionResult>.Success(new WebhookExecutionResult(
                Outcome: "Passed",
                DurationMs: 100,
                FailureReason: null,
                Findings: Array.Empty<WebhookFindingDto>())));
        }
    }
}
