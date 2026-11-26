using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
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
public sealed class PilotSimrsFinancialProbesTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IReadOnlySimrsConnectionFactory _simrsConnectionFactory;

    public PilotSimrsFinancialProbesTests(PrIMAApiFactory factory)
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
    public void JournalBalanceDetector_DeclaresExpectedManifest()
    {
        var probe = new JournalBalanceDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("Journal Balance Detector");
        probe.Manifest.TargetModule.Should().Be("Accounting");
        probe.Manifest.Scope.Should().Be("DataIntegrity");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(300);
        probe.Manifest.WebhookEndpointKey.Should().Be("journal-balance");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void DuplicateReceivableDetector_DeclaresExpectedManifest()
    {
        var probe = new DuplicateReceivableDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("Duplicate Receivable Detector");
        probe.Manifest.TargetModule.Should().Be("Billing");
        probe.Manifest.Scope.Should().Be("DataIntegrity");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(300);
        probe.Manifest.WebhookEndpointKey.Should().Be("duplicate-receivable");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void UnprocessedBillingPipelineDetector_DeclaresExpectedManifest()
    {
        var probe = new UnprocessedBillingPipelineDetector(_simrsConnectionFactory);

        probe.Manifest.Name.Should().Be("Unprocessed Billing Pipeline Detector");
        probe.Manifest.TargetModule.Should().Be("Billing");
        probe.Manifest.Scope.Should().Be("ProcessIntegrity");
        probe.Manifest.ExpectedCadenceSeconds.Should().Be(300);
        probe.Manifest.WebhookEndpointKey.Should().Be("unprocessed-billing");
        probe.Manifest.Version.Should().Be("1.0.0");
    }

    // -------------------------------------------------------------------------
    // 2. Anomaly Detection Against Seeded Sandbox Fixtures (DEC-007)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JournalBalanceDetector_DiscoversSeededUnbalancedBatch_PopulatesFinancialEvidence()
    {
        // Arrange
        var probe = new JournalBalanceDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded anomaly batch JRN-202611-ERR01
        var finding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "JRN-202611-ERR01");
        finding.Should().NotBeNull();
        finding!.Title.Should().Contain("JRN-202611-ERR01");
        finding.BaseSeverity.Should().Be("High");

        var evidence = finding.Evidence;
        evidence.Module.Should().Be("Accounting");
        evidence.SourceTable.Should().Be("simrs.acc_journal_entries");
        evidence.RecordKeys.Should().Contain("JRN-202611-ERR01");
        evidence.DebitTotal.Should().Be(1250000.00m);
        evidence.CreditTotal.Should().Be(1200000.00m);
        evidence.VarianceDelta.Should().Be(50000.00m);
        evidence.Trace.Should().Contain("1,250,000");

        // Must NOT flag balanced batch JRN-202611-0001
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "JRN-202611-0001");
    }

    [Fact]
    public async Task DuplicateReceivableDetector_DiscoversSeededDuplicateReceivables_PopulatesFinancialEvidence()
    {
        // Arrange
        var probe = new DuplicateReceivableDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded anomaly duplicate receivable TRX-AR-202611-DUP01
        var finding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "TRX-AR-202611-DUP01");
        finding.Should().NotBeNull();
        finding!.Title.Should().Contain("TRX-AR-202611-DUP01");
        finding.BaseSeverity.Should().Be("High");

        var evidence = finding.Evidence;
        evidence.Module.Should().Be("Billing");
        evidence.SourceTable.Should().Be("simrs.ar_receivables");
        evidence.RecordKeys.Should().Contain("TRX-AR-202611-DUP01");
        evidence.DebitTotal.Should().Be(3000000.00m); // 2 * 1,500,000
        evidence.CreditTotal.Should().Be(1500000.00m); // MAX single expected
        evidence.VarianceDelta.Should().Be(1500000.00m); // Duplicate excess
        evidence.Trace.Should().Contain("Found 2 active receivable records");

        // Must NOT flag single active receivable TRX-AR-202611-001
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "TRX-AR-202611-001");
    }

    [Fact]
    public async Task UnprocessedBillingPipelineDetector_DiscoversSeededUnprocessedBill_PopulatesFinancialEvidence()
    {
        // Arrange
        var probe = new UnprocessedBillingPipelineDetector(_simrsConnectionFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.FindingDiscovered);
        report.Findings.Should().NotBeEmpty();

        // Must find seeded anomaly bill BILL-202611-NOJRN01
        var finding = report.Findings.FirstOrDefault(f => f.TargetEntityReference == "BILL-202611-NOJRN01");
        finding.Should().NotBeNull();
        finding!.Title.Should().Contain("BILL-202611-NOJRN01");
        finding.BaseSeverity.Should().Be("High");

        var evidence = finding.Evidence;
        evidence.Module.Should().Be("Billing");
        evidence.SourceTable.Should().Be("simrs.trx_patient_bills");
        evidence.RecordKeys.Should().Contain("BILL-202611-NOJRN01");
        evidence.DebitTotal.Should().Be(890000.00m);
        evidence.CreditTotal.Should().Be(0.00m);
        evidence.VarianceDelta.Should().Be(890000.00m);
        evidence.Trace.Should().Contain("890,000");

        // Must NOT flag settled bill BILL-202611-001 because it has matching journal entries
        report.Findings.Should().NotContain(f => f.TargetEntityReference == "BILL-202611-001");
    }

    // -------------------------------------------------------------------------
    // 3. Clean Run Reporting (Zero Anomalies)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JournalBalanceDetector_WhenAllBatchesBalanced_ReturnsPassedReport()
    {
        // Arrange: Fake connection factory returning zero unbalanced batches
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new JournalBalanceDetector(fakeFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("All journal batches in the past 3 days are balanced");
    }

    [Fact]
    public async Task DuplicateReceivableDetector_WhenNoDuplicates_ReturnsPassedReport()
    {
        // Arrange
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new DuplicateReceivableDetector(fakeFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("No duplicate active receivables");
    }

    [Fact]
    public async Task UnprocessedBillingPipelineDetector_WhenAllBillsJournalized_ReturnsPassedReport()
    {
        // Arrange
        var fakeFactory = new FakeReadOnlySimrsConnectionFactory();
        var probe = new UnprocessedBillingPipelineDetector(fakeFactory);

        // Act
        var report = await probe.ExecuteInspectionAsync();

        // Assert
        report.Outcome.Should().Be(TelemetryOutcome.Passed);
        report.Findings.Should().BeEmpty();
        report.TraceString.Should().Contain("All settled patient bills have matching auto-journal entries");
    }

    // -------------------------------------------------------------------------
    // 4. End-to-End Ingestion Integration & Evidence Persistence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EndToEnd_JournalBalanceDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithEvidence()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new JournalBalanceDetector(_simrsConnectionFactory).Manifest);

        var probe = new JournalBalanceDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var expectedCorrelationKey = $"{detectorId:D}:JRN-202611-ERR01";
        response.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = response.Results[0].IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("JRN-202611-ERR01");
        ((string)issue.Scope).Should().Be("DataIntegrity");
        ((string)issue.CurrentSeverity).Should().Be("High");

        // Verify Occurrence financial evidence
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("Accounting");
        ((string)occurrence.SourceTable).Should().Be("simrs.acc_journal_entries");
        ((decimal)occurrence.DebitTotal).Should().Be(1250000.00m);
        ((decimal)occurrence.CreditTotal).Should().Be(1200000.00m);
        ((decimal)occurrence.VarianceDelta).Should().Be(50000.00m);
    }

    [Fact]
    public async Task EndToEnd_DuplicateReceivableDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithEvidence()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new DuplicateReceivableDetector(_simrsConnectionFactory).Manifest);

        var probe = new DuplicateReceivableDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var expectedCorrelationKey = $"{detectorId:D}:TRX-AR-202611-DUP01";
        response.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = response.Results[0].IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("TRX-AR-202611-DUP01");
        ((string)issue.Scope).Should().Be("DataIntegrity");
        ((string)issue.CurrentSeverity).Should().Be("High");

        // Verify Occurrence financial evidence
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("Billing");
        ((string)occurrence.SourceTable).Should().Be("simrs.ar_receivables");
        ((decimal)occurrence.DebitTotal).Should().Be(3000000.00m);
        ((decimal)occurrence.CreditTotal).Should().Be(1500000.00m);
        ((decimal)occurrence.VarianceDelta).Should().Be(1500000.00m);
    }

    [Fact]
    public async Task EndToEnd_UnprocessedBillingPipelineDetector_IngestsTelemetry_CreatesIssueAndOccurrenceWithEvidence()
    {
        // Arrange: Enroll detector via handshake
        var (detectorId, apiKey) = await EnrollProbeAsync(new UnprocessedBillingPipelineDetector(_simrsConnectionFactory).Manifest);

        var probe = new UnprocessedBillingPipelineDetector(_simrsConnectionFactory);
        var report = await probe.ExecuteInspectionAsync();

        var ingestionClient = new TelemetryIngestionClient(_client, Options.Create(new DetectorSdkOptions()), NullLogger<TelemetryIngestionClient>.Instance);

        // Act: Ingest telemetry
        var response = await ingestionClient.IngestAsync(detectorId, apiKey, report, triggerSource: "Scheduled");

        // Assert: Ingestion succeeded
        response.Should().NotBeNull();
        response!.InspectionRunId.Should().NotBeEmpty();
        response.Results.Should().NotBeEmpty();

        var expectedCorrelationKey = $"{detectorId:D}:BILL-202611-NOJRN01";
        response.Results[0].CorrelationKey.Should().Be(expectedCorrelationKey);

        // Verify Issue and Occurrence in DB
        var issue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = response.Results[0].IssueId });

        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be(expectedCorrelationKey);
        ((string)issue.Title).Should().Contain("BILL-202611-NOJRN01");
        ((string)issue.Scope).Should().Be("ProcessIntegrity");
        ((string)issue.CurrentSeverity).Should().Be("High");

        // Verify Occurrence financial evidence
        var occurrence = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });

        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("Billing");
        ((string)occurrence.SourceTable).Should().Be("simrs.trx_patient_bills");
        ((decimal)occurrence.DebitTotal).Should().Be(890000.00m);
        ((decimal)occurrence.CreditTotal).Should().Be(0.00m);
        ((decimal)occurrence.VarianceDelta).Should().Be(890000.00m);
    }

    // -------------------------------------------------------------------------
    // 5. Embedded Webhook Execution for Financial Probes
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("journal-balance", "JRN-202611-ERR01", 50000.00)]
    [InlineData("duplicate-receivable", "TRX-AR-202611-DUP01", 1500000.00)]
    [InlineData("unprocessed-billing", "BILL-202611-NOJRN01", 890000.00)]
    public async Task EmbeddedWebhookListener_ExecutesPilotProbes_ReturnsFindingsSynchronously(
        string webhookKey, string expectedEntityRef, decimal expectedVariance)
    {
        // Arrange
        IDetectorProbe probe = webhookKey switch
        {
            "journal-balance" => new JournalBalanceDetector(_simrsConnectionFactory),
            "duplicate-receivable" => new DuplicateReceivableDetector(_simrsConnectionFactory),
            "unprocessed-billing" => new UnprocessedBillingPipelineDetector(_simrsConnectionFactory),
            _ => throw new ArgumentException($"Unknown webhook key: {webhookKey}")
        };

        var (detectorId, apiKey) = await EnrollProbeAsync(probe.Manifest);

        var tempCredPath = Path.Combine(Path.GetTempPath(), $"prima_cred_{Guid.NewGuid():N}.json");
        var cacheContent = $$"""
        {
          "detectorId": "{{detectorId}}",
          "apiKey": "{{apiKey}}",
          "serverUrl": "http://localhost",
          "detectorName": "{{probe.Manifest.Name}}",
          "probes": {
            "{{probe.Manifest.Name}}": {
              "detectorId": "{{detectorId}}",
              "apiKey": "{{apiKey}}",
              "serverUrl": "http://localhost",
              "detectorName": "{{probe.Manifest.Name}}"
            }
          }
        }
        """;
        await File.WriteAllTextAsync(tempCredPath, cacheContent);

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton(_simrsConnectionFactory);
                services.AddSingleton<IDetectorProbe>(probe);

                services.AddSingleton<IBootstrapHandshakeService>(_ => new BootstrapHandshakeService(
                    _client,
                    Options.Create(new DetectorSdkOptions
                    {
                        CredentialFilePath = tempCredPath,
                        ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost"
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
        matched.Evidence.VarianceDelta.Should().Be(expectedVariance);

        // Clean up temporary credential file
        if (File.Exists(tempCredPath))
        {
            try { File.Delete(tempCredPath); } catch { }
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
        // 1. Generate enrollment token
        var authToken = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

        var tokenRes = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", new GenerateEnrollmentTokenRequest("Probe Enrollment"));
        tokenRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokenBody = await tokenRes.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        tokenBody.Should().NotBeNull();

        // 2. Perform handshake
        var handshakeReq = new DetectorHandshakeRequest(
            EnrollmentToken: tokenBody!.Token,
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
