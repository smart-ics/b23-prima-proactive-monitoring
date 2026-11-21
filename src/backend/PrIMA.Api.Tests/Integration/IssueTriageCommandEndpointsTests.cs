using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Issues.AssignIssue;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class IssueTriageCommandEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IssueTriageCommandEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _factory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private async Task<(Guid IssueId, Guid DetectorId)> SeedIssueAsync(
        string status = "Open",
        string? currentAssignee = null,
        DateTime? acknowledgedAtUtc = null)
    {
        var detectorId = await _factory.SeedDetectorAsync(name: $"Detector-{Guid.NewGuid():N}");
        var inspectionRunId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
            VALUES (@Id, @DetectorId, @Now, 'Scheduled', 'FindingDiscovered', 100, @Now);
            """,
            new { Id = inspectionRunId, DetectorId = detectorId, Now = now });

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @Key, 'Triage Test Issue', @DetectorId, 'Triage Test Detector', 'DataIntegrity',
                'High', 'High', 3, @Status, 0,
                1, @CurrentAssignee, @Now, @Now,
                @AcknowledgedAtUtc, NULL, NULL, NULL
            );
            """,
            new
            {
                Id = issueId,
                Key = $"TEST:TRIAGE:{issueId:N}",
                DetectorId = detectorId,
                Status = status,
                CurrentAssignee = currentAssignee,
                Now = now,
                AcknowledgedAtUtc = acknowledgedAtUtc
            });

        return (issueId, detectorId);
    }

    // =========================================================================
    // POST /api/issues/{id}/take-it
    // =========================================================================

    [Fact]
    public async Task TakeIt_WithoutAuthentication_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsync($"/api/issues/{issueId}/take-it", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TakeIt_WhenIssueNotFound_Returns404NotFoundProblemDetails()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        var response = await authClient.PostAsync($"/api/issues/{nonExistentId}/take-it", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TakeIt_WhenIssueClosed_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Resolved");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/take-it", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TakeIt_WhenIssueOpen_TransitionsToInvestigatingAndWritesAuditLog()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Open");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/take-it", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Issue record updated
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee, AcknowledgedAtUtc FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Investigating");
        ((string)issue.CurrentAssignee).Should().Be("Ahmad Budi");
        ((DateTime?)issue.AcknowledgedAtUtc).Should().NotBeNull();

        // Verify IssueActivityLogs record inserted
        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("InvestigationStarted");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
        ((string)activityLog.Summary).Should().Contain("via Take It");
    }

    // =========================================================================
    // POST /api/issues/{id}/assign
    // =========================================================================

    [Fact]
    public async Task Assign_WithoutAuthentication_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_WhenOfficerIdIsEmpty_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Open");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(Guid.Empty));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Assign_WhenOfficerDoesNotExistOrInactive_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Open");
        var nonExistentOfficerId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(nonExistentOfficerId));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.Should().Contain(nonExistentOfficerId.ToString());
    }

    [Fact]
    public async Task Assign_WhenIssueNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        using var conn = _factory.CreateDbConnection();
        var officerId = await conn.QuerySingleAsync<Guid>("SELECT Id FROM dbo.Officers WHERE Username = 'siti.rahma'");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{Guid.NewGuid()}/assign", new AssignIssueRequest(officerId));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_WhenIssueClosed_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Dismissed");
        using var conn = _factory.CreateDbConnection();
        var officerId = await conn.QuerySingleAsync<Guid>("SELECT Id FROM dbo.Officers WHERE Username = 'siti.rahma'");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(officerId));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Assign_WhenIssueOpen_TransitionsToAcknowledgedAndSetsAssignee()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Open");
        using var conn = _factory.CreateDbConnection();
        var sitiId = await conn.QuerySingleAsync<Guid>("SELECT Id FROM dbo.Officers WHERE Username = 'siti.rahma'");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(sitiId));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Issue record updated
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee, AcknowledgedAtUtc FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Acknowledged");
        ((string)issue.CurrentAssignee).Should().Be("Siti Rahma");
        ((DateTime?)issue.AcknowledgedAtUtc).Should().NotBeNull();

        // Verify IssueActivityLogs record inserted
        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("Assigned");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
        ((string)activityLog.Summary).Should().Contain("Siti Rahma");
    }

    [Fact]
    public async Task Assign_WhenIssueInvestigating_PreservesInvestigatingStatus_OQ007()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Investigating", currentAssignee: "Ahmad Budi");
        using var conn = _factory.CreateDbConnection();
        var sitiId = await conn.QuerySingleAsync<Guid>("SELECT Id FROM dbo.Officers WHERE Username = 'siti.rahma'");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(sitiId));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Issue record updated without state regression
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Investigating");
        ((string)issue.CurrentAssignee).Should().Be("Siti Rahma");

        // Verify IssueActivityLogs record inserted as Reassigned
        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("Reassigned");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
        ((string)activityLog.Summary).Should().Contain("reassigned from Ahmad Budi to Siti Rahma");
    }

    // =========================================================================
    // POST /api/issues/{id}/acknowledge
    // =========================================================================

    [Fact]
    public async Task Acknowledge_WithoutAuthentication_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsync($"/api/issues/{issueId}/acknowledge", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Acknowledge_WhenIssueNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var response = await authClient.PostAsync($"/api/issues/{Guid.NewGuid()}/acknowledge", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Acknowledge_WhenIssueAlreadyInvestigating_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/acknowledge", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Acknowledge_WhenIssueOpen_TransitionsToAcknowledged()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Open");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/acknowledge", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify DB
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee, AcknowledgedAtUtc FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Acknowledged");
        ((string)issue.CurrentAssignee).Should().Be("Ahmad Budi");
        ((DateTime?)issue.AcknowledgedAtUtc).Should().NotBeNull();

        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("Acknowledged");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
    }

    // =========================================================================
    // POST /api/issues/{id}/start-investigation
    // =========================================================================

    [Fact]
    public async Task StartInvestigation_WithoutAuthentication_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsync($"/api/issues/{issueId}/start-investigation", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartInvestigation_WhenIssueNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var response = await authClient.PostAsync($"/api/issues/{Guid.NewGuid()}/start-investigation", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartInvestigation_WhenIssueOpen_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(status: "Open");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/start-investigation", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StartInvestigation_WhenAcknowledgedWithoutAssignee_AssignsCaller()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Acknowledged", currentAssignee: null, acknowledgedAtUtc: DateTime.UtcNow);

        var response = await authClient.PostAsync($"/api/issues/{issueId}/start-investigation", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify DB
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Investigating");
        ((string)issue.CurrentAssignee).Should().Be("Ahmad Budi");

        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("InvestigationStarted");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
    }

    [Fact]
    public async Task StartInvestigation_WhenAcknowledgedWithExistingAssignee_PreservesAssignee()
    {
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");
        var (issueId, _) = await SeedIssueAsync(status: "Acknowledged", currentAssignee: "Siti Rahma", acknowledgedAtUtc: DateTime.UtcNow);

        var response = await authClient.PostAsync($"/api/issues/{issueId}/start-investigation", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify DB
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleAsync<dynamic>(
            "SELECT Status, CurrentAssignee FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });

        ((string)issue.Status).Should().Be("Investigating");
        ((string)issue.CurrentAssignee).Should().Be("Siti Rahma");

        var activityLog = await conn.QuerySingleAsync<dynamic>(
            "SELECT ActivityType, ActorName FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = issueId });

        ((string)activityLog.ActivityType).Should().Be("InvestigationStarted");
        ((string)activityLog.ActorName).Should().Be("Ahmad Budi");
    }
}
