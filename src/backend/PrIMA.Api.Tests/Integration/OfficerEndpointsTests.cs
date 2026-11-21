using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using PrIMA.Api.Features.Officers;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class OfficerEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OfficerEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    [Fact]
    public async Task GetOfficers_WithoutAuthentication_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/officers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOfficers_WithAuthentication_ReturnsActiveOfficersOrderedByFullNameAscending()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        // Act
        var response = await authClient.GetAsync("/api/officers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var officers = await response.Content.ReadFromJsonAsync<List<OfficerDto>>(JsonOptions);
        officers.Should().NotBeNull();
        officers!.Count.Should().BeGreaterOrEqualTo(3);

        // Verify ordering by FullName ASC
        var fullNames = officers.Select(o => o.FullName).ToList();
        fullNames.Should().BeInAscendingOrder();

        // Verify seeded active officers exist
        officers.Should().Contain(o => o.Username == "ahmad.budi" && o.FullName == "Ahmad Budi" && o.Role == "EdpOfficer");
        officers.Should().Contain(o => o.Username == "edp.lead" && o.FullName == "EDP Lead" && o.Role == "Lead");
        officers.Should().Contain(o => o.Username == "siti.rahma" && o.FullName == "Siti Rahma" && o.Role == "SystemAnalyst");
    }

    [Fact]
    public async Task GetOfficers_ExcludesInactiveOfficers()
    {
        // Arrange
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Officers (Id, Username, FullName, PasswordHash, Role, IsActive)
            VALUES (@Id, @Username, @FullName, 'hash', 'EdpOfficer', 0);
            """,
            new
            {
                Id = Guid.NewGuid(),
                Username = "inactive.user",
                FullName = "ZZZ Inactive User"
            });

        var authClient = await CreateAuthenticatedClientAsync();

        // Act
        var response = await authClient.GetAsync("/api/officers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var officers = await response.Content.ReadFromJsonAsync<List<OfficerDto>>(JsonOptions);
        officers.Should().NotBeNull();
        officers.Should().NotContain(o => o.Username == "inactive.user");
    }
}
