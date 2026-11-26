using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Tests.Fixtures;
using PrIMA.Detectors.Probes;
using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class PilotSimrsSyncAndInfraProbesTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IReadOnlySimrsConnectionFactory _simrsConnectionFactory;

    public PilotSimrsSyncAndInfraProbesTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _simrsConnectionFactory = new ReadOnlySimrsConnectionFactory(PrIMAApiFactory.TestConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // 1. Manifest and Metadata Specifications
    // -------------------------------------------------------------------------

    [Fact]
    public void EmrSyncDetector_DeclaresExpectedManifest()
    {
        var probe = new EmrSyncDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("EMR Sync Detector");
        probe.Manifest.TargetModule.Should().Be("EMR");
        probe.Manifest.Scope.Should().Be("DataSync");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(300);
        probe.Manifest.WebhookEndpointKey.Should().Be("emr-sync");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void HfisScheduleSyncDetector_DeclaresExpectedManifest()
    {
        var probe = new HfisScheduleSyncDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("HFIS Doctor Schedule Detector");
        probe.Manifest.TargetModule.Should().Be("Outpatient");
        probe.Manifest.Scope.Should().Be("DataSync");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(300);
        probe.Manifest.WebhookEndpointKey.Should().Be("hfis-schedule-sync");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void PrintQueueSpoolerDetector_DeclaresExpectedManifest()
    {
        var probe = new PrintQueueSpoolerDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("Print Queue Spooler Detector");
        probe.Manifest.TargetModule.Should().Be("PrintSpooler");
        probe.Manifest.Scope.Should().Be("Infrastructure");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(60);
        probe.Manifest.WebhookEndpointKey.Should().Be("print-queue-spooler");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    // -------------------------------------------------------------------------
    // 2. Anomaly Detection Against Seeded Sandbox Fixtures (DEC-007)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmrSyncDetector_DiscoversSeededFailedAndStalledLogs_NullFinancialEvidence()
    {
        // Arrange
        var probe = new EmrSyncDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded failed sync log ENC-202611-FAIL01
        var failFinding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "ENC-202611-FAIL01");
        failFinding.Should().NotBeNull();
        failFinding!.Title.Should().Contain("ENC-202611-FAIL01");
        failFinding.BaseSeverity.Should().Be("Medium");

        var failEvidence = failFinding.Evidence;
        failEvidence.Module.Should().Be("EMR");
        failEvidence.SourceTable.Should().Be("simrs.emr_sync_logs");
        failEvidence.RecordKeys.Should().Contain("ENC-202611-FAIL01");
        failEvidence.DebitTotal.Should().BeNull();
        failEvidence.CreditTotal.Should().BeNull();
        failEvidence.VarianceDelta.Should().BeNull();
        failEvidence.Trace.Should().Contain("SATUSEHAT");

        // Must find seeded stalled sync log ENC-202611-STALL01
        var stallFinding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "ENC-202611-STALL01");
        stallFinding.Should().NotBeNull();
        stallFinding!.Title.Should().Contain("ENC-202611-STALL01");
        stallFinding.BaseSeverity.Should().Be("Medium");
        stallFinding.Evidence.DebitTotal.Should().BeNull();
        stallFinding.Evidence.CreditTotal.Should().BeNull();
        stallFinding.Evidence.VarianceDelta.Should().BeNull();

        // Must NOT flag healthy synced log ENC-202611-001
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "ENC-202611-001");
    }

    [Fact]
    public async Task HfisScheduleSyncDetector_DiscoversSeededScheduleMismatch_NullFinancialEvidence()
    {
        // Arrange
        var probe = new HfisScheduleSyncDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded schedule mismatch SCH-OBG-002
        var finding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "SCH-OBG-002");
        finding.Should().NotBeNull();
        finding!.Title.Should().Contain("SCH-OBG-002");
        finding.Title.Should().Contain("dr. Siti Rahma");
        finding.BaseSeverity.Should().Be("Medium");

        var evidence = finding.Evidence;
        evidence.Module.Should().Be("Outpatient");
        evidence.SourceTable.Should().Be("simrs.hfis_doctor_schedules");
        evidence.RecordKeys.Should().Contain("SCH-OBG-002");
        evidence.DebitTotal.Should().BeNull();
        evidence.CreditTotal.Should().BeNull();
        evidence.VarianceDelta.Should().BeNull();
        evidence.Trace.Should().Contain("SIMRS (09:00-13:00, Quota: 25)");
        evidence.Trace.Should().Contain("HFIS (10:00-13:00, Quota: 15)");

        // Must NOT flag healthy aligned schedule SCH-INT-001
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "SCH-INT-001");
    }

    [Fact]
    public async Task PrintQueueSpoolerDetector_DiscoversSeededOfflineSpoolerAndStalledQueue_NullFinancialEvidence()
    {
        // Arrange
        var probe = new PrintQueueSpoolerDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded stalled print queue PRN-KASIR-02 (>300s oldest job)
        var stalledFinding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "PRN-KASIR-02");
        stalledFinding.Should().NotBeNull();
        stalledFinding!.Title.Should().Contain("PRN-KASIR-02");
        stalledFinding.BaseSeverity.Should().Be("Critical");

        var stalledEvidence = stalledFinding.Evidence;
        stalledEvidence.Module.Should().Be("PrintSpooler");
        stalledEvidence.SourceTable.Should().Be("simrs.infra_print_queues");
        stalledEvidence.RecordKeys.Should().Contain("PRN-KASIR-02");
        stalledEvidence.DebitTotal.Should().BeNull();
        stalledEvidence.CreditTotal.Should().BeNull();
        stalledEvidence.VarianceDelta.Should().BeNull();
        stalledEvidence.Trace.Should().Contain("Pending Jobs: 12");
        stalledEvidence.Trace.Should().Contain("Oldest Job Age: 720s");

        // Must find seeded offline spooler PRN-IGD-01
        var offlineFinding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "PRN-IGD-01");
        offlineFinding.Should().NotBeNull();
        offlineFinding!.Title.Should().Contain("PRN-IGD-01");
        offlineFinding.BaseSeverity.Should().Be("Critical");
        offlineFinding.Evidence.DebitTotal.Should().BeNull();
        offlineFinding.Evidence.CreditTotal.Should().BeNull();
        offlineFinding.Evidence.VarianceDelta.Should().BeNull();
        offlineFinding.Evidence.Trace.Should().Contain("Spooler Status: 'Offline'");

        // Must NOT flag clean queue PRN-FARMASI-01
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "PRN-FARMASI-01");
    }

    // -------------------------------------------------------------------------
    // 3. Clean Run Reporting (Zero Anomalies)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmrSyncDetector_WhenAllTransmissionsClean_ReturnsPassedReport()
    {
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new EmrSyncDetector(fakeFactory);

        var report = await probe.ExecuteInspectionAsync();

        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("All EMR synchronization transactions are healthy and up to date");
    }

    [Fact]
    public async Task HfisScheduleSyncDetector_WhenAllSchedulesAligned_ReturnsPassedReport()
    {
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new HfisScheduleSyncDetector(fakeFactory);

        var report = await probe.ExecuteInspectionAsync();

        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("All active outpatient doctor schedules match BPJS HFIS records");
    }

    [Fact]
    public async Task PrintQueueSpoolerDetector_WhenAllSpoolersOnlineAndEmpty_ReturnsPassedReport()
    {
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new PrintQueueSpoolerDetector(fakeFactory);

        var report = await probe.ExecuteInspectionAsync();

        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("All hospital station print spoolers and queues are online");
    }

    // -------------------------------------------------------------------------
    // 4. End-to-End Ingestion Integration & Evidence Persistence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EndToEnd_EmrSyncDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithNullFinancialFields()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new EmrSyncDetector(_simrsConnectionFactory).Manifest);

        var probe = new EmrSyncDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var expectedCorrelationKey = $"{detectorId:D}:ENC-202611-FAIL01";
        response.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = response.Results[0].IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("ENC-202611-FAIL01");
        ((string)issue.Scope).Should().Be("DataSync");
        ((string)issue.CurrentSeverity).Should().Be("Medium");

        // Verify Occurrence non-financial evidence (null financial fields per PRIMA-DOMAIN.md §TelemetryEvidence)
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("EMR");
        ((string)occurrence.SourceTable).Should().Be("simrs.emr_sync_logs");
        ((object?)occurrence.DebitTotal).Should().BeNull();
        ((object?)occurrence.CreditTotal).Should().BeNull();
        ((object?)occurrence.VarianceDelta).Should().BeNull();
        ((string)occurrence.TraceString).Should().Contain("SATUSEHAT");
    }

    [Fact]
    public async Task EndToEnd_HfisScheduleSyncDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithNullFinancialFields()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new HfisScheduleSyncDetector(_simrsConnectionFactory).Manifest);

        var probe = new HfisScheduleSyncDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var expectedCorrelationKey = $"{detectorId:D}:SCH-OBG-002";
        response.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = response.Results[0].IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("SCH-OBG-002");
        ((string)issue.Scope).Should().Be("DataSync");
        ((string)issue.CurrentSeverity).Should().Be("Medium");

        // Verify Occurrence non-financial evidence
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("Outpatient");
        ((string)occurrence.SourceTable).Should().Be("simrs.hfis_doctor_schedules");
        ((object?)occurrence.DebitTotal).Should().BeNull();
        ((object?)occurrence.CreditTotal).Should().BeNull();
        ((object?)occurrence.VarianceDelta).Should().BeNull();
        ((string)occurrence.TraceString).Should().Contain("SIMRS");
        ((string)occurrence.TraceString).Should().Contain("HFIS");
    }

    [Fact]
    public async Task EndToEnd_PrintQueueSpoolerDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithNullFinancialFields()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new PrintQueueSpoolerDetector(_simrsConnectionFactory).Manifest);

        var probe = new PrintQueueSpoolerDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var matchedResult = response.Results.First(r => r.CorrelationKey.Contains("PRN-IGD-01"));
        var expectedCorrelationKey = $"{detectorId:D}:PRN-IGD-01";
        matchedResult.CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = matchedResult.IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("PRN-IGD-01");
        ((string)issue.Scope).Should().Be("Infrastructure");
        ((string)issue.CurrentSeverity).Should().Be("Critical");

        // Verify Occurrence non-financial evidence
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("PrintSpooler");
        ((string)occurrence.SourceTable).Should().Be("simrs.infra_print_queues");
        ((object?)occurrence.DebitTotal).Should().BeNull();
        ((object?)occurrence.CreditTotal).Should().BeNull();
        ((object?)occurrence.VarianceDelta).Should().BeNull();
        ((string)occurrence.TraceString).Should().Contain("Offline");
    }

    // -------------------------------------------------------------------------
    // 5. Embedded Webhook Execution for Sync & Infrastructure Probes
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("emr-sync", "ENC-202611-FAIL01")]
    [InlineData("hfis-schedule-sync", "SCH-OBG-002")]
    [InlineData("print-queue-spooler", "PRN-IGD-01")]
    public async Task EmbeddedWebhookListener_ExecutesSyncAndInfraProbes_ReturnsFindingsSynchronously(
        string webhookKey, string expectedEntityRef)
    {
        // Arrange
        var tempCredPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"prima_cred_{Guid.NewGuid():N}.json");
        var token = await GenerateEnrollmentTokenAsync("Webhook Listener Test");

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton(_simrsConnectionFactory);
                services.AddSingleton<IDetectorProbe, EmrSyncDetector>();
                services.AddSingleton<IDetectorProbe, HfisScheduleSyncDetector>();
                services.AddSingleton<IDetectorProbe, PrintQueueSpoolerDetector>();

                services.AddSingleton<IBootstrapHandshakeService>(_ => new BootstrapHandshakeService(
                    _client,
                    Options.Create(new DetectorSdkOptions
                    {
                        CredentialFilePath = tempCredPath,
                        EnrollmentToken = token,
                        ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
                    }),
                    NullLogger<BootstrapHandshakeService>.Instance));

                services.AddSingleton(new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions { MaxRetries = 1 })));
                services.AddSingleton<IProbeExecutor, ProbeExecutor>();
                services.AddScoped<IDetectorRunner, DetectorRunner>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDetectorWebhooks();
                });
            });

        using var server = new TestServer(builder);
        var testClient = server.CreateClient();

        try
        {
            // Act: POST /webhooks/{detectorKey}
            var triggerPayload = new WebhookTriggerRequest(
                DetectorId: null,
                TriggerSource: "Manual",
                IssueId: null,
                DispatchedAtUtc: DateTime.UtcNow);

            var response = await testClient.PostAsJsonAsync($"/webhooks/{webhookKey}", triggerPayload);

            var responseContent = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"webhook call failed: {responseContent}");

            var body = await response.Content.ReadFromJsonAsync<WebhookExecutionResponse>(JsonOptions);
            body.Should().NotBeNull();
            body!.Outcome.Should().Be("FindingDiscovered");
            body.Findings.Should().Contain(f => f.TargetEntityReference == expectedEntityRef);

            var matched = body.Findings.First(f => f.TargetEntityReference == expectedEntityRef);
            matched.Evidence.DebitTotal.Should().BeNull();
            matched.Evidence.CreditTotal.Should().BeNull();
            matched.Evidence.VarianceDelta.Should().BeNull();
        }
        finally
        {
            if (System.IO.File.Exists(tempCredPath))
            {
                try { System.IO.File.Delete(tempCredPath); } catch { }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helper: Enroll probe via Handshake
    // -------------------------------------------------------------------------

    private async Task<string> GenerateEnrollmentTokenAsync(string label = "Test")
    {
        var authToken = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

        var tokenRes = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", new GenerateEnrollmentTokenRequest(label));
        tokenRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokenBody = await tokenRes.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        return tokenBody!.Token;
    }

    private async Task<(Guid DetectorId, string ApiKey)> EnrollProbeAsync(DetectorManifest manifest)
    {
        var token = await GenerateEnrollmentTokenAsync("Sync Probe Enrollment");

        var handshakeReq = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: $"{manifest.Name} - {Guid.NewGuid():N}",
            TargetModule: manifest.TargetModule,
            Scope: manifest.Scope,
            ExpectedCadenceSeconds: manifest.ExpectedCadenceSeconds,
            WebhookUrl: $"http://localhost:5090/webhooks/{manifest.WebhookEndpointKey}",
            Description: manifest.Description,
            Version: manifest.Version);

        var handshakeRes = await _client.PostAsJsonAsync("/api/detectors/handshake", handshakeReq);
        handshakeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var handshakeBody = await handshakeRes.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);
        handshakeBody.Should().NotBeNull();

        return (handshakeBody!.DetectorId, handshakeBody.ApiKey);
    }

    // -------------------------------------------------------------------------
    // Fake ReadOnlySimrsConnectionFactory for Clean-Run Testing
    // -------------------------------------------------------------------------

    private sealed class FakeReadOnlySimrsConnectionFactory : IReadOnlySimrsConnectionFactory
    {
        public int CommandTimeoutSeconds => 5;
        public int LockTimeoutMs => 2000;

        public SqlConnection CreateConnection() => throw new NotImplementedException();
        public Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public SqlConnection OpenConnection() => throw new NotImplementedException();

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken ct = default)
        {
            return Task.FromResult(Enumerable.Empty<T>());
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default)
        {
            return Task.FromResult(default(T));
        }

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default)
        {
            return Task.FromResult(default(T));
        }
    }
}
