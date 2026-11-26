using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrIMA.Api.Tests.Fixtures;
using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class Stage5DetectorSdkCoreTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;

    public Stage5DetectorSdkCoreTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string HashKey(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    public void DetectorManifest_InitializesPropertiesAndAutoGeneratesWebhookKey()
    {
        // Act
        var manifest = new DetectorManifest(
            name: "Hospital Journal Balance Probe",
            targetModule: "Accounting",
            scope: "DataIntegrity",
            expectedCadenceSeconds: 60,
            description: "Detects unbalanced journal entries",
            version: "1.2.0");

        // Assert
        manifest.Name.Should().Be("Hospital Journal Balance Probe");
        manifest.TargetModule.Should().Be("Accounting");
        manifest.Scope.Should().Be("DataIntegrity");
        manifest.ExpectedCadenceSeconds.Should().Be(60);
        manifest.Description.Should().Be("Detects unbalanced journal entries");
        manifest.Version.Should().Be("1.2.0");
        manifest.WebhookEndpointKey.Should().Be("hospital-journal-balance-probe");
    }

    [Fact]
    public void TelemetryReport_FactoryMethods_SetCorrectOutcomesAndPayloads()
    {
        // Act 1: Passed
        var passed = TelemetryReport.Passed(durationMs: 42, traceString: "Clean run");
        passed.Outcome.Should().Be(TelemetryOutcome.Passed);
        passed.Findings.Should().BeEmpty();
        passed.ExecutionDurationMs.Should().Be(42);
        passed.TraceString.Should().Be("Clean run");
        passed.ErrorMessage.Should().BeNull();

        // Act 2: FindingDiscovered
        var findings = new[]
        {
            new FindingItem(
                targetEntityReference: "BATCH-901",
                title: "Unbalanced Ledger Batch",
                baseSeverity: "High",
                evidence: new FindingEvidenceItem("Accounting", debitTotal: 100m, creditTotal: 80m, varianceDelta: 20m))
        };
        var discovered = TelemetryReport.FindingDiscovered(findings, durationMs: 120);
        discovered.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        discovered.Findings.Should().HaveCount(1);
        discovered.Findings[0].TargetEntityReference.Should().Be("BATCH-901");
        discovered.ExecutionDurationMs.Should().Be(120);

        // Act 3: ExecutionFailed
        var failed = TelemetryReport.ExecutionFailed("SQL query timeout after 5000ms", "stack trace info", durationMs: 5005);
        failed.Outcome.Should().Be(TelemetryOutcome.ExecutionFailed);
        failed.Findings.Should().BeEmpty();
        failed.ErrorMessage.Should().Be("SQL query timeout after 5000ms");
        failed.TraceString.Should().Be("stack trace info");
        failed.ExecutionDurationMs.Should().Be(5005);
    }

    [Fact]
    public async Task ProbeExecutor_WhenProbePasses_ReturnsPassedTelemetryReport()
    {
        // Arrange
        var mockProbe = new MockDetectorProbe(
            name: "Test Pass Probe",
            executionDelegate: ct => Task.FromResult(TelemetryReport.Passed()));
        var executor = new ProbeExecutor(NullLogger<ProbeExecutor>.Instance);

        // Act
        var report = await executor.ExecuteAsync(mockProbe);

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.ExecutionDurationMs.Should().BeGreaterThanOrEqualTo(0);
        report.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ProbeExecutor_WhenProbeThrowsException_TrapsToExecutionFailed()
    {
        // Arrange: Probe throws unexpected exception (e.g. database error)
        var mockProbe = new MockDetectorProbe(
            name: "Failing Test Probe",
            executionDelegate: ct => throw new InvalidOperationException("Simulated SQL server unreachable."));
        var executor = new ProbeExecutor(NullLogger<ProbeExecutor>.Instance);

        // Act: Must not throw, must trap to ExecutionFailed
        var report = await executor.ExecuteAsync(mockProbe);

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.ExecutionFailed);
        report.ErrorMessage.Should().Be("Simulated SQL server unreachable.");
        report.TraceString.Should().Contain("InvalidOperationException");
        report.ExecutionDurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProbeExecutor_WhenProbeIsCancelled_TrapsToExecutionFailed()
    {
        // Arrange: CancellationToken is already cancelled
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockProbe = new MockDetectorProbe(
            name: "Cancelled Probe",
            executionDelegate: ct => Task.FromCanceled<TelemetryReport>(ct));
        var executor = new ProbeExecutor(NullLogger<ProbeExecutor>.Instance);

        // Act
        var report = await executor.ExecuteAsync(mockProbe, cts.Token);

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.ExecutionFailed);
        report.ErrorMessage.Should().Contain("timed out or was cancelled");
    }

    [Fact]
    public async Task TelemetryIngestionClient_IngestPassedReport_SuccessfullySubmitsWithApiKey()
    {
        // Arrange
        const string rawApiKey = "prm_key_sdk_test_passed_run_01";
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Passed Run Test Detector",
            apiKeyHash: HashKey(rawApiKey),
            expectedCadenceSeconds: 60);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var report = TelemetryReport.Passed(durationMs: 35, traceString: "Clean run verification");

        // Act
        var response = await ingestionClient.IngestAsync(
            detectorId,
            rawApiKey,
            report,
            triggerSource: "Scheduled");

        // Assert
        response.Should().NotBeNull();
        response!.DetectorId.Should().Be(detectorId);
        response.Outcome.Should().Be("Passed");
        response.InspectionRunId.Should().NotBeEmpty();

        // Verify database persistence
        var run = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT Id, Outcome, DurationMs FROM dbo.InspectionRuns WHERE Id = @Id",
            new { Id = response.InspectionRunId });
        ((object?)run).Should().NotBeNull();
        ((string?)run?.Outcome).Should().Be("Passed");
    }

    [Fact]
    public async Task TelemetryIngestionClient_IngestFindingsReport_CreatesIssueAndOccurrence()
    {
        // Arrange
        const string rawApiKey = "prm_key_sdk_findings_run_01";
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Findings Test Detector",
            apiKeyHash: HashKey(rawApiKey),
            expectedCadenceSeconds: 60);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var findings = new[]
        {
            new FindingItem(
                targetEntityReference: "INV-2026-00192",
                title: "Duplicate Patient Invoice Record",
                baseSeverity: "High",
                evidence: new FindingEvidenceItem("Billing", sourceTable: "simrs.ar_receivables", debitTotal: 500000m, creditTotal: 500000m, varianceDelta: 500000m))
        };
        var report = TelemetryReport.FindingDiscovered(findings, durationMs: 85);

        // Act
        var response = await ingestionClient.IngestAsync(
            detectorId,
            rawApiKey,
            report,
            triggerSource: "Scheduled");

        // Assert
        response.Should().NotBeNull();
        response!.Outcome.Should().Be("FindingDiscovered");
        response.NewIssuesCreated.Should().Be(1);
        response.Results.Should().HaveCount(1);
        response.Results[0].CorrelationKey.Should().Be($"{detectorId}:INV-2026-00192");
    }

    [Fact]
    public async Task TelemetryIngestionClient_IngestExecutionFailed_TruncatesExcessiveErrorMessageSafely()
    {
        // Arrange
        const string rawApiKey = "prm_key_sdk_failure_truncation_01";
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Failure Truncation Test Detector",
            apiKeyHash: HashKey(rawApiKey),
            expectedCadenceSeconds: 60);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        // Message longer than 1000 characters
        var excessiveErrorMessage = new string('E', 1500);
        var report = TelemetryReport.ExecutionFailed(excessiveErrorMessage, traceString: "Stack trace text");

        // Act
        var response = await ingestionClient.IngestAsync(
            detectorId,
            rawApiKey,
            report,
            triggerSource: "Scheduled");

        // Assert
        response.Should().NotBeNull();
        response!.Outcome.Should().Be("ExecutionFailed");

        var failureReason = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT FailureReason FROM dbo.InspectionRuns WHERE Id = @Id",
            new { Id = response.InspectionRunId });
        failureReason.Should().NotBeNull();
        failureReason!.Length.Should().BeLessThanOrEqualTo(1000);
        failureReason.Should().EndWith("...");
    }

    [Fact]
    public async Task TelemetryIngestionClient_WhenCadenceDriftOccurs_SurfacesWarningsInResponse()
    {
        // Arrange: Last run was 200 seconds ago, expected cadence is 60 seconds (> +50% drift)
        const string rawApiKey = "prm_key_sdk_cadence_drift_01";
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Cadence Drift Test Detector",
            apiKeyHash: HashKey(rawApiKey),
            expectedCadenceSeconds: 60,
            lastRunAtUtc: DateTime.UtcNow.AddSeconds(-200));

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var report = TelemetryReport.Passed(durationMs: 15);

        // Act
        var response = await ingestionClient.IngestAsync(
            detectorId,
            rawApiKey,
            report,
            triggerSource: "Scheduled");

        // Assert
        response.Should().NotBeNull();
        response!.Warnings.Should().NotBeEmpty();
        response.Warnings[0].Should().Contain("Execution cadence drifted");
    }

    [Fact]
    public async Task TelemetryIngestionClient_WhenInvalidApiKeyProvided_ThrowsHttpRequestExceptionWithoutRetry()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Invalid Key Probe",
            apiKeyHash: HashKey("correct-key"),
            expectedCadenceSeconds: 60);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 3 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var report = TelemetryReport.Passed();

        // Act
        var act = () => ingestionClient.IngestAsync(detectorId, "wrong-key", report);

        // Assert: 401 Unauthorized should immediately throw and not loop retries
        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DetectorRunner_RunAndIngestAsync_AutomaticallyTrapsProbeExceptionAndSubmitsFailureToPrIMA()
    {
        // Arrange (BR-PRIMA-005): If a probe throws an unhandled exception, runner traps into ExecutionFailed and submits to PrIMA
        const string rawApiKey = "prm_key_sdk_auto_trapping_01";
        var detectorId = await _factory.SeedDetectorAsync(
            name: "SDK Autonomous Trapping Detector",
            apiKeyHash: HashKey(rawApiKey),
            expectedCadenceSeconds: 60);

        var failingProbe = new MockDetectorProbe(
            name: "Crashing Database Probe",
            executionDelegate: ct => throw new TimeoutException("Database connection timeout after 5000ms."));

        var executor = new ProbeExecutor(NullLogger<ProbeExecutor>.Instance);
        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var runner = new DetectorRunner(
            executor,
            ingestionClient,
            Options.Create(new DetectorSdkOptions()),
            NullLogger<DetectorRunner>.Instance);

        // Act
        var result = await runner.RunAndIngestAsync(
            failingProbe,
            detectorId,
            rawApiKey,
            triggerSource: "Scheduled");

        // Assert
        result.Report.Outcome.Should().Be(TelemetryOutcome.ExecutionFailed);
        result.Report.ErrorMessage.Should().Contain("Database connection timeout");
        result.IngestionResponse.Should().NotBeNull();
        result.IngestionResponse!.Outcome.Should().Be("ExecutionFailed");

        // Verify in database that an ExecutionFailed inspection run was persisted
        var dbRunOutcome = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT Outcome FROM dbo.InspectionRuns WHERE Id = @Id",
            new { Id = result.IngestionResponse.InspectionRunId });
        dbRunOutcome.Should().Be("ExecutionFailed");

        var dbFailureReason = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT FailureReason FROM dbo.InspectionRuns WHERE Id = @Id",
            new { Id = result.IngestionResponse.InspectionRunId });
        dbFailureReason.Should().Contain("Database connection timeout");
    }

    private sealed class MockDetectorProbe : IDetectorProbe
    {
        private readonly Func<CancellationToken, Task<TelemetryReport>> _executionDelegate;

        public MockDetectorProbe(
            string name,
            Func<CancellationToken, Task<TelemetryReport>> executionDelegate,
            string module = "Accounting",
            string scope = "DataIntegrity",
            int cadence = 60)
        {
            Manifest = new DetectorManifest(name, module, scope, cadence);
            _executionDelegate = executionDelegate;
        }

        public DetectorManifest Manifest { get; }

        public Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default) =>
            _executionDelegate(ct);
    }
}
