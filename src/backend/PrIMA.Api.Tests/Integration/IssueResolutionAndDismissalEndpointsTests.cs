using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Issues.DismissIssue;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class IssueResolutionAndDismissalEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record IssueStatusRow(string Status, DateTime? ResolvedAtUtc, DateTime? DismissedAtUtc);
    private sealed record ActivityLogRow(string ActivityType, string ActorName, string Summary, string? DetailsJson);
    private sealed record DismissalRecordRow(string DismissalReason, string JustificationRationale, string DismissedBy, bool IsSuperseded);

    public IssueResolutionAndDismissalEndpointsTests(PrIMAApiFactory factory)
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

    private async Task<(Guid IssueId, Guid DetectorId)> SeedIssueAsync(
        string status = "Investigating",
        string? currentAssignee = "Ahmad Budi",
        DateTime? resolvedAtUtc = null,
        DateTime? dismissedAtUtc = null)
    {
        var detectorId = await _factory.SeedDetectorAsync($"Detector-{Guid.NewGuid():N}");
        var issueId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        const string runSql = """
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
            VALUES (@Id, @DetectorId, @Now, 'ScheduledTimer', 'FailureWithAnomalies', 120, @Now);
            """;
        await _factory.ExecuteSqlAsync(runSql, new { Id = runId, DetectorId = detectorId, Now = now });

        const string issueSql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, 'DataIntegrity',
                'High', 'High', 3, @Status, 0,
                1, @CurrentAssignee, @Now, @Now,
                @Now, @ResolvedAtUtc, @DismissedAtUtc, NULL
            );
            """;

        await _factory.ExecuteSqlAsync(issueSql, new
        {
            Id = issueId,
            CorrelationKey = $"KEY-{issueId:N}",
            Title = "Test Issue for Resolution / Dismissal",
            DetectorId = detectorId,
            DetectorName = "Test Billing Detector",
            Status = status,
            CurrentAssignee = currentAssignee,
            Now = now,
            ResolvedAtUtc = resolvedAtUtc,
            DismissedAtUtc = dismissedAtUtc
        });

        return (issueId, detectorId);
    }

    private async Task SeedVerificationRecordAsync(Guid issueId, bool cleanPassed, bool isSuperseded)
    {
        const string sql = """
            INSERT INTO dbo.VerificationRecords (
                Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                @Id, @IssueId, 'ManualQuery', @CleanPassed, 'Verification query check result', 'Ahmad Budi', SYSUTCDATETIME(), @IsSuperseded
            );
            """;

        await _factory.ExecuteSqlAsync(sql, new
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            CleanPassed = cleanPassed,
            IsSuperseded = isSuperseded
        });
    }

    #region Resolve Endpoint Tests

    [Fact]
    public async Task Resolve_WithoutAuthentication_Returns401Unauthorized()
    {
        var (issueId, _) = await SeedIssueAsync("Investigating");

        var response = await _client.PostAsync($"/api/issues/{issueId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resolve_NonExistentIssue_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        var response = await authClient.PostAsync($"/api/issues/{nonExistentId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolve_WhenIssueNotInInvestigatingStatus_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Open");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resolve_WhenNoVerificationRecordExists_Returns409ConflictWithMandatoryVerificationGateMessage()
    {
        // Arrange - BR-PRIMA-004 verification gate enforcement
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Investigating");

        // Act
        var response = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Mandatory verification gate requires a clean, passed verification record");
    }

    [Fact]
    public async Task Resolve_WhenVerificationRecordFailed_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Investigating");
        await SeedVerificationRecordAsync(issueId, cleanPassed: false, isSuperseded: false);

        var response = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resolve_WhenVerificationRecordSuperseded_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Investigating");
        await SeedVerificationRecordAsync(issueId, cleanPassed: true, isSuperseded: true);

        var response = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resolve_WhenActiveCleanPassedVerificationRecordExists_ResolvesIssueAndWritesAuditLog()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Investigating");
        await SeedVerificationRecordAsync(issueId, cleanPassed: true, isSuperseded: false);

        // Act
        var response = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);

        // Assert - 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Check dbo.Issues
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleOrDefaultAsync<IssueStatusRow>(
            "SELECT Status, ResolvedAtUtc, DismissedAtUtc FROM dbo.Issues WHERE Id = @Id", new { Id = issueId });
        issue.Should().NotBeNull();
        issue!.Status.Should().Be("Resolved");
        issue.ResolvedAtUtc.Should().NotBeNull();

        // Check dbo.IssueActivityLogs
        var activityLog = await conn.QuerySingleOrDefaultAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'Resolved'",
            new { Id = issueId });
        activityLog.Should().NotBeNull();
        activityLog!.ActivityType.Should().Be("Resolved");
        activityLog.ActorName.Should().Be("Ahmad Budi");
        activityLog.Summary.Should().Contain("clean verification passed");
    }

    #endregion

    #region Dismiss Endpoint Tests

    [Fact]
    public async Task Dismiss_WithoutAuthentication_Returns401Unauthorized()
    {
        var (issueId, _) = await SeedIssueAsync("Open");
        var request = new DismissIssueRequest("FalsePositive", "This is an expected test anomaly that is safe to dismiss.");

        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/dismiss", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dismiss_NonExistentIssue_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();
        var request = new DismissIssueRequest("FalsePositive", "This is an expected test anomaly that is safe to dismiss.");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{nonExistentId}/dismiss", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dismiss_InvalidReasonEnum_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Open");
        var request = new DismissIssueRequest("InvalidNonExistentReason", "This is an expected test anomaly that is safe to dismiss.");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/dismiss", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Dismiss_RationaleUnder20Characters_Returns422UnprocessableEntity()
    {
        // Arrange - BR-PRIMA-006: Rationale length must be >= 20 chars
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync("Open");
        var request = new DismissIssueRequest("FalsePositive", "Too short");

        // Act
        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/dismiss", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("Resolved")]
    [InlineData("Dismissed")]
    public async Task Dismiss_WhenIssueAlreadyClosed_Returns409Conflict(string closedStatus)
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(
            status: closedStatus,
            resolvedAtUtc: closedStatus == "Resolved" ? DateTime.UtcNow : null,
            dismissedAtUtc: closedStatus == "Dismissed" ? DateTime.UtcNow : null);

        var request = new DismissIssueRequest("FalsePositive", "This is an expected test anomaly that is safe to dismiss.");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/dismiss", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Acknowledged")]
    [InlineData("Investigating")]
    [InlineData("Reopened")]
    public async Task Dismiss_WhenIssueActiveWithValidData_DismissesIssueInsertsRecordAndWritesAuditLog(string activeStatus)
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var (issueId, _) = await SeedIssueAsync(activeStatus);
        const string justification = "This anomaly is confirmed as an accepted operational exception during migration.";
        var request = new DismissIssueRequest("AcceptedOperationalException", justification);

        // Act
        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/dismiss", request);

        // Assert - 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Check dbo.Issues
        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleOrDefaultAsync<IssueStatusRow>(
            "SELECT Status, ResolvedAtUtc, DismissedAtUtc FROM dbo.Issues WHERE Id = @Id", new { Id = issueId });
        issue.Should().NotBeNull();
        issue!.Status.Should().Be("Dismissed");
        issue.DismissedAtUtc.Should().NotBeNull();

        // Check dbo.DismissalRecords
        var dismissalRecord = await conn.QuerySingleOrDefaultAsync<DismissalRecordRow>(
            "SELECT DismissalReason, JustificationRationale, DismissedBy, IsSuperseded FROM dbo.DismissalRecords WHERE IssueId = @Id",
            new { Id = issueId });
        dismissalRecord.Should().NotBeNull();
        dismissalRecord!.DismissalReason.Should().Be("AcceptedOperationalException");
        dismissalRecord.JustificationRationale.Should().Be(justification);
        dismissalRecord.DismissedBy.Should().Be("Ahmad Budi");
        dismissalRecord.IsSuperseded.Should().BeFalse();

        // Check dbo.IssueActivityLogs
        var activityLog = await conn.QuerySingleOrDefaultAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'Dismissed'",
            new { Id = issueId });
        activityLog.Should().NotBeNull();
        activityLog!.ActivityType.Should().Be("Dismissed");
        activityLog.ActorName.Should().Be("Ahmad Budi");
        activityLog.Summary.Should().Contain("AcceptedOperationalException");
        activityLog.DetailsJson.Should().Contain("AcceptedOperationalException");
    }

    #endregion
}
