using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Auth;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class AuthEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_WithValidSeededOfficerCredentials_Returns200AndValidJwtWithExpectedClaims()
    {
        // Arrange - Using seeded baseline officer "ahmad.budi" from 002_SeedInitialUsers.sql
        var request = new LoginRequest("ahmad.budi", "Hospital@2026!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrWhiteSpace();
        loginResult.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        loginResult.Officer.Should().NotBeNull();
        loginResult.Officer.Username.Should().Be("ahmad.budi");
        loginResult.Officer.FullName.Should().Be("Ahmad Budi");
        loginResult.Officer.Role.Should().Be("EdpOfficer");

        // Validate JWT structure and claims
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.CanReadToken(loginResult.Token).Should().BeTrue();

        var jwtToken = tokenHandler.ReadJwtToken(loginResult.Token);
        jwtToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == loginResult.Officer.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == "name" && c.Value == "ahmad.budi");
        jwtToken.Claims.Should().Contain(c => c.Type == "fullName" && c.Value == "Ahmad Budi");
        jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == "EdpOfficer");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401ProblemDetails()
    {
        // Arrange
        var request = new LoginRequest("ahmad.budi", "IncorrectPassword123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().BeOneOf("application/problem+json", "application/json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Detail.Should().Be("Invalid username or password.");
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions["code"]?.ToString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithInactiveOfficer_Returns401ProblemDetails()
    {
        // Arrange - Insert an inactive officer into dbo.Officers
        var inactiveId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.Officers (Id, Username, FullName, PasswordHash, Role, IsActive)
            VALUES (
                @Id,
                'inactive.officer',
                'Inactive Officer',
                'AQAAAAIAAYagAAAAEMmdc3uiohYm2mW/zkhAOqopoll7Fo4OMVl1YNuSlCzK07wAumPvcrsQa/0J8Ry5Wg==',
                'EdpOfficer',
                0
            );
            """;
        await _factory.ExecuteSqlAsync(insertSql, new { Id = inactiveId });

        var request = new LoginRequest("inactive.officer", "Hospital@2026!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Detail.Should().Be("Account is deactivated. Contact EDP Lead.");
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions["code"]?.ToString().Should().Be("ACCOUNT_DEACTIVATED");
    }
}
