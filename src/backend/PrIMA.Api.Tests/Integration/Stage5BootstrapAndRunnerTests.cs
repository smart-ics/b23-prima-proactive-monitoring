using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Tests.Fixtures;
using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class Stage5BootstrapAndRunnerTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private readonly List<string> _tempFilesToCleanup = new();

    public Stage5BootstrapAndRunnerTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
                // Ignore test file deletion cleanup errors
            }
        }

        return Task.CompletedTask;
    }

    private string GetTempCredentialFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"prima_cred_{Guid.NewGuid():N}.json");
        _tempFilesToCleanup.Add(path);
        return path;
    }

    private async Task<string> GenerateEnrollmentTokenAsync(string label = "Runner Test Token")
    {
        var authToken = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var response = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", new GenerateEnrollmentTokenRequest(label));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        created.Should().NotBeNull();
        return created!.Token;
    }

    [Fact]
    public async Task BootstrapHandshakeService_WhenCredentialsCachedOnDisk_ReturnsCachedCredentialsWithoutNetworkCall()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var cachedDetectorId = Guid.NewGuid();
        const string cachedApiKey = "prm_key_cached_secret_12345";
        const string serverUrl = "http://127.0.0.1:5000";

        var cacheContent = $$"""
        {
          "detectorId": "{{cachedDetectorId}}",
          "apiKey": "{{cachedApiKey}}",
          "serverUrl": "{{serverUrl}}",
          "detectorName": "Disk Cache Probe"
        }
        """;
        await File.WriteAllTextAsync(tempCredPath, cacheContent);

        // Dummy client that will fail if any HTTP call is made
        var handler = new ThrowingHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(serverUrl) };

        var service = new BootstrapHandshakeService(
            httpClient,
            Options.Create(new DetectorSdkOptions { CredentialFilePath = tempCredPath }),
            NullLogger<BootstrapHandshakeService>.Instance);

        var probe = new TestSimpleProbe("Disk Cache Probe");

        // Act
        var credential = await service.EnsureCredentialsAsync(probe);

        // Assert
        credential.Should().NotBeNull();
        credential!.DetectorId.Should().Be(cachedDetectorId);
        credential.ApiKey.Should().Be(cachedApiKey);
        credential.ServerUrl.Should().Be(serverUrl);
    }

    [Fact]
    public async Task BootstrapHandshakeService_WhenCredentialsMissing_AndEnrollmentTokenPresent_ExecutesHandshake_SavesCredentials()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var token = await GenerateEnrollmentTokenAsync("Handshake Integration Probe");
        var probe = new TestSimpleProbe($"Live-Handshake-Probe-{Guid.NewGuid():N}");

        var service = new BootstrapHandshakeService(
            _client,
            Options.Create(new DetectorSdkOptions
            {
                CredentialFilePath = tempCredPath,
                EnrollmentToken = token,
                ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
            }),
            NullLogger<BootstrapHandshakeService>.Instance);

        // Act
        var credential = await service.EnsureCredentialsAsync(probe);

        // Assert
        credential.Should().NotBeNull();
        credential!.DetectorId.Should().NotBeEmpty();
        credential.ApiKey.Should().StartWith("prm_key_");

        // Verify credentials file was written to disk
        File.Exists(tempCredPath).Should().BeTrue();
        var savedJson = await File.ReadAllTextAsync(tempCredPath);
        savedJson.Should().Contain(credential.DetectorId.ToString());
        savedJson.Should().Contain(credential.ApiKey);

        // Act 2: Calling again should read from disk cache
        var cached = await service.EnsureCredentialsAsync(probe);
        cached.Should().NotBeNull();
        cached!.DetectorId.Should().Be(credential.DetectorId);
        cached.ApiKey.Should().Be(credential.ApiKey);
    }

    [Fact]
    public async Task BootstrapHandshakeService_WhenCredentialsMissing_AndNoEnrollmentToken_LogsErrorAndReturnsNull()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var probe = new TestSimpleProbe("Missing-Token-Probe");

        var service = new BootstrapHandshakeService(
            _client,
            Options.Create(new DetectorSdkOptions
            {
                CredentialFilePath = tempCredPath,
                EnrollmentToken = null
            }),
            NullLogger<BootstrapHandshakeService>.Instance);

        // Act
        var credential = await service.EnsureCredentialsAsync(probe);

        // Assert
        credential.Should().BeNull();
        File.Exists(tempCredPath).Should().BeFalse();
    }

    [Fact]
    public async Task BootstrapHandshakeService_WhenInvalidEnrollmentToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var probe = new TestSimpleProbe("Invalid-Token-Probe");

        var service = new BootstrapHandshakeService(
            _client,
            Options.Create(new DetectorSdkOptions
            {
                CredentialFilePath = tempCredPath,
                EnrollmentToken = "PRM-TOKEN-INVALID-SECRET-9999",
                ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
            }),
            NullLogger<BootstrapHandshakeService>.Instance);

        // Act
        var act = async () => await service.EnsureCredentialsAsync(probe);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Handshake registration failed*401*");
        File.Exists(tempCredPath).Should().BeFalse();
    }

    [Fact]
    public async Task DetectorCliRunner_WhenProbePasses_ExecutesInspection_IngestsTelemetry_ReturnsExitCode0()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var token = await GenerateEnrollmentTokenAsync("CLI Runner Pass Token");
        var probe = new TestSimpleProbe($"CLI-Pass-Probe-{Guid.NewGuid():N}");

        var handshakeService = new BootstrapHandshakeService(
            _client,
            Options.Create(new DetectorSdkOptions
            {
                CredentialFilePath = tempCredPath,
                EnrollmentToken = token,
                ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
            }),
            NullLogger<BootstrapHandshakeService>.Instance);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var detectorRunner = new DetectorRunner(
            new ProbeExecutor(NullLogger<ProbeExecutor>.Instance),
            ingestionClient,
            handshakeService,
            Options.Create(new DetectorSdkOptions { CredentialFilePath = tempCredPath }),
            NullLogger<DetectorRunner>.Instance);

        var cliRunner = new DetectorCliRunner(
            new[] { probe },
            handshakeService,
            detectorRunner,
            NullLogger<DetectorCliRunner>.Instance);

        // Act
        var exitCode = await cliRunner.RunOnceAsync();

        // Assert
        exitCode.Should().Be(0);

        // Verify Inspection Run was recorded in database
        var runCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(*) FROM dbo.InspectionRuns WHERE Outcome = 'Passed'");
        runCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DetectorCliRunner_WhenProbeFails_ReturnsExitCode1()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var token = await GenerateEnrollmentTokenAsync("CLI Runner Fail Token");
        var probe = new TestFailingProbe($"CLI-Fail-Probe-{Guid.NewGuid():N}");

        var handshakeService = new BootstrapHandshakeService(
            _client,
            Options.Create(new DetectorSdkOptions
            {
                CredentialFilePath = tempCredPath,
                EnrollmentToken = token,
                ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
            }),
            NullLogger<BootstrapHandshakeService>.Instance);

        var ingestionClient = new TelemetryIngestionClient(
            _client,
            Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
            NullLogger<TelemetryIngestionClient>.Instance);

        var detectorRunner = new DetectorRunner(
            new ProbeExecutor(NullLogger<ProbeExecutor>.Instance),
            ingestionClient,
            handshakeService,
            Options.Create(new DetectorSdkOptions { CredentialFilePath = tempCredPath }),
            NullLogger<DetectorRunner>.Instance);

        var cliRunner = new DetectorCliRunner(
            new[] { probe },
            handshakeService,
            detectorRunner,
            NullLogger<DetectorCliRunner>.Instance);

        // Act
        var exitCode = await cliRunner.RunOnceAsync();

        // Assert
        exitCode.Should().Be(1);

        // Verify failure was submitted to database
        var failedRunCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(*) FROM dbo.InspectionRuns WHERE Outcome = 'ExecutionFailed'");
        failedRunCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DetectorCliRunner_WhenNoProbesRegistered_ReturnsExitCode0()
    {
        // Arrange
        var handshakeService = new BootstrapHandshakeService(_client);
        var ingestionClient = new TelemetryIngestionClient(_client);
        var detectorRunner = new DetectorRunner(new ProbeExecutor(), ingestionClient, handshakeService);

        var cliRunner = new DetectorCliRunner(
            Array.Empty<IDetectorProbe>(),
            handshakeService,
            detectorRunner,
            NullLogger<DetectorCliRunner>.Instance);

        // Act
        var exitCode = await cliRunner.RunOnceAsync();

        // Assert
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task EmbeddedWebhookListener_MatchingProbe_ExecutesAndReturns200WithPayloadWithin15Seconds()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var token = await GenerateEnrollmentTokenAsync("Webhook Listener Test Token");
        var probe = new TestProbeWithFindings("hospital-webhook-test-probe");

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton<IDetectorProbe>(probe);

                services.AddSingleton<IBootstrapHandshakeService>(_ => new BootstrapHandshakeService(
                    _client,
                    Options.Create(new DetectorSdkOptions
                    {
                        CredentialFilePath = tempCredPath,
                        EnrollmentToken = token,
                        ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
                    }),
                    NullLogger<BootstrapHandshakeService>.Instance));

                var ingestionClient = new TelemetryIngestionClient(
                    _client,
                    Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
                    NullLogger<TelemetryIngestionClient>.Instance);

                services.AddSingleton(ingestionClient);
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

        // Act: POST /webhooks/hospital-webhook-test-probe matching DetectorWebhookClient contract
        var triggerPayload = new WebhookTriggerRequest(
            DetectorId: null,
            TriggerSource: "Manual",
            IssueId: null,
            DispatchedAtUtc: DateTime.UtcNow);

        var response = await testClient.PostAsJsonAsync("/webhooks/hospital-webhook-test-probe", triggerPayload);

        // Assert: HTTP 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WebhookExecutionResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Outcome.Should().Be("FindingDiscovered");
        body.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        body.Findings.Should().HaveCount(1);
        body.Findings[0].TargetEntityReference.Should().Be("BATCH-WEBHOOK-99");
        body.Findings[0].Evidence.DebitTotal.Should().Be(1500m);
    }

    [Fact]
    public async Task EmbeddedWebhookListener_UnknownProbe_Returns404NotFound()
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton<IBootstrapHandshakeService>(new BootstrapHandshakeService(_client));
                services.AddSingleton(new TelemetryIngestionClient(_client));
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

        // Act
        var response = await testClient.PostAsJsonAsync("/webhooks/non-existent-probe", new WebhookTriggerRequest(null, "Manual", null, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmbeddedWebhookListener_WhenProbeThrowsException_TrapsInto200WithOutcomeExecutionFailed()
    {
        // Arrange
        var tempCredPath = GetTempCredentialFilePath();
        var token = await GenerateEnrollmentTokenAsync("Webhook Exception Token");
        var probe = new TestFailingProbe("failing-webhook-probe");

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton<IDetectorProbe>(probe);

                services.AddSingleton<IBootstrapHandshakeService>(_ => new BootstrapHandshakeService(
                    _client,
                    Options.Create(new DetectorSdkOptions
                    {
                        CredentialFilePath = tempCredPath,
                        EnrollmentToken = token,
                        ServerUrl = _client.BaseAddress?.ToString() ?? "http://localhost:5000"
                    }),
                    NullLogger<BootstrapHandshakeService>.Instance));

                var ingestionClient = new TelemetryIngestionClient(
                    _client,
                    Options.Create(new DetectorSdkOptions { MaxRetries = 1 }),
                    NullLogger<TelemetryIngestionClient>.Instance);

                services.AddSingleton(ingestionClient);
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

        // Act
        var response = await testClient.PostAsJsonAsync("/webhooks/failing-webhook-probe", new WebhookTriggerRequest(null, "Manual", null, null));

        // Assert: Failsafe traps exception into 200 OK with Outcome = ExecutionFailed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WebhookExecutionResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Outcome.Should().Be("ExecutionFailed");
        body.FailureReason.Should().Contain("Simulated probe failure");
    }

    #region Helper Test Doubles

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Unexpected HTTP network call was made.");
    }

    private sealed class TestSimpleProbe : IDetectorProbe
    {
        public DetectorManifest Manifest { get; }

        public TestSimpleProbe(string name)
        {
            Manifest = new DetectorManifest(
                name: name,
                targetModule: "Billing",
                scope: "DataIntegrity",
                expectedCadenceSeconds: 60,
                description: "Test simple probe manifest",
                version: "1.0.0",
                webhookEndpointKey: name.ToLowerInvariant().Replace(" ", "-"));
        }

        public Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
        {
            return Task.FromResult(TelemetryReport.Passed(durationMs: 35, traceString: "Clean inspection passed"));
        }
    }

    private sealed class TestFailingProbe : IDetectorProbe
    {
        public DetectorManifest Manifest { get; }

        public TestFailingProbe(string name)
        {
            Manifest = new DetectorManifest(
                name: name,
                targetModule: "Accounting",
                scope: "DataIntegrity",
                expectedCadenceSeconds: 60,
                description: "Failing test probe",
                version: "1.0.0",
                webhookEndpointKey: name.ToLowerInvariant().Replace(" ", "-"));
        }

        public Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException("Simulated probe failure during query execution.");
        }
    }

    private sealed class TestProbeWithFindings : IDetectorProbe
    {
        public DetectorManifest Manifest { get; }

        public TestProbeWithFindings(string name)
        {
            Manifest = new DetectorManifest(
                name: name,
                targetModule: "Accounting",
                scope: "DataIntegrity",
                expectedCadenceSeconds: 60,
                description: "Probe returning finding",
                version: "1.0.0",
                webhookEndpointKey: name.ToLowerInvariant().Replace(" ", "-"));
        }

        public Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
        {
            var finding = new FindingItem(
                targetEntityReference: "BATCH-WEBHOOK-99",
                title: "Webhook Unbalanced Batch",
                baseSeverity: "High",
                evidence: new FindingEvidenceItem("Accounting", debitTotal: 1500m, creditTotal: 1000m, varianceDelta: 500m));

            return Task.FromResult(TelemetryReport.FindingDiscovered(new[] { finding }, durationMs: 40));
        }
    }

    #endregion
}
