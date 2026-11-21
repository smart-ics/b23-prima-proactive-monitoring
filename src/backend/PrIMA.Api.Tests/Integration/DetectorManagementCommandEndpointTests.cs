using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.RegisterDetector;
using PrIMA.Api.Features.Detectors.ToggleDetectorStatus;
using PrIMA.Api.Features.Detectors.UpdateDetector;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorManagementCommandEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DetectorManagementCommandEndpointTests(PrIMAApiFactory factory)
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

    #region POST /api/detectors (Register)

    [Fact]
    public async Task RegisterDetector_WithoutAuthentication_Returns401Unauthorized()
    {
        var request = new RegisterDetectorRequest(
            Name: "Unauth-Detector",
            Description: "Test Unauth",
            Scope: "DataIntegrity",
            TargetModule: "Billing",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: null);

        var response = await _client.PostAsJsonAsync("/api/detectors", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterDetector_WithValidData_Returns201CreatedAndPersists()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var uniqueName = $"Detector-Reg-{Guid.NewGuid():N}";
        var request = new RegisterDetectorRequest(
            Name: uniqueName,
            Description: "Monitors daily reconciliation table integrity",
            Scope: "DataIntegrity",
            TargetModule: "Finance",
            ExpectedCadenceSeconds: 600,
            WebhookUrl: "https://probes.simrs.local/detectors/finance-recon");

        var response = await authClient.PostAsJsonAsync("/api/detectors", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var detail = await response.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().NotBeEmpty();
        detail.Name.Should().Be(uniqueName);
        detail.Description.Should().Be("Monitors daily reconciliation table integrity");
        detail.Scope.Should().Be("DataIntegrity");
        detail.TargetModule.Should().Be("Finance");
        detail.ExpectedCadenceSeconds.Should().Be(600);
        detail.WebhookUrl.Should().Be("https://probes.simrs.local/detectors/finance-recon");
        detail.AdminStatus.Should().Be("Active");
        detail.HealthStatus.Should().Be("Healthy");
        detail.ReliabilityStats.Should().NotBeNull();
        detail.ReliabilityStats.PassRate30d.Should().Be(100.0m);
        detail.RecentRuns.Should().BeEmpty();
        detail.ActiveIssues.Should().BeEmpty();

        // Verify in database
        using var conn = _factory.CreateDbConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Detectors WHERE Id = @Id AND Name = @Name",
            new { detail.Id, detail.Name });
        count.Should().Be(1);
    }

    [Fact]
    public async Task RegisterDetector_WhenDuplicateName_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var duplicateName = $"Detector-Dup-{Guid.NewGuid():N}";
        var request1 = new RegisterDetectorRequest(
            Name: duplicateName,
            Description: "Initial registration",
            Scope: "ProcessIntegrity",
            TargetModule: "Pharmacy",
            ExpectedCadenceSeconds: 120,
            WebhookUrl: null);

        var response1 = await authClient.PostAsJsonAsync("/api/detectors", request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var request2 = new RegisterDetectorRequest(
            Name: duplicateName,
            Description: "Duplicate attempt",
            Scope: "DataSync",
            TargetModule: "Pharmacy",
            ExpectedCadenceSeconds: 240,
            WebhookUrl: null);

        var response2 = await authClient.PostAsJsonAsync("/api/detectors", request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response2.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Contain("name already exists");
    }

    [Theory]
    [InlineData("", "Valid description", "DataIntegrity", "Module", 100)] // Empty name
    [InlineData("Valid Name", "", "DataIntegrity", "Module", 100)] // Empty description
    [InlineData("Valid Name", "Valid description", "InvalidScopeName", "Module", 100)] // Invalid scope
    [InlineData("Valid Name", "Valid description", "DataIntegrity", "", 100)] // Empty target module
    [InlineData("Valid Name", "Valid description", "DataIntegrity", "Module", 0)] // Zero cadence
    [InlineData("Valid Name", "Valid description", "DataIntegrity", "Module", -10)] // Negative cadence
    public async Task RegisterDetector_WithValidationErrors_Returns422UnprocessableEntity(
        string name, string description, string scope, string targetModule, int cadence)
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new RegisterDetectorRequest(
            Name: name,
            Description: description,
            Scope: scope,
            TargetModule: targetModule,
            ExpectedCadenceSeconds: cadence,
            WebhookUrl: null);

        var response = await authClient.PostAsJsonAsync("/api/detectors", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RegisterDetector_WithInvalidWebhookUrl_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new RegisterDetectorRequest(
            Name: "Invalid-Webhook-Detector",
            Description: "Detector with invalid webhook",
            Scope: "DataSync",
            TargetModule: "Radiology",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: "not-a-valid-url");

        var response = await authClient.PostAsJsonAsync("/api/detectors", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    #endregion

    #region PUT /api/detectors/{id} (Update)

    [Fact]
    public async Task UpdateDetector_WithoutAuthentication_Returns401Unauthorized()
    {
        var request = new UpdateDetectorRequest(
            Description: "Updated description",
            Scope: "DataSync",
            TargetModule: "Laboratory",
            ExpectedCadenceSeconds: 450,
            WebhookUrl: null);

        var response = await _client.PutAsJsonAsync($"/api/detectors/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDetector_WhenNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new UpdateDetectorRequest(
            Description: "Updated description",
            Scope: "DataSync",
            TargetModule: "Laboratory",
            ExpectedCadenceSeconds: 450,
            WebhookUrl: null);

        var response = await authClient.PutAsJsonAsync($"/api/detectors/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDetector_WithValidData_Returns204NoContentAndPreservesName()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. Create a detector first
        var uniqueName = $"Detector-Update-{Guid.NewGuid():N}";
        var createRequest = new RegisterDetectorRequest(
            Name: uniqueName,
            Description: "Original description",
            Scope: "DataIntegrity",
            TargetModule: "Outpatient",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: null);

        var createResponse = await authClient.PostAsJsonAsync("/api/detectors", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        created.Should().NotBeNull();

        // 2. Update detector configuration
        var updateRequest = new UpdateDetectorRequest(
            Description: "New updated description for outpatient",
            Scope: "ProcessIntegrity",
            TargetModule: "Outpatient-Emergency",
            ExpectedCadenceSeconds: 150,
            WebhookUrl: "https://probes.simrs.local/detectors/outpatient");

        var updateResponse = await authClient.PutAsJsonAsync($"/api/detectors/{created!.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Verify changes in DB, confirming Name remained unchanged
        using var conn = _factory.CreateDbConnection();
        var row = await conn.QuerySingleAsync(
            "SELECT Name, Description, Scope, TargetModule, ExpectedCadenceSeconds, WebhookUrl FROM dbo.Detectors WHERE Id = @Id",
            new { created.Id });

        ((string)row.Name).Should().Be(uniqueName);
        ((string)row.Description).Should().Be("New updated description for outpatient");
        ((string)row.Scope).Should().Be("ProcessIntegrity");
        ((string)row.TargetModule).Should().Be("Outpatient-Emergency");
        ((int)row.ExpectedCadenceSeconds).Should().Be(150);
        ((string)row.WebhookUrl).Should().Be("https://probes.simrs.local/detectors/outpatient");
    }

    [Fact]
    public async Task UpdateDetector_WhenRetired_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. Create detector
        var uniqueName = $"Detector-Retired-{Guid.NewGuid():N}";
        var createRequest = new RegisterDetectorRequest(
            Name: uniqueName,
            Description: "To be retired",
            Scope: "Infrastructure",
            TargetModule: "SysAdmin",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: null);

        var createResponse = await authClient.PostAsJsonAsync("/api/detectors", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);

        // 2. Retire detector
        var toggleResponse = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created!.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Retire"));
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Attempt update
        var updateRequest = new UpdateDetectorRequest(
            Description: "Trying to update retired detector",
            Scope: "Infrastructure",
            TargetModule: "SysAdmin",
            ExpectedCadenceSeconds: 600,
            WebhookUrl: null);

        var updateResponse = await authClient.PutAsJsonAsync($"/api/detectors/{created.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await updateResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("retired detector");
    }

    #endregion

    #region POST /api/detectors/{id}/toggle-status (Toggle Status)

    [Fact]
    public async Task ToggleDetectorStatus_WithoutAuthentication_Returns401Unauthorized()
    {
        var request = new ToggleDetectorStatusRequest("Pause");
        var response = await _client.PostAsJsonAsync($"/api/detectors/{Guid.NewGuid()}/toggle-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToggleDetectorStatus_WhenNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new ToggleDetectorStatusRequest("Pause");
        var response = await authClient.PostAsJsonAsync($"/api/detectors/{Guid.NewGuid()}/toggle-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleDetectorStatus_WithInvalidAction_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var request = new ToggleDetectorStatusRequest("InvalidActionName");
        var response = await authClient.PostAsJsonAsync($"/api/detectors/{Guid.NewGuid()}/toggle-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ToggleDetectorStatus_LifecycleTransitions_PauseResumeRetire()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. Create active detector
        var uniqueName = $"Detector-Toggle-{Guid.NewGuid():N}";
        var createRequest = new RegisterDetectorRequest(
            Name: uniqueName,
            Description: "Test pause, resume, retire",
            Scope: "DataIntegrity",
            TargetModule: "Inpatient",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: null);

        var createResponse = await authClient.PostAsJsonAsync("/api/detectors", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);

        using var conn = _factory.CreateDbConnection();

        // 2. Pause detector
        var pauseResponse = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created!.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Pause"));
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var status1 = await conn.ExecuteScalarAsync<string>(
            "SELECT AdminStatus FROM dbo.Detectors WHERE Id = @Id", new { created.Id });
        status1.Should().Be("Paused");

        // 3. Pausing an already paused detector returns 409 Conflict
        var secondPause = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Pause"));
        secondPause.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. Resume detector
        var resumeResponse = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Resume"));
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var status2 = await conn.ExecuteScalarAsync<string>(
            "SELECT AdminStatus FROM dbo.Detectors WHERE Id = @Id", new { created.Id });
        status2.Should().Be("Active");

        // 5. Resuming an already active detector returns 409 Conflict
        var secondResume = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Resume"));
        secondResume.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 6. Retire detector
        var retireResponse = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Retire"));
        retireResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var status3 = await conn.ExecuteScalarAsync<string>(
            "SELECT AdminStatus FROM dbo.Detectors WHERE Id = @Id", new { created.Id });
        status3.Should().Be("Retired");

        // 7. Any subsequent status change on a retired detector returns 409 Conflict
        var pauseRetired = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Pause"));
        pauseRetired.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var resumeRetired = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Resume"));
        resumeRetired.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var retireAgain = await authClient.PostAsJsonAsync(
            $"/api/detectors/{created.Id}/toggle-status",
            new ToggleDetectorStatusRequest("Retire"));
        retireAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion
}
