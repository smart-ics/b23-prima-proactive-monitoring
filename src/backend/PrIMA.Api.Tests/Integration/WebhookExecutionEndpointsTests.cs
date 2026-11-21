using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Detectors.TriggerManualRun;
using PrIMA.Api.Features.Issues.TriggerVerificationRun;
using PrIMA.Api.Infrastructure.Webhooks;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class WebhookExecutionEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public WebhookExecutionEndpointsTests(PrIMAApiFactory factory)
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

    private async Task<HttpClient> CreateMockedAuthenticatedClientAsync(IDetectorWebhookClient mockClient)
    {
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IDetectorWebhookClient>(_ => mockClient);
            });
        });

        var client = customFactory.CreateClient();
        var token = await _factory.GetAuthTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedDetectorWithWebhookAsync(
        string name = "Pharmacy Stock Probe",
        string? webhookUrl = "https://probes.internal/api/pharmacy",
        string adminStatus = "Active",
        string healthStatus = "Healthy")
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.Detectors (
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
            ) VALUES (
                @Id, @Name, 'Probe description', 'DataIntegrity', 'Pharmacy', 300,
                @WebhookUrl, @AdminStatus, @HealthStatus, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME()
            );
            """;

        await _factory.ExecuteSqlAsync(sql, new
        {
            Id = id,
            Name = name,
            WebhookUrl = webhookUrl,
            AdminStatus = adminStatus,
            HealthStatus = healthStatus
        });

        return id;
    }

    private async Task<Guid> SeedIssueWithRemediationAsync(
        Guid detectorId,
        string status = "Investigating",
        bool includeRemediation = true)
    {
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        const string issueSql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc
            ) VALUES (
                @Id, @CorrelationKey, 'Stock Variance Detected', @DetectorId, 'Pharmacy Stock Probe', 'DataIntegrity',
                'High', 'High', 3, @Status, 0,
                1, 'Ahmad Budi', @Now, @Now
            );
            """;

        await _factory.ExecuteSqlAsync(issueSql, new
        {
            Id = issueId,
            CorrelationKey = $"PHARM-VAR-{Guid.NewGuid():N}",
            DetectorId = detectorId,
            Status = status,
            Now = now
        });

        if (includeRemediation)
        {
            const string remSql = """
                INSERT INTO dbo.RemediationRecords (
                    Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
                ) VALUES (
                    @Id, @IssueId, 'PhysicalStockAdjustment', 'Recounted penicillin vials and adjusted physical stock.', 'Ahmad Budi', @Now, 0
                );
                """;

            await _factory.ExecuteSqlAsync(remSql, new
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                Now = now
            });
        }

        return issueId;
    }

    // =========================================================================
    // TriggerManualRun Endpoint Tests (POST /api/detectors/{id}/trigger-run)
    // =========================================================================

    [Fact]
    public async Task TriggerManualRun_WithoutAuthorization_Returns401Unauthorized()
    {
        var response = await _client.PostAsync($"/api/detectors/{Guid.NewGuid()}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TriggerManualRun_WhenDetectorNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var missingId = Guid.NewGuid();

        var response = await authClient.PostAsync($"/api/detectors/{missingId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task TriggerManualRun_WhenDetectorRetired_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync(adminStatus: "Retired");

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("retired");
    }

    [Fact]
    public async Task TriggerManualRun_WhenDetectorPaused_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync(adminStatus: "Paused");

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("paused");
    }

    [Fact]
    public async Task TriggerManualRun_WhenNoWebhookConfigured_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync(webhookUrl: null);

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("webhook URL");
    }

    [Fact]
    public async Task TriggerManualRun_WhenWebhookTimesOut_Returns504GatewayTimeout()
    {
        var mockWebhook = new MockWebhookClient(
            Result<WebhookExecutionResult>.Failure(WebhookErrors.Timeout("The external probe timed out after 15 seconds.")));

        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Gateway Timeout");
        problem.Status.Should().Be(StatusCodes.Status504GatewayTimeout);
    }

    [Fact]
    public async Task TriggerManualRun_WhenWebhookFails_Returns502BadGateway()
    {
        var mockWebhook = new MockWebhookClient(
            Result<WebhookExecutionResult>.Failure(WebhookErrors.Unreachable("HTTP 500 Internal Server Error.")));

        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Bad Gateway");
        problem.Status.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task TriggerManualRun_OnSuccess_PersistsRunUpdatesHealthAndReturns200OK()
    {
        var executionResult = new WebhookExecutionResult(
            Outcome: "Passed",
            DurationMs: 142,
            FailureReason: null,
            Findings: Array.Empty<WebhookFindingDto>());

        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Success(executionResult));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync(healthStatus: "Failing");

        var response = await authClient.PostAsync($"/api/detectors/{detectorId}/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultDto = await response.Content.ReadFromJsonAsync<InspectionRunResultDto>(JsonOptions);
        resultDto.Should().NotBeNull();
        resultDto!.RunId.Should().NotBeEmpty();
        resultDto.Outcome.Should().Be("Passed");
        resultDto.DurationMs.Should().Be(142);
        resultDto.FailureReason.Should().BeNull();
        resultDto.FindingsCount.Should().Be(0);

        // Verify InspectionRun persisted in database
        var runRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.InspectionRuns WHERE Id = @Id", new { Id = resultDto.RunId });
        ((object?)runRow).Should().NotBeNull();
        ((string)runRow!.TriggerSource).Should().Be("Manual");
        ((string)runRow!.Outcome).Should().Be("Passed");

        // Verify detector health updated to Healthy
        var detectorRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id", new { Id = detectorId });
        ((string)detectorRow!.HealthStatus).Should().Be("Healthy");
    }

    // =========================================================================
    // TriggerVerificationRun Endpoint Tests (POST /api/issues/{id}/verification/trigger-run)
    // =========================================================================

    [Fact]
    public async Task TriggerVerificationRun_WithoutAuthorization_Returns401Unauthorized()
    {
        var response = await _client.PostAsync($"/api/issues/{Guid.NewGuid()}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenIssueNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var missingId = Guid.NewGuid();

        var response = await authClient.PostAsync($"/api/issues/{missingId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenIssueNotInvestigating_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Acknowledged");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Investigating");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenNoRemediationRecorded_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating", includeRemediation: false);

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("remediation");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenDetectorHasNoWebhook_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await SeedDetectorWithWebhookAsync(webhookUrl: null);
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("webhook URL");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenCleanPass_UnlocksVerificationGateAndAppendsAuditLog()
    {
        var executionResult = new WebhookExecutionResult(
            Outcome: "Passed",
            DurationMs: 95,
            FailureReason: null,
            Findings: Array.Empty<WebhookFindingDto>());

        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Success(executionResult));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultDto = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        resultDto.Should().NotBeNull();
        resultDto!.IssueId.Should().Be(issueId);
        resultDto.VerificationMethod.Should().Be("AutomatedReRun");
        resultDto.CleanPassed.Should().BeTrue();
        resultDto.GateUnlocked.Should().BeTrue();
        resultDto.EvidenceNotes.Should().Contain($"Automated verification run '{resultDto.RunId}' completed with zero findings.");

        // Verify VerificationRecord inserted in DB
        var verRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE Id = @Id", new { Id = resultDto.VerificationId });
        ((object?)verRow).Should().NotBeNull();
        ((bool)verRow!.CleanPassed).Should().BeTrue();
        ((string)verRow!.VerificationMethod).Should().Be("AutomatedReRun");

        // Verify InspectionRun inserted in DB with TriggerSource = AutomatedReRun
        var runRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.InspectionRuns WHERE Id = @Id", new { Id = resultDto.RunId });
        ((object?)runRow).Should().NotBeNull();
        ((string)runRow!.TriggerSource).Should().Be("AutomatedReRun");

        // Verify ActivityLog inserted
        var activityLog = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'VerificationRecorded'",
            new { IssueId = issueId });
        ((object?)activityLog).Should().NotBeNull();
        ((string)activityLog!.Summary).Should().Contain("CleanPassed");

        // BR-PRIMA-004 Verification Gate Check: Resolving the issue should now SUCCEED!
        var resolveResponse = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify issue status is Resolved
        var resolvedStatus = await _factory.QuerySingleOrDefaultSqlAsync<string>(
            "SELECT Status FROM dbo.Issues WHERE Id = @Id", new { Id = issueId });
        resolvedStatus.Should().Be("Resolved");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenFindingDiscovered_KeepsVerificationGateLocked()
    {
        var findings = new[]
        {
            new WebhookFindingDto(
                TargetEntityReference: "SKU-992",
                Title: "Remaining Discrepancy",
                BaseSeverity: "High",
                Evidence: new WebhookEvidenceDto("Pharmacy", "dbo.Stocks", "SKU=992", 100m, 90m, 10m, "Discrepancy still exists"))
        };

        var executionResult = new WebhookExecutionResult(
            Outcome: "FindingDiscovered",
            DurationMs: 110,
            FailureReason: null,
            Findings: findings);

        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Success(executionResult));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultDto = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        resultDto.Should().NotBeNull();
        resultDto!.CleanPassed.Should().BeFalse();
        resultDto.GateUnlocked.Should().BeFalse();
        resultDto.EvidenceNotes.Should().Contain("discovered 1 finding(s). Verification failed.");

        // BR-PRIMA-004 Verification Gate Check: Resolving the issue must FAIL with 409 Conflict
        var resolveResponse = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await resolveResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("verification gate");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenWebhookTimesOut_Returns504GatewayTimeout()
    {
        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Failure(WebhookErrors.Timeout()));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(504);
        problem.Detail.Should().Contain("timed out after 15 seconds");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenWebhookFails_Returns502BadGateway()
    {
        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Failure(WebhookErrors.Unreachable("HTTP 502 Bad Gateway")));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(502);
        problem.Detail.Should().Contain("HTTP 502 Bad Gateway");
    }

    [Fact]
    public async Task TriggerVerificationRun_WhenExecutionFailed_KeepsVerificationGateLocked()
    {
        var executionResult = new WebhookExecutionResult(
            Outcome: "ExecutionFailed",
            DurationMs: 450,
            FailureReason: "Probe internal exception",
            Findings: Array.Empty<WebhookFindingDto>());

        var mockWebhook = new MockWebhookClient(Result<WebhookExecutionResult>.Success(executionResult));
        var authClient = await CreateMockedAuthenticatedClientAsync(mockWebhook);
        var detectorId = await SeedDetectorWithWebhookAsync();
        var issueId = await SeedIssueWithRemediationAsync(detectorId, status: "Investigating");

        var response = await authClient.PostAsync($"/api/issues/{issueId}/verification/trigger-run", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultDto = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        resultDto.Should().NotBeNull();
        resultDto!.CleanPassed.Should().BeFalse();
        resultDto.GateUnlocked.Should().BeFalse();
        resultDto.EvidenceNotes.Should().Contain("failed execution: Probe internal exception");

        // BR-PRIMA-004 Verification Gate Check: Resolving the issue must FAIL with 409 Conflict
        var resolveResponse = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed class MockWebhookClient : IDetectorWebhookClient
    {
        private readonly Result<WebhookExecutionResult> _resultToReturn;

        public MockWebhookClient(Result<WebhookExecutionResult> resultToReturn)
        {
            _resultToReturn = resultToReturn;
        }

        public Task<Result<WebhookExecutionResult>> ExecuteRunAsync(
            Guid detectorId,
            string webhookUrl,
            string triggerSource,
            Guid? issueId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_resultToReturn);
        }
    }
}
