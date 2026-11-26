using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class Stage5ApiKeyAndCadenceDriftTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Stage5ApiKeyAndCadenceDriftTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpRequestMessage CreateRequest(object content, string? apiKey = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(content)
        };

        if (apiKey is not null)
        {
            message.Headers.Add(ApiKeyEndpointFilter.HeaderName, apiKey);
        }

        return message;
    }

    private static string HashKey(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    public async Task Authenticate_WithValidPerDetectorHashedKey_AllowsIngestion()
    {
        // Arrange
        const string rawDetectorApiKey = "prm_key_detector_pilot_01_secure_alpha";
        var keyHash = HashKey(rawDetectorApiKey);

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Per-Detector Auth Test Probe",
            apiKeyHash: keyHash,
            expectedCadenceSeconds: 60);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Manual",
            Outcome = "Passed",
            DurationMs = 75
        };

        var message = CreateRequest(request, apiKey: rawDetectorApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.DetectorId.Should().Be(detectorId);
        body.Outcome.Should().Be("Passed");
    }

    [Fact]
    public async Task Authenticate_WithInvalidPerDetectorKey_Returns401Unauthorized()
    {
        // Arrange
        const string rawDetectorApiKey = "prm_key_correct_secret";
        var keyHash = HashKey(rawDetectorApiKey);

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Per-Detector Wrong Key Probe",
            apiKeyHash: keyHash);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Manual",
            Outcome = "Passed",
            DurationMs = 50
        };

        var message = CreateRequest(request, apiKey: "prm_key_incorrect_secret");

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Missing or invalid X-Detector-API-Key header.");
    }

    [Fact]
    public async Task Authenticate_WithGlobalFallbackKey_WhenDetectorHasNoPerDetectorKey_AllowsIngestion()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(
            name: "Global Fallback Test Probe",
            apiKeyHash: null);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Manual",
            Outcome = "Passed",
            DurationMs = 45
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.DetectorId.Should().Be(detectorId);
    }

    [Fact]
    public async Task Authenticate_WithGlobalFallbackKey_WhenDetectorHasSpecificKey_AllowsIngestion()
    {
        // Arrange
        var keyHash = HashKey("prm_key_detector_specific_99");
        var detectorId = await _factory.SeedDetectorAsync(
            name: "Detector With Specific Key Probe",
            apiKeyHash: keyHash);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Manual",
            Outcome = "Passed",
            DurationMs = 45
        };

        // Fallback global key should still be accepted
        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ingest_ScheduledRun_WithCadenceDriftExceedingPlus50Percent_GeneratesWarningAndPersists()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-5);
        var executionTime = baselineTime.AddSeconds(120); // 120s actual vs 60s expected -> +100% drift (> +50%)

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Drift Upper Probe",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 110
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Warnings.Should().NotBeEmpty();
        body.Warnings.Should().Contain(w => w.Contains("Execution cadence drifted") && w.Contains("actual: 120s, expected: 60s"));

        // Verify Database Persistence
        const string sql = "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;";
        var row = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(sql, new { Id = detectorId });
        ((int)row!.LastActualIntervalSeconds).Should().Be(120);
        ((string)row.CadenceDriftWarning).Should().Contain("Execution cadence drifted");
    }

    [Fact]
    public async Task Ingest_ScheduledRun_WithCadenceDriftExceedingMinus50Percent_GeneratesWarningAndPersists()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-5);
        var executionTime = baselineTime.AddSeconds(20); // 20s actual vs 60s expected -> -66.7% drift (< -50%)

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Drift Lower Probe",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 95
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Warnings.Should().NotBeEmpty();
        body.Warnings.Should().Contain(w => w.Contains("Execution cadence drifted") && w.Contains("actual: 20s, expected: 60s"));

        // Verify Database Persistence
        const string sql = "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;";
        var row = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(sql, new { Id = detectorId });
        ((int)row!.LastActualIntervalSeconds).Should().Be(20);
        ((string)row.CadenceDriftWarning).Should().Contain("Execution cadence drifted");
    }

    [Fact]
    public async Task Ingest_ScheduledRun_NormalizesCadence_ClearsDriftWarning()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-5);
        var executionTime = baselineTime.AddSeconds(60); // 60s actual vs 60s expected -> 0% drift (within [30s, 90s])

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Drift Normalization Probe",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        // Pre-populate CadenceDriftWarning in DB to test clearing
        await _factory.ExecuteSqlAsync(
            "UPDATE dbo.Detectors SET CadenceDriftWarning = 'Previous drift warning', LastActualIntervalSeconds = 150 WHERE Id = @Id;",
            new { Id = detectorId });

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 80
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Warnings.Should().BeEmpty();

        // Verify Database Persistence: CadenceDriftWarning cleared to NULL, LastActualIntervalSeconds updated
        const string sql = "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;";
        var row = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(sql, new { Id = detectorId });
        ((int)row!.LastActualIntervalSeconds).Should().Be(60);
        Assert.Null(row.CadenceDriftWarning);
    }

    [Fact]
    public async Task Ingest_ManualRun_DoesNotComputeOrOverwriteCadenceDrift()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-5);
        var executionTime = baselineTime.AddSeconds(15);

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Manual Run No Drift Probe",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        await _factory.ExecuteSqlAsync(
            "UPDATE dbo.Detectors SET CadenceDriftWarning = 'Preserved Warning', LastActualIntervalSeconds = 120 WHERE Id = @Id;",
            new { Id = detectorId });

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Manual",
            Outcome = "Passed",
            DurationMs = 50
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Warnings.Should().BeEmpty();

        // Verify Database: CadenceDriftWarning and LastActualIntervalSeconds are preserved
        const string sql = "SELECT LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;";
        var row = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(sql, new { Id = detectorId });
        ((int)row!.LastActualIntervalSeconds).Should().Be(120);
        ((string)row.CadenceDriftWarning).Should().Be("Preserved Warning");
    }

    [Fact]
    public async Task Ingest_ScheduledRun_ExecutionFailed_CalculatesAndPersistsCadenceDrift()
    {
        // Arrange
        var baselineTime = DateTime.UtcNow.AddMinutes(-5);
        var executionTime = baselineTime.AddSeconds(180); // 180s actual vs 60s expected -> +200% drift

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Failing Probe With Drift",
            expectedCadenceSeconds: 60,
            lastRunAtUtc: baselineTime);

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 200,
            FailureReason = "Probe connection failed with timeout."
        };

        var message = CreateRequest(request, apiKey: PrIMAApiFactory.TestApiKey);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Outcome.Should().Be("ExecutionFailed");
        body.Warnings.Should().NotBeEmpty();
        body.Warnings.Should().Contain(w => w.Contains("Execution cadence drifted"));

        // Verify Database: Health is Failing, Drift Warning is recorded
        const string sql = "SELECT HealthStatus, LastActualIntervalSeconds, CadenceDriftWarning FROM dbo.Detectors WHERE Id = @Id;";
        var row = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(sql, new { Id = detectorId });
        ((string)row!.HealthStatus).Should().Be("Failing");
        ((int)row.LastActualIntervalSeconds).Should().Be(180);
        ((string)row.CadenceDriftWarning).Should().Contain("Execution cadence drifted");
    }
}
