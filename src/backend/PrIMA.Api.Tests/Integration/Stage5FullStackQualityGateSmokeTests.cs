using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Tests.Fixtures;
using PrIMA.Detectors.Probes;
using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;
using Xunit;

using ApiFindingItem = PrIMA.Api.Features.Ingestion.FindingItem;
using ApiFindingEvidenceItem = PrIMA.Api.Features.Ingestion.FindingEvidenceItem;
using ApiIngestTelemetryResponse = PrIMA.Api.Features.Ingestion.IngestTelemetryResponse;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class Stage5FullStackQualityGateSmokeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IReadOnlySimrsConnectionFactory _simrsConnectionFactory;
    private readonly List<string> _tempFilesToCleanup = new();

    public Stage5FullStackQualityGateSmokeTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _simrsConnectionFactory = new ReadOnlySimrsConnectionFactory(PrIMAApiFactory.TestConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        foreach (var file in _tempFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors for temp files
            }
        }

        return Task.CompletedTask;
    }

    private string CreateTempCredentialsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fullstack_smoke_creds_{Guid.NewGuid():N}.json");
        _tempFilesToCleanup.Add(path);
        return path;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _factory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private MonitoringSupervisorWorker CreateSupervisorWorker(int retentionDays = 90)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supervisor:PollingIntervalSeconds"] = "60",
                ["Supervisor:UnacknowledgedSlaTimeoutMinutes"] = "30",
                ["Retention:InspectionRunsDays"] = retentionDays.ToString()
            })
            .Build();

        return new MonitoringSupervisorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<MonitoringSupervisorWorker>.Instance);
    }

    // -------------------------------------------------------------------------
    // 1. Comprehensive End-to-End Full-Stack Pipeline Smoke Test
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullStack_EndToEndPipeline_Handshake_TelemetryIngestion_CadenceDrift_Webhook_Health_And_RetentionPruning()
    {
        // ---------------------------------------------------------------------
        // Step 1: EDP Officer generates 24h enrollment token
        // ---------------------------------------------------------------------
        var authClient = await CreateAuthenticatedClientAsync();
        var tokenGenerateResponse = await authClient.PostAsJsonAsync(
            "/api/detectors/enrollment-tokens",
            new GenerateEnrollmentTokenRequest("FullStack Quality Gate Pilot Token"));

        tokenGenerateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokenData = await tokenGenerateResponse.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        tokenData.Should().NotBeNull();
        tokenData!.Token.Should().StartWith("PRM-TOKEN-");

        var enrollmentToken = tokenData.Token;

        // ---------------------------------------------------------------------
        // Step 2: Bootstrap Handshake registers pilot detector manifest (DEC-004)
        // ---------------------------------------------------------------------
        var pilotProbe = new JournalBalanceDetector(_simrsConnectionFactory);
        var manifest = pilotProbe.Manifest;
        var uniqueDetectorName = $"{manifest.Name} - E2E Smoke {Guid.NewGuid():N}";

        var handshakeRequest = new DetectorHandshakeRequest(
            EnrollmentToken: enrollmentToken,
            Name: uniqueDetectorName,
            TargetModule: manifest.TargetModule,
            Scope: manifest.Scope,
            ExpectedCadenceSeconds: manifest.ExpectedCadenceSeconds,
            WebhookUrl: $"http://127.0.0.1:5090/webhooks/{manifest.WebhookEndpointKey}",
            Description: manifest.Description,
            Version: manifest.Version);

        // Handshake endpoint is anonymous bootstrap
        var handshakeResponse = await _client.PostAsJsonAsync("/api/detectors/handshake", handshakeRequest);
        handshakeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var handshakeResult = await handshakeResponse.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);
        handshakeResult.Should().NotBeNull();
        handshakeResult!.DetectorId.Should().NotBeEmpty();
        handshakeResult.ApiKey.Should().StartWith("prm_key_");
        handshakeResult.Status.Should().Be("Active");
        handshakeResult.Version.Should().Be(manifest.Version);

        var detectorId = handshakeResult.DetectorId;
        var detectorApiKey = handshakeResult.ApiKey;

        // Verify Database: Token marked Consumed and linked to detector
        using (var connection = _factory.CreateDbConnection())
        {
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(enrollmentToken))).ToLowerInvariant();
            var tokenDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT Status, ConsumedByDetectorId, ConsumedAtUtc FROM dbo.DetectorEnrollmentTokens WHERE TokenHash = @TokenHash;",
                new { TokenHash = tokenHash });

            ((string)tokenDb!.Status).Should().Be("Consumed");
            ((Guid)tokenDb.ConsumedByDetectorId).Should().Be(detectorId);
            ((DateTime?)tokenDb.ConsumedAtUtc).Should().NotBeNull();

            // Verify Database: Detector created with Hashed API key and Unknown health
            var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(detectorApiKey))).ToLowerInvariant();
            var detectorDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT Name, AdminStatus, HealthStatus, ApiKeyHash, Version, ExpectedCadenceSeconds, Scope FROM dbo.Detectors WHERE Id = @Id;",
                new { Id = detectorId });

            ((string)detectorDb!.Name).Should().Be(uniqueDetectorName);
            ((string)detectorDb.AdminStatus).Should().Be("Active");
            ((string)detectorDb.HealthStatus).Should().Be("Unknown");
            ((string)detectorDb.ApiKeyHash).Should().Be(apiKeyHash);
            ((string)detectorDb.Version).Should().Be(manifest.Version);
            ((int)detectorDb.ExpectedCadenceSeconds).Should().Be(manifest.ExpectedCadenceSeconds);
            ((string)detectorDb.Scope).Should().Be("DataIntegrity");
        }

        // ---------------------------------------------------------------------
        // Step 3: Ingest Telemetry Findings using provisioned per-detector key (BR-PRIMA-001)
        // ---------------------------------------------------------------------
        var baselineTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var findingsPayload = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = baselineTimestamp,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 125,
            Findings = new List<ApiFindingItem>
            {
                new()
                {
                    TargetEntityReference = "JRN-202611-ERR01",
                    Title = "Unbalanced Journal Batch JRN-202611-ERR01",
                    BaseSeverity = "High",
                    Evidence = new ApiFindingEvidenceItem
                    {
                        Module = "Accounting",
                        SourceTable = "simrs.acc_journal_entries",
                        RecordKeys = "journal_no=JRN-202611-ERR01",
                        DebitTotal = 5000000.00m,
                        CreditTotal = 4850000.00m,
                        VarianceDelta = 150000.00m,
                        Trace = "Unbalanced journal batch JRN-202611-ERR01 debit: 5000000.00 credit: 4850000.00 delta: 150000.00"
                    }
                }
            }
        };

        var ingestionMessage = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(findingsPayload)
        };
        ingestionMessage.Headers.Add("X-Detector-API-Key", detectorApiKey);

        var ingestionResponse = await _client.SendAsync(ingestionMessage);
        ingestionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingestionResponseContent = await ingestionResponse.Content.ReadAsStringAsync();
        ingestionResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"ingestion failed: {ingestionResponseContent}");

        var ingestionBody = await ingestionResponse.Content.ReadFromJsonAsync<ApiIngestTelemetryResponse>(JsonOptions);
        ingestionBody.Should().NotBeNull();
        ingestionBody!.DetectorId.Should().Be(detectorId);
        ingestionBody.Outcome.Should().Be("FindingDiscovered");
        ingestionBody.Results.Should().NotBeEmpty();

        var findingResult = ingestionBody.Results[0];
        findingResult.IssueId.Should().NotBeEmpty();

        // Verify Issue and Occurrence in Database
        using (var connection = _factory.CreateDbConnection())
        {
            var issueDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                """
                SELECT Id, CorrelationKey, Scope, CurrentSeverity, BaseSeverity, Status,
                       OccurrenceCount
                FROM dbo.Issues
                WHERE Id = @Id;
                """,
                new { Id = findingResult.IssueId });

            ((object?)issueDb).Should().NotBeNull();
            ((string)issueDb!.Scope).Should().Be("DataIntegrity");
            ((string)issueDb.Status).Should().Be("Open");
            ((string)issueDb.CurrentSeverity).Should().Be("High");
            ((int)issueDb.OccurrenceCount).Should().Be(1);

            var occurrenceDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                """
                SELECT OccurrenceNumber, Module, DebitTotal, CreditTotal, VarianceDelta, TraceString
                FROM dbo.Occurrences
                WHERE IssueId = @IssueId;
                """,
                new { IssueId = (Guid)issueDb.Id });

            ((object?)occurrenceDb).Should().NotBeNull();
            ((int)occurrenceDb!.OccurrenceNumber).Should().Be(1);
            ((string)occurrenceDb.Module).Should().Be("Accounting");
            ((decimal)occurrenceDb.DebitTotal).Should().Be(5000000.00m);
            ((decimal)occurrenceDb.CreditTotal).Should().Be(4850000.00m);
            ((decimal)occurrenceDb.VarianceDelta).Should().Be(150000.00m);
            ((string)occurrenceDb.TraceString).Should().Contain("JRN-202611-ERR01");
        }

        // ---------------------------------------------------------------------
        // Step 4: Cadence Drift Detection when interval diverges by > ±50% (BR-PRIMA-010)
        // Expected cadence: 300s. Divergent interval: 600s (+100% drift).
        // ---------------------------------------------------------------------
        var driftedTimestamp = baselineTimestamp.AddSeconds(600);
        var cleanRunPayload = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = driftedTimestamp,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 85
        };

        var driftMessage = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(cleanRunPayload)
        };
        driftMessage.Headers.Add("X-Detector-API-Key", detectorApiKey);

        var driftResponse = await _client.SendAsync(driftMessage);
        driftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var driftBody = await driftResponse.Content.ReadFromJsonAsync<ApiIngestTelemetryResponse>(JsonOptions);
        driftBody.Should().NotBeNull();
        driftBody!.Warnings.Should().NotBeEmpty();
        driftBody.Warnings.Should().Contain(w =>
            w.Contains("Execution cadence drifted") &&
            w.Contains("actual: 600s, expected: 300s"));

        // Verify Database: LastActualIntervalSeconds and CadenceDriftWarning persisted
        using (var connection = _factory.CreateDbConnection())
        {
            var detectorDriftDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;",
                new { Id = detectorId });

            ((int)detectorDriftDb!.LastActualIntervalSeconds).Should().Be(600);
            ((string)detectorDriftDb.CadenceDriftWarning).Should().Contain("Execution cadence drifted");
        }

        // ---------------------------------------------------------------------
        // Step 5: On-Demand Webhook Run via /webhooks/{detectorKey} (DEC-005)
        // ---------------------------------------------------------------------
        var tempCredPath = CreateTempCredentialsFile();
        var credentialsJson = $$"""
        {
          "probes": {
            "{{manifest.Name}}": {
              "detectorId": "{{detectorId}}",
              "apiKey": "{{detectorApiKey}}",
              "serverUrl": "http://localhost",
              "detectorName": "{{manifest.Name}}"
            }
          }
        }
        """;
        await File.WriteAllTextAsync(tempCredPath, credentialsJson);

        // Test webhook host running the probe
        var webhookBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton(NullLoggerFactory.Instance);
                services.AddSingleton(_simrsConnectionFactory);
                services.AddSingleton<IDetectorProbe>(pilotProbe);
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

        using var webhookServer = new TestServer(webhookBuilder);
        var webhookClient = webhookServer.CreateClient();

        var webhookTrigger = new WebhookTriggerRequest(
            DetectorId: detectorId,
            TriggerSource: "Manual",
            IssueId: null,
            DispatchedAtUtc: DateTime.UtcNow);

        var webhookResponse = await webhookClient.PostAsJsonAsync(
            $"/webhooks/{manifest.WebhookEndpointKey}",
            webhookTrigger);

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var webhookBody = await webhookResponse.Content.ReadFromJsonAsync<WebhookExecutionResponse>(JsonOptions);
        webhookBody.Should().NotBeNull();
        webhookBody!.Outcome.Should().Be("FindingDiscovered");
        webhookBody.Findings.Should().Contain(f => f.TargetEntityReference == "JRN-202611-ERR01");

        // ---------------------------------------------------------------------
        // Step 6: Deep Database Health Check on GET /api/health (DEC-009)
        // ---------------------------------------------------------------------
        var healthResponse = await _client.GetAsync("/api/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var healthBody = await healthResponse.Content.ReadFromJsonAsync<FullStackHealthResponseDto>(JsonOptions);
        healthBody.Should().NotBeNull();
        healthBody!.Status.Should().Be("Healthy");
        healthBody.Database.Should().Be("Connected");
        healthBody.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // ---------------------------------------------------------------------
        // Step 7: Daily Retention Pruning Execution (DEC-009, BR-PRIMA-009)
        // ---------------------------------------------------------------------
        var simulatedNow = DateTime.UtcNow;
        var oldRunCleanPassed1 = Guid.NewGuid();
        var oldRunCleanPassed2 = Guid.NewGuid();
        var oldRunLinkedToOccurrence = Guid.NewGuid();
        var oldRunFindingDiscovered = Guid.NewGuid();
        var recentPassedRun = Guid.NewGuid();

        using (var connection = _factory.CreateDbConnection())
        {
            // Eligible for pruning: clean Passed runs older than 90 days
            await InsertInspectionRunRecordAsync(connection, oldRunCleanPassed1, detectorId, simulatedNow.AddDays(-95), "Passed");
            await InsertInspectionRunRecordAsync(connection, oldRunCleanPassed2, detectorId, simulatedNow.AddDays(-120), "Passed");

            // Ineligible: recent Passed run within 90 days
            await InsertInspectionRunRecordAsync(connection, recentPassedRun, detectorId, simulatedNow.AddDays(-30), "Passed");

            // Ineligible: old FindingDiscovered run older than 90 days (preserve audit trail)
            await InsertInspectionRunRecordAsync(connection, oldRunFindingDiscovered, detectorId, simulatedNow.AddDays(-100), "FindingDiscovered");

            // Ineligible: old Passed run linked to an occurrence
            await InsertInspectionRunRecordAsync(connection, oldRunLinkedToOccurrence, detectorId, simulatedNow.AddDays(-105), "Passed");
            var linkedIssueId = Guid.NewGuid();
            await connection.ExecuteAsync("""
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                    OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
                ) VALUES (
                    @Id, @Key, 'Linked Old Issue', @DetectorId, 'Smoke Test', 'DataIntegrity',
                    'Low', 'Low', 1, 'Closed', 0, 1, @Now, @Now
                );
                """, new { Id = linkedIssueId, Key = $"RETENTION:{Guid.NewGuid():N}", DetectorId = detectorId, Now = simulatedNow.AddDays(-105) });

            await connection.ExecuteAsync("""
                INSERT INTO dbo.Occurrences (
                    Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc, Module, TraceString
                ) VALUES (
                    @Id, @IssueId, @InspectionRunId, 1, @Now, 'Accounting', 'Retention evidence link'
                );
                """, new { Id = Guid.NewGuid(), IssueId = linkedIssueId, InspectionRunId = oldRunLinkedToOccurrence, Now = simulatedNow.AddDays(-105) });
        }

        var supervisorWorker = CreateSupervisorWorker(retentionDays: 90);
        var prunedCount = await supervisorWorker.ExecuteRetentionPruningAsync(simulatedUtcNow: simulatedNow);

        prunedCount.Should().Be(2, "only the two unlinked Passed runs older than 90 days should be deleted");

        using (var connection = _factory.CreateDbConnection())
        {
            var remainingRuns = (await connection.QueryAsync<Guid>(
                "SELECT Id FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId;",
                new { DetectorId = detectorId })).ToHashSet();

            // Pruned
            remainingRuns.Should().NotContain(oldRunCleanPassed1);
            remainingRuns.Should().NotContain(oldRunCleanPassed2);

            // Preserved
            remainingRuns.Should().Contain(recentPassedRun);
            remainingRuns.Should().Contain(oldRunFindingDiscovered);
            remainingRuns.Should().Contain(oldRunLinkedToOccurrence);
        }
    }

    // -------------------------------------------------------------------------
    // 2. Focused Isolation Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handshake_WhenTokenAlreadyConsumed_RejectsWith401Unauthorized()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var tokenRes = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", new GenerateEnrollmentTokenRequest("Single Use Token"));
        var tokenData = await tokenRes.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        var token = tokenData!.Token;

        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: $"Detector-Single-Use-{Guid.NewGuid():N}",
            TargetModule: "Billing",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: 60,
            WebhookUrl: "http://localhost:5090/webhooks/single-use",
            Description: "Test single use token",
            Version: "1.0.0");

        // First handshake succeeds
        var firstHandshake = await _client.PostAsJsonAsync("/api/detectors/handshake", request);
        firstHandshake.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: Second handshake with same consumed token
        var secondHandshake = await _client.PostAsJsonAsync("/api/detectors/handshake", request);

        // Assert
        secondHandshake.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TelemetryIngestion_WithInvalidPerDetectorApiKey_RejectsWith401Unauthorized()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(
            name: "Per-Detector Auth Fail Probe",
            apiKeyHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("prm_key_correct_secret"))).ToLowerInvariant());

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 50
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Detector-API-Key", "prm_key_tampered_wrong_secret");

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CadenceDrift_DivergingMinus50Percent_GeneratesWarningAndPersists()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-10);
        var executionTime = baselineTime.AddSeconds(20); // 20s actual vs 60s expected -> -66.7% drift (< -50%)

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Fast-Drift Probe",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 70
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Detector-API-Key", PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiIngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Warnings.Should().NotBeEmpty();
        body.Warnings.Should().Contain(w => w.Contains("Execution cadence drifted") && w.Contains("actual: 20s, expected: 60s"));

        // Verify Database
        using var connection = _factory.CreateDbConnection();
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;",
            new { Id = detectorId });

        ((int)row!.LastActualIntervalSeconds).Should().Be(20);
        ((string)row.CadenceDriftWarning).Should().Contain("Execution cadence drifted");
    }

    [Fact]
    public async Task HealthCheck_DeepDatabaseCheck_ReportsHealthyAndConnected()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FullStackHealthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
        body.Database.Should().Be("Connected");
        body.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    // -------------------------------------------------------------------------
    // Helper Methods & Types
    // -------------------------------------------------------------------------

    private static async Task InsertInspectionRunRecordAsync(IDbConnection conn, Guid id, Guid detectorId, DateTime timestampUtc, string outcome)
    {
        await conn.ExecuteAsync("""
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc
            ) VALUES (
                @Id, @DetectorId, @TimestampUtc, 'Scheduled', @Outcome, 50, @TimestampUtc
            );
            """, new { Id = id, DetectorId = detectorId, TimestampUtc = timestampUtc, Outcome = outcome });
    }

    private sealed record FullStackHealthResponseDto(
        string Status,
        string Database,
        DateTime? TimestampUtc,
        string? Error);
}
