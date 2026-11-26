using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorHandshakeEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DetectorHandshakeEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private async Task<string> GenerateEnrollmentTokenAsync(string label = "Test Enrollment Token")
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var response = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", new GenerateEnrollmentTokenRequest(label));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        created.Should().NotBeNull();
        return created!.Token;
    }

    [Fact]
    public async Task Handshake_WithValidPendingToken_NewDetector_CreatesDetectorUnknownHealth_ConsumesToken_Returns200WithApiKey()
    {
        // Arrange
        var token = await GenerateEnrollmentTokenAsync("Handshake Fresh Test");
        var detectorName = $"fresh-detector-{Guid.NewGuid():N}";
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: detectorName,
            TargetModule: "Billing",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: 60,
            WebhookUrl: "http://127.0.0.1:5099/webhooks/fresh-test",
            Description: "Fresh test detector manifest",
            Version: "1.0.0");

        // Act - Anonymous call allowed
        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var handshakeResult = await response.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);
        handshakeResult.Should().NotBeNull();
        handshakeResult!.DetectorId.Should().NotBeEmpty();
        handshakeResult.ApiKey.Should().StartWith("prm_key_");
        handshakeResult.Status.Should().Be("Active");
        handshakeResult.Name.Should().Be(detectorName);
        handshakeResult.Version.Should().Be("1.0.0");

        // Verify in Database
        using var connection = _factory.CreateDbConnection();
        var expectedApiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(handshakeResult.ApiKey))).ToLowerInvariant();

        var detector = await connection.QuerySingleOrDefaultAsync<DetectorDbRow>(
            "SELECT Id, Name, AdminStatus, HealthStatus, ApiKeyHash, Version, ExpectedCadenceSeconds, Scope FROM dbo.Detectors WHERE Id = @Id",
            new { Id = handshakeResult.DetectorId });

        detector.Should().NotBeNull();
        detector!.AdminStatus.Should().Be("Active");
        detector.HealthStatus.Should().Be("Unknown");
        detector.ApiKeyHash.Should().Be(expectedApiKeyHash);
        detector.Version.Should().Be("1.0.0");
        detector.ExpectedCadenceSeconds.Should().Be(60);
        detector.Scope.Should().Be("DataIntegrity");

        // Token consumed
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        var tokenRecord = await connection.QuerySingleOrDefaultAsync<TokenDbRow>(
            "SELECT Status, ConsumedAtUtc, ConsumedByDetectorId FROM dbo.DetectorEnrollmentTokens WHERE TokenHash = @TokenHash",
            new { TokenHash = tokenHash });

        tokenRecord.Should().NotBeNull();
        tokenRecord!.Status.Should().Be("Consumed");
        tokenRecord.ConsumedAtUtc.Should().NotBeNull();
        tokenRecord.ConsumedByDetectorId.Should().Be(handshakeResult.DetectorId);
    }

    [Fact]
    public async Task Handshake_WithAlreadyConsumedToken_Returns401Unauthorized()
    {
        // Arrange
        var token = await GenerateEnrollmentTokenAsync("Handshake Consumed Test");
        var detectorName = $"consumed-test-{Guid.NewGuid():N}";
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: detectorName,
            TargetModule: "Billing",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: 60);

        // First handshake succeeds
        var firstResponse = await _client.PostAsJsonAsync("/api/detectors/handshake", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Replay handshake with already consumed token
        var secondRequest = request with { Name = $"another-detector-{Guid.NewGuid():N}" };
        var secondResponse = await _client.PostAsJsonAsync("/api/detectors/handshake", secondRequest);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Handshake_WithInvalidToken_Returns401Unauthorized()
    {
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: "PRM-TOKEN-non-existent-invalid-token",
            Name: $"invalid-token-detector-{Guid.NewGuid():N}",
            TargetModule: "Accounting",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: 120);

        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Handshake_WithRevokedToken_Returns401Unauthorized()
    {
        // Arrange - Generate and revoke token
        var authClient = await CreateAuthenticatedClientAsync();
        var genResponse = await authClient.PostAsJsonAsync(
            "/api/detectors/enrollment-tokens",
            new GenerateEnrollmentTokenRequest("Revocation Test"));
        var created = await genResponse.Content.ReadFromJsonAsync<GenerateEnrollmentTokenResponse>(JsonOptions);
        created.Should().NotBeNull();

        var revokeResponse = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{created!.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Attempt handshake with revoked token
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: created.Token,
            Name: $"revoked-token-detector-{Guid.NewGuid():N}",
            TargetModule: "EMR",
            Scope: "DataSync",
            ExpectedCadenceSeconds: 300);

        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Handshake_WithExpiredToken_Returns401Unauthorized()
    {
        // Arrange - Generate token and force expired in DB
        var token = await GenerateEnrollmentTokenAsync("Expired Token Test");
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        using (var connection = _factory.CreateDbConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE dbo.DetectorEnrollmentTokens SET ExpiresAtUtc = DATEADD(hour, -2, SYSUTCDATETIME()) WHERE TokenHash = @TokenHash",
                new { TokenHash = tokenHash });
        }

        // Act
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: $"expired-token-detector-{Guid.NewGuid():N}",
            TargetModule: "Outpatient",
            Scope: "DataSync",
            ExpectedCadenceSeconds: 60);

        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Handshake_ExistingDetector_IdempotentlyUpdatesManifest_RotatesApiKey_RestoresActiveStatus()
    {
        // Arrange - First enrollment
        var token1 = await GenerateEnrollmentTokenAsync("Initial Enrollment");
        var detectorName = $"idempotent-detector-{Guid.NewGuid():N}";
        var initialRequest = new DetectorHandshakeRequest(
            EnrollmentToken: token1,
            Name: detectorName,
            TargetModule: "Billing",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: 60,
            Description: "Initial Description",
            Version: "1.0.0");

        var response1 = await _client.PostAsJsonAsync("/api/detectors/handshake", initialRequest);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialResult = await response1.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);
        initialResult.Should().NotBeNull();

        // Simulate detector paused by admin in DB
        using (var connection = _factory.CreateDbConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE dbo.Detectors SET AdminStatus = 'Paused' WHERE Id = @Id",
                new { Id = initialResult!.DetectorId });
        }

        // Generate second token for re-enrollment / manifest upgrade
        var token2 = await GenerateEnrollmentTokenAsync("Manifest Upgrade");
        var upgradeRequest = new DetectorHandshakeRequest(
            EnrollmentToken: token2,
            Name: detectorName,
            TargetModule: "BillingAndReceivables",
            Scope: "ProcessIntegrity",
            ExpectedCadenceSeconds: 120,
            WebhookUrl: "http://127.0.0.1:5099/webhooks/upgraded",
            Description: "Upgraded Description",
            Version: "1.2.0");

        // Act - Re-enrollment handshake
        var response2 = await _client.PostAsJsonAsync("/api/detectors/handshake", upgradeRequest);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var upgradeResult = await response2.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);
        upgradeResult.Should().NotBeNull();
        upgradeResult!.DetectorId.Should().Be(initialResult.DetectorId); // Same detector ID
        upgradeResult.ApiKey.Should().NotBe(initialResult.ApiKey); // Rotated API key
        upgradeResult.Status.Should().Be("Active"); // Restored to Active even though it was Paused
        upgradeResult.Version.Should().Be("1.2.0");

        // Verify DB updated idempotently
        using (var connection = _factory.CreateDbConnection())
        {
            var detector = await connection.QuerySingleOrDefaultAsync<DetectorManifestDbRow>(
                "SELECT TargetModule, Scope, ExpectedCadenceSeconds, WebhookUrl, Description, Version, AdminStatus, ApiKeyHash FROM dbo.Detectors WHERE Id = @Id",
                new { Id = initialResult.DetectorId });

            detector.Should().NotBeNull();
            detector!.TargetModule.Should().Be("BillingAndReceivables");
            detector.Scope.Should().Be("ProcessIntegrity");
            detector.ExpectedCadenceSeconds.Should().Be(120);
            detector.WebhookUrl.Should().Be("http://127.0.0.1:5099/webhooks/upgraded");
            detector.Description.Should().Be("Upgraded Description");
            detector.Version.Should().Be("1.2.0");
            detector.AdminStatus.Should().Be("Active");

            var newKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(upgradeResult.ApiKey))).ToLowerInvariant();
            detector.ApiKeyHash.Should().Be(newKeyHash);
        }
    }

    [Fact]
    public async Task Handshake_ExistingRetiredDetector_Returns409Conflict()
    {
        // Arrange - Enroll detector then retire it
        var token1 = await GenerateEnrollmentTokenAsync("Retire Handshake Token 1");
        var detectorName = $"retired-detector-{Guid.NewGuid():N}";
        var initialRequest = new DetectorHandshakeRequest(
            EnrollmentToken: token1,
            Name: detectorName,
            TargetModule: "Infrastructure",
            Scope: "Infrastructure",
            ExpectedCadenceSeconds: 60);

        var response1 = await _client.PostAsJsonAsync("/api/detectors/handshake", initialRequest);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialResult = await response1.Content.ReadFromJsonAsync<DetectorHandshakeResponse>(JsonOptions);

        using (var connection = _factory.CreateDbConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE dbo.Detectors SET AdminStatus = 'Retired' WHERE Id = @Id",
                new { Id = initialResult!.DetectorId });
        }

        // Generate token for re-handshake
        var token2 = await GenerateEnrollmentTokenAsync("Retire Handshake Token 2");
        var reEnrollRequest = initialRequest with { EnrollmentToken = token2 };

        // Act
        var response2 = await _client.PostAsJsonAsync("/api/detectors/handshake", reEnrollRequest);

        // Assert - BR-PRIMA-008: Retired detectors cannot be modified or re-enrolled
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(29)]    // Less than 30s minimum
    [InlineData(86401)] // Greater than 86400s (24h) maximum
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Handshake_WithCadenceOutOfBounds_Returns422UnprocessableEntity(int invalidCadence)
    {
        var token = await GenerateEnrollmentTokenAsync("Cadence Bounds Test");
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: $"cadence-test-{Guid.NewGuid():N}",
            TargetModule: "Accounting",
            Scope: "DataIntegrity",
            ExpectedCadenceSeconds: invalidCadence);

        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("InvalidScope")]
    [InlineData("DataQuality")]    // Non-canonical scope in Stage 5
    [InlineData("BusinessLogic")]  // Non-canonical scope in Stage 5
    [InlineData("")]
    public async Task Handshake_WithNonCanonicalScope_Returns422UnprocessableEntity(string invalidScope)
    {
        var token = await GenerateEnrollmentTokenAsync("Scope Bounds Test");
        var request = new DetectorHandshakeRequest(
            EnrollmentToken: token,
            Name: $"scope-test-{Guid.NewGuid():N}",
            TargetModule: "Accounting",
            Scope: invalidScope,
            ExpectedCadenceSeconds: 60);

        var response = await _client.PostAsJsonAsync("/api/detectors/handshake", request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private sealed record DetectorDbRow(
        Guid Id,
        string Name,
        string AdminStatus,
        string HealthStatus,
        string? ApiKeyHash,
        string? Version,
        int ExpectedCadenceSeconds,
        string Scope);

    private sealed record DetectorManifestDbRow(
        string TargetModule,
        string Scope,
        int ExpectedCadenceSeconds,
        string? WebhookUrl,
        string Description,
        string? Version,
        string AdminStatus,
        string? ApiKeyHash);

    private sealed record TokenDbRow(
        string Status,
        DateTime? ConsumedAtUtc,
        Guid? ConsumedByDetectorId);
}
