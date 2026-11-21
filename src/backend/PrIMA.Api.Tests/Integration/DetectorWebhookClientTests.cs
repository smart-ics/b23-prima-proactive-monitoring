using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Infrastructure.Persistence;
using PrIMA.Api.Infrastructure.Webhooks;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorWebhookClientTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly IDetectorRepository _detectorRepository;
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DetectorWebhookClientTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        var scope = factory.Services.CreateScope();
        _detectorRepository = scope.ServiceProvider.GetRequiredService<IDetectorRepository>();
        _issueRepository = scope.ServiceProvider.GetRequiredService<IIssueRepository>();
        _sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Detector> InsertSampleDetectorAsync(string name = "Pharmacy Stock Probe")
    {
        var detector = new Detector(
            Guid.NewGuid(),
            name,
            "Validates daily physical vs ledger drug counts",
            MonitoringScope.DataIntegrity,
            "Pharmacy",
            300,
            "https://hospital.local/probes/pharmacy-sync",
            AdminStatus.Active,
            DetectorHealth.Healthy,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(-2));

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        await _detectorRepository.InsertAsync(detector, conn, tx);
        tx.Commit();

        return detector;
    }

    [Fact]
    public async Task ExecuteRunAsync_On200Success_ReturnsDeserializedWebhookExecutionResult()
    {
        var detector = await InsertSampleDetectorAsync("Billing Webhook Probe");
        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        bool hasDeliveryId = false;
        string? userAgent = null;

        var mockHandler = new TestHttpMessageHandler(async request =>
        {
            capturedMethod = request.Method;
            hasDeliveryId = request.Headers.Contains("X-PrIMA-Delivery-Id");
            userAgent = request.Headers.UserAgent.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync();

            var responsePayload = new
            {
                outcome = "Passed",
                durationMs = 85,
                failureReason = (string?)null,
                findings = new[]
                {
                    new
                    {
                        targetEntityReference = "BILL-901",
                        title = "Invoice Discrepancy",
                        baseSeverity = "Medium",
                        evidence = new
                        {
                            module = "Billing",
                            sourceTable = "dbo.Invoices",
                            recordKeys = "InvoiceId=901",
                            debitTotal = 1000m,
                            creditTotal = 950m,
                            varianceDelta = 50m,
                            trace = "Ledger mismatch detected"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responsePayload)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var client = new DetectorWebhookClient(
            httpClient,
            _detectorRepository,
            _issueRepository,
            _sqlConnectionFactory,
            NullLogger<DetectorWebhookClient>.Instance);

        var result = await client.ExecuteRunAsync(
            detector.Id,
            detector.WebhookUrl!,
            "Manual",
            issueId: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be("Passed");
        result.Value.DurationMs.Should().Be(85);
        result.Value.Findings.Should().HaveCount(1);
        result.Value.Findings[0].TargetEntityReference.Should().Be("BILL-901");
        result.Value.Findings[0].Title.Should().Be("Invoice Discrepancy");
        result.Value.Findings[0].BaseSeverity.Should().Be("Medium");
        result.Value.Findings[0].Evidence.DebitTotal.Should().Be(1000m);

        capturedMethod.Should().Be(HttpMethod.Post);
        hasDeliveryId.Should().BeTrue();
        userAgent.Should().Contain("PrIMA-Supervisor/1.0");

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain(detector.Id.ToString());
        capturedBody.Should().Contain("Manual");

        // Verify detector health was not affected
        var reloaded = await _detectorRepository.GetByIdAsync(detector.Id, transaction: null);
        reloaded!.HealthStatus.Should().Be(DetectorHealth.Healthy);
    }

    [Fact]
    public async Task ExecuteRunAsync_OnTimeout_RecordsExecutionFailedRun_MarksFailing_AndSynthesizesFailIssue()
    {
        var detector = await InsertSampleDetectorAsync("Timeout Probe");

        var mockHandler = new TestHttpMessageHandler(_ =>
            throw new TaskCanceledException("The operation was canceled due to timeout."));

        var httpClient = new HttpClient(mockHandler);
        var client = new DetectorWebhookClient(
            httpClient,
            _detectorRepository,
            _issueRepository,
            _sqlConnectionFactory,
            NullLogger<DetectorWebhookClient>.Instance);

        var result = await client.ExecuteRunAsync(
            detector.Id,
            detector.WebhookUrl!,
            "Manual");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Webhooks.Timeout");
        result.Error.StatusCode.Should().Be(504);

        // 1. Detector marked Failing
        var reloadedDetector = await _detectorRepository.GetByIdAsync(detector.Id, transaction: null);
        reloadedDetector!.HealthStatus.Should().Be(DetectorHealth.Failing);

        // 2. InspectionRun recorded with ExecutionFailed
        using var conn = _factory.CreateDbConnection();
        var run = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId",
            new { DetectorId = detector.Id });
        ((object?)run).Should().NotBeNull();
        ((string)run!.Outcome).Should().Be("ExecutionFailed");
        ((string)run.FailureReason).Should().Contain("timed out");

        // 3. Infrastructure issue synthesized (FAIL:{detectorId})
        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key",
            new { Key = $"FAIL:{detector.Id}" });
        ((object?)issue).Should().NotBeNull();
        ((string)issue!.Title).Should().Be($"Execution Failure: {detector.Name}");
        ((string)issue.Scope).Should().Be("Infrastructure");
        ((string)issue.BaseSeverity).Should().Be("High");
        ((int)issue.OccurrenceCount).Should().Be(1);

        // 4. Occurrence inserted
        var occurrence = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });
        ((object?)occurrence).Should().NotBeNull();
        ((string)occurrence!.Module).Should().Be("Pharmacy");

        // 5. Activity log inserted
        var log = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId",
            new { IssueId = (Guid)issue.Id });
        ((object?)log).Should().NotBeNull();
        ((string)log!.ActivityType).Should().Be("Created");
    }

    [Fact]
    public async Task ExecuteRunAsync_OnHttp500Error_RecordsExecutionFailedRun_MarksFailing_AndSynthesizesFailIssue()
    {
        var detector = await InsertSampleDetectorAsync("Unreachable Probe");

        var mockHandler = new TestHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Database connection failed inside detector probe service.")
            }));

        var httpClient = new HttpClient(mockHandler);
        var client = new DetectorWebhookClient(
            httpClient,
            _detectorRepository,
            _issueRepository,
            _sqlConnectionFactory,
            NullLogger<DetectorWebhookClient>.Instance);

        var result = await client.ExecuteRunAsync(
            detector.Id,
            detector.WebhookUrl!,
            "Manual");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Webhooks.Unreachable");
        result.Error.StatusCode.Should().Be(502);

        // 1. Detector marked Failing
        var reloadedDetector = await _detectorRepository.GetByIdAsync(detector.Id, transaction: null);
        reloadedDetector!.HealthStatus.Should().Be(DetectorHealth.Failing);

        // 2. InspectionRun recorded
        using var conn = _factory.CreateDbConnection();
        var run = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId",
            new { DetectorId = detector.Id });
        ((object?)run).Should().NotBeNull();
        ((string)run!.Outcome).Should().Be("ExecutionFailed");

        // 3. Issue FAIL:{detectorId} created
        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key",
            new { Key = $"FAIL:{detector.Id}" });
        ((object?)issue).Should().NotBeNull();
        ((string)issue!.CorrelationKey).Should().Be($"FAIL:{detector.Id}");
        ((int)issue.OccurrenceCount).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteRunAsync_OnRepeatedFailure_AppendsOccurrenceAndLogsRecurrence()
    {
        var detector = await InsertSampleDetectorAsync("Recurring Fail Probe");

        var mockHandler = new TestHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("Remote probe service is down.")
            }));

        var httpClient = new HttpClient(mockHandler);
        var client = new DetectorWebhookClient(
            httpClient,
            _detectorRepository,
            _issueRepository,
            _sqlConnectionFactory,
            NullLogger<DetectorWebhookClient>.Instance);

        // First failure -> Occurrence #1 (Created)
        var firstResult = await client.ExecuteRunAsync(detector.Id, detector.WebhookUrl!, "Manual");
        firstResult.IsFailure.Should().BeTrue();

        // Second failure -> Occurrence #2 (RecurrenceObserved)
        var secondResult = await client.ExecuteRunAsync(detector.Id, detector.WebhookUrl!, "Manual");
        secondResult.IsFailure.Should().BeTrue();

        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key",
            new { Key = $"FAIL:{detector.Id}" });
        ((object?)issue).Should().NotBeNull();
        ((int)issue!.OccurrenceCount).Should().Be(2);

        var occurrences = (await conn.QueryAsync<dynamic>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC",
            new { IssueId = (Guid)issue.Id })).ToList();
        occurrences.Should().HaveCount(2);
        ((int)occurrences[0].OccurrenceNumber).Should().Be(1);
        ((int)occurrences[1].OccurrenceNumber).Should().Be(2);

        var logs = (await conn.QueryAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC",
            new { IssueId = (Guid)issue.Id })).ToList();
        logs.Should().HaveCount(2);
        ((string)logs[0].ActivityType).Should().Be("Created");
        ((string)logs[1].ActivityType).Should().Be("RecurrenceObserved");
    }

    [Fact]
    public async Task ExecuteRunAsync_OnRepeatedFailureWhenClosed_ReopensIssue()
    {
        var detector = await InsertSampleDetectorAsync("Reopening Probe");

        var mockHandler = new TestHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var httpClient = new HttpClient(mockHandler);
        var client = new DetectorWebhookClient(
            httpClient,
            _detectorRepository,
            _issueRepository,
            _sqlConnectionFactory,
            NullLogger<DetectorWebhookClient>.Instance);

        // First failure creates issue
        await client.ExecuteRunAsync(detector.Id, detector.WebhookUrl!, "Manual");

        using var conn = _factory.CreateDbConnection();
        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key",
            new { Key = $"FAIL:{detector.Id}" });
        ((object?)issue).Should().NotBeNull();
        Guid issueId = (Guid)issue!.Id;

        // Manually mark issue as Resolved
        await conn.ExecuteAsync(
            "UPDATE dbo.Issues SET Status = 'Resolved', ResolvedAtUtc = SYSUTCDATETIME() WHERE Id = @Id",
            new { Id = issueId });

        // Second failure -> Reopens closed issue
        await client.ExecuteRunAsync(detector.Id, detector.WebhookUrl!, "Manual");

        var reloadedIssue = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = issueId });
        ((object?)reloadedIssue).Should().NotBeNull();
        ((string)reloadedIssue!.Status).Should().Be("Reopened");
        ((int)reloadedIssue.OccurrenceCount).Should().Be(2);
        ((DateTime?)reloadedIssue.ReopenedAtUtc).Should().NotBeNull();
        ((DateTime?)reloadedIssue.ResolvedAtUtc).Should().BeNull();

        var reopenedLog = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'Reopened'",
            new { IssueId = issueId });
        ((object?)reopenedLog).Should().NotBeNull();
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsync;

        public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request);
        }
    }
}
