using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Features.Detectors.Enrollment.GetPendingEnrollmentTokens;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class EnrollmentTokenEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EnrollmentTokenEndpointsTests(PrIMAApiFactory factory)
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

    #region POST /api/detectors/enrollment-tokens (Generate)

    [Fact]
    public async Task GenerateEnrollmentToken_WithoutAuthentication_Returns401Unauthorized()
    {
        var request = new GenerateEnrollmentTokenRequest("Test Label");
        var response = await _client.PostAsJsonAsync("/api/detectors/enrollment-tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerateEnrollmentToken_WithValidLabel_GeneratesToken_HashesInDb_Returns201Created()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var label = "Pilot Lab Edge Detector Token";
        var request = new GenerateEnrollmentTokenRequest(label);

        // Act
        var response = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GenerateEnrollmentTokenResponse>(content, JsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.Token.Should().StartWith("PRM-TOKEN-");
        result.TokenPrefix.Should().StartWith("PRM-TOKEN-");
        result.Prefix.Should().Be(result.TokenPrefix);
        result.Label.Should().Be(label);
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));
        result.ExpiresAtUtc.Should().BeBefore(DateTime.UtcNow.AddHours(25));

        // Verify Database Persistence:
        // Plaintext token must NEVER be stored in the database; strictly SHA-256 hash
        using var conn = _factory.CreateDbConnection();
        var dbRecord = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.DetectorEnrollmentTokens WHERE Id = @Id",
            new { Id = result.Id });

        Assert.NotNull(dbRecord);
        string dbTokenHash = (string)dbRecord!.TokenHash;
        string dbPrefix = (string)dbRecord.TokenPrefix;
        string dbStatus = (string)dbRecord.Status;
        string dbOfficer = (string)dbRecord.CreatedByOfficer;

        dbPrefix.Should().Be(result.TokenPrefix);
        dbStatus.Should().Be("Pending");
        dbOfficer.Should().NotBeNullOrWhiteSpace();

        // Calculate expected SHA-256 of the returned plaintext token
        var expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(result.Token));
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        dbTokenHash.Should().Be(expectedHash);
        dbTokenHash.Should().NotBe(result.Token); // Ensure hash is not the plaintext token
    }

    [Fact]
    public async Task GenerateEnrollmentToken_WithNullLabel_SucceedsWithNullLabel()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new GenerateEnrollmentTokenRequest(null);

        // Act
        var response = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GenerateEnrollmentTokenResponse>(content, JsonOptions);
        result.Should().NotBeNull();
        result!.Label.Should().BeNull();
    }

    [Fact]
    public async Task GenerateEnrollmentToken_WithLabelExceeding100Chars_Returns422UnprocessableEntity()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var excessiveLabel = new string('A', 101);
        var request = new GenerateEnrollmentTokenRequest(excessiveLabel);

        // Act
        var response = await authClient.PostAsJsonAsync("/api/detectors/enrollment-tokens", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    #endregion

    #region GET /api/detectors/enrollment-tokens (List Pending)

    [Fact]
    public async Task GetPendingEnrollmentTokens_WithoutAuthentication_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/detectors/enrollment-tokens");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPendingEnrollmentTokens_ReturnsOnlyPendingAndUnexpiredTokens()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        // Generate an active pending token via API
        var activeLabel = $"Active-Pending-{Guid.NewGuid():N}";
        var genResponse = await authClient.PostAsJsonAsync(
            "/api/detectors/enrollment-tokens",
            new GenerateEnrollmentTokenRequest(activeLabel));
        genResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var generated = JsonSerializer.Deserialize<GenerateEnrollmentTokenResponse>(
            await genResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        // Manually insert an expired pending token, a consumed token, and a revoked token
        using var conn = _factory.CreateDbConnection();
        var expiredId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        var revokedId = Guid.NewGuid();

        await conn.ExecuteAsync("""
            INSERT INTO dbo.DetectorEnrollmentTokens (Id, TokenPrefix, TokenHash, Label, Status, ExpiresAtUtc, CreatedByOfficer, CreatedAtUtc)
            VALUES 
                (@ExpiredId, 'PRM-TOKEN-exp', @ExpiredHash, 'Expired Token', 'Pending', DATEADD(hour, -2, SYSUTCDATETIME()), 'Test Officer', DATEADD(hour, -26, SYSUTCDATETIME())),
                (@ConsumedId, 'PRM-TOKEN-con', @ConsumedHash, 'Consumed Token', 'Consumed', DATEADD(hour, 12, SYSUTCDATETIME()), 'Test Officer', SYSUTCDATETIME()),
                (@RevokedId, 'PRM-TOKEN-rev', @RevokedHash, 'Revoked Token', 'Revoked', DATEADD(hour, 12, SYSUTCDATETIME()), 'Test Officer', SYSUTCDATETIME());
            """,
            new 
            { 
                ExpiredId = expiredId, 
                ExpiredHash = $"hash-exp-{Guid.NewGuid():N}",
                ConsumedId = consumedId, 
                ConsumedHash = $"hash-con-{Guid.NewGuid():N}",
                RevokedId = revokedId,
                RevokedHash = $"hash-rev-{Guid.NewGuid():N}"
            });

        // Act
        var listResponse = await authClient.GetAsync("/api/detectors/enrollment-tokens");

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await listResponse.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<List<PendingEnrollmentTokenDto>>(content, JsonOptions);
        tokens.Should().NotBeNull();

        // The active token must be in the list
        tokens!.Should().Contain(t => t.Id == generated.Id && t.Label == activeLabel && t.Status == "Pending");

        // Expired, Consumed, and Revoked tokens must NOT be present
        tokens.Should().NotContain(t => t.Id == expiredId);
        tokens.Should().NotContain(t => t.Id == consumedId);
        tokens.Should().NotContain(t => t.Id == revokedId);
    }

    #endregion

    #region DELETE /api/detectors/enrollment-tokens/{id} (Revoke)

    [Fact]
    public async Task RevokeEnrollmentToken_WithoutAuthentication_Returns401Unauthorized()
    {
        var response = await _client.DeleteAsync($"/api/detectors/enrollment-tokens/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeEnrollmentToken_WhenPending_UpdatesStatusToRevoked_Returns204NoContent()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        var genResponse = await authClient.PostAsJsonAsync(
            "/api/detectors/enrollment-tokens",
            new GenerateEnrollmentTokenRequest("Token to Revoke"));
        genResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var generated = JsonSerializer.Deserialize<GenerateEnrollmentTokenResponse>(
            await genResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        // Act
        var revokeResponse = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{generated.Id}");

        // Assert
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify status in DB is Revoked
        using var conn = _factory.CreateDbConnection();
        var status = await conn.ExecuteScalarAsync<string>(
            "SELECT Status FROM dbo.DetectorEnrollmentTokens WHERE Id = @Id",
            new { Id = generated.Id });
        status.Should().Be("Revoked");

        // Verify it is no longer in pending list
        var listResponse = await authClient.GetAsync("/api/detectors/enrollment-tokens");
        var list = JsonSerializer.Deserialize<List<PendingEnrollmentTokenDto>>(
            await listResponse.Content.ReadAsStringAsync(), JsonOptions)!;
        list.Should().NotContain(t => t.Id == generated.Id);
    }

    [Fact]
    public async Task RevokeEnrollmentToken_WhenNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        var response = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{nonExistentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeEnrollmentToken_WhenAlreadyConsumed_Returns409Conflict()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var consumedId = Guid.NewGuid();

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.DetectorEnrollmentTokens (Id, TokenPrefix, TokenHash, Label, Status, ExpiresAtUtc, CreatedByOfficer, CreatedAtUtc, ConsumedAtUtc)
                VALUES (@Id, 'PRM-TOKEN-con', @TokenHash, 'Consumed Token', 'Consumed', DATEADD(hour, 12, SYSUTCDATETIME()), 'Test Officer', SYSUTCDATETIME(), SYSUTCDATETIME());
                """,
                new { Id = consumedId, TokenHash = $"hash-consumed-{Guid.NewGuid():N}" });
        }

        // Act
        var response = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{consumedId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevokeEnrollmentToken_WhenAlreadyRevoked_Returns409Conflict()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        var genResponse = await authClient.PostAsJsonAsync(
            "/api/detectors/enrollment-tokens",
            new GenerateEnrollmentTokenRequest("Revoke Twice Test"));
        genResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var generated = JsonSerializer.Deserialize<GenerateEnrollmentTokenResponse>(
            await genResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        // Revoke once
        var firstRevoke = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{generated.Id}");
        firstRevoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Revoke again
        var secondRevoke = await authClient.DeleteAsync($"/api/detectors/enrollment-tokens/{generated.Id}");

        // Assert
        secondRevoke.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion
}
