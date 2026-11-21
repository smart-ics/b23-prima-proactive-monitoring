using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Infrastructure.Webhooks;

public sealed class DetectorWebhookClient : IDetectorWebhookClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IDetectorRepository _detectorRepository;
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<DetectorWebhookClient> _logger;

    public DetectorWebhookClient(
        HttpClient httpClient,
        IDetectorRepository detectorRepository,
        IIssueRepository issueRepository,
        ISqlConnectionFactory connectionFactory,
        ILogger<DetectorWebhookClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _detectorRepository = detectorRepository ?? throw new ArgumentNullException(nameof(detectorRepository));
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            if (_httpClient.Timeout != TimeSpan.FromSeconds(15))
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(15);
            }
        }
        catch (InvalidOperationException)
        {
            // Ignore if client is already configured or in-flight
        }

        try
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PrIMA-Supervisor/1.0");
            }
        }
        catch (Exception)
        {
            // Ignore header parsing exceptions
        }
    }

    public async Task<Result<WebhookExecutionResult>> ExecuteRunAsync(
        Guid detectorId,
        string webhookUrl,
        string triggerSource,
        Guid? issueId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || !Uri.TryCreate(webhookUrl, UriKind.Absolute, out _))
        {
            var invalidUrlReason = $"Invalid or empty webhook URL '{webhookUrl}'.";
            await RecordFailureAsync(detectorId, triggerSource, 0, invalidUrlReason, CancellationToken.None);
            return Result<WebhookExecutionResult>.Failure(WebhookErrors.Unreachable(invalidUrlReason));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = JsonContent.Create(
                    new
                    {
                        detectorId,
                        triggerSource,
                        issueId,
                        dispatchedAtUtc = DateTime.UtcNow
                    },
                    options: JsonOptions)
            };

            request.Headers.Add("X-PrIMA-Delivery-Id", Guid.NewGuid().ToString());

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = response.Content != null
                    ? await response.Content.ReadAsStringAsync(CancellationToken.None)
                    : string.Empty;
                var reason = $"Webhook returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorBody}";
                _logger.LogWarning("Webhook call for detector '{DetectorId}' failed with status code {StatusCode}: {Reason}", detectorId, (int)response.StatusCode, reason);

                await RecordFailureAsync(detectorId, triggerSource, durationMs, reason, CancellationToken.None);

                var error = response.StatusCode == HttpStatusCode.GatewayTimeout
                    ? WebhookErrors.Timeout(reason)
                    : WebhookErrors.Unreachable(reason);

                return Result<WebhookExecutionResult>.Failure(error);
            }

            var responseJson = response.Content != null
                ? await response.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;
            TelemetryResponsePayload? payload = null;

            if (!string.IsNullOrWhiteSpace(responseJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<TelemetryResponsePayload>(responseJson, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize JSON response from webhook for detector '{DetectorId}'.", detectorId);
                }
            }

            var outcome = !string.IsNullOrWhiteSpace(payload?.Outcome) ? payload.Outcome : "Passed";
            var finalDuration = payload?.DurationMs > 0 ? payload.DurationMs.Value : durationMs;
            var failureReason = payload?.FailureReason;
            var findings = (IReadOnlyList<WebhookFindingDto>)(payload?.Findings ?? (IReadOnlyList<WebhookFindingDto>)Array.Empty<WebhookFindingDto>());

            return Result<WebhookExecutionResult>.Success(new WebhookExecutionResult(outcome, finalDuration, failureReason, findings));
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            var failureReason = $"Webhook request timed out after 15 seconds: {ex.Message}";
            _logger.LogError(ex, "Webhook request timed out for detector '{DetectorId}'.", detectorId);

            await RecordFailureAsync(detectorId, triggerSource, durationMs, failureReason, CancellationToken.None);
            return Result<WebhookExecutionResult>.Failure(WebhookErrors.Timeout(failureReason));
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            var failureReason = $"Webhook request timed out after 15 seconds: {ex.Message}";
            _logger.LogError(ex, "Webhook request timed out for detector '{DetectorId}'.", detectorId);

            await RecordFailureAsync(detectorId, triggerSource, durationMs, failureReason, CancellationToken.None);
            return Result<WebhookExecutionResult>.Failure(WebhookErrors.Timeout(failureReason));
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            var failureReason = $"Webhook HTTP request failed: {ex.Message}";
            _logger.LogError(ex, "Webhook HTTP request failed for detector '{DetectorId}'.", detectorId);

            await RecordFailureAsync(detectorId, triggerSource, durationMs, failureReason, CancellationToken.None);
            return Result<WebhookExecutionResult>.Failure(WebhookErrors.Unreachable(failureReason));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            var failureReason = $"Unexpected webhook error: {ex.Message}";
            _logger.LogError(ex, "Unexpected webhook execution error for detector '{DetectorId}'.", detectorId);

            await RecordFailureAsync(detectorId, triggerSource, durationMs, failureReason, CancellationToken.None);
            return Result<WebhookExecutionResult>.Failure(WebhookErrors.Unreachable(failureReason));
        }
    }

    private async Task RecordFailureAsync(
        Guid detectorId,
        string triggerSource,
        int durationMs,
        string failureReason,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                if (connection is DbConnection dbConn)
                {
                    await dbConn.OpenAsync(cancellationToken);
                }
                else
                {
                    connection.Open();
                }
            }

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                var detector = await _detectorRepository.GetByIdAsync(detectorId, connection, transaction, cancellationToken);
                if (detector is null)
                {
                    _logger.LogWarning("Cannot record webhook failure: Detector '{DetectorId}' not found in database.", detectorId);
                    return;
                }

                var runTimeUtc = DateTime.UtcNow;
                var parsedTrigger = Enum.TryParse<TriggerSource>(triggerSource, true, out var ts) ? ts : TriggerSource.Manual;
                var safeFailureReason = failureReason.Length > 1000 ? failureReason[..1000] : failureReason;

                // 1. Record InspectionRun with Outcome = ExecutionFailed
                var inspectionRun = InspectionRun.Create(
                    detector.Id,
                    runTimeUtc,
                    parsedTrigger,
                    InspectionOutcome.ExecutionFailed,
                    durationMs,
                    safeFailureReason);

                await _detectorRepository.RecordInspectionRunAsync(inspectionRun, connection, transaction, cancellationToken);

                // 2. Update detector HealthStatus = Failing
                await _detectorRepository.UpdateHealthAndHeartbeatAsync(
                    detector.Id,
                    DetectorHealth.Failing,
                    lastRunUtc: runTimeUtc,
                    lastHealthyUtc: null,
                    connection,
                    transaction,
                    cancellationToken);

                // 3. Synthesize or append FAIL:{detectorId} infrastructure issue
                var correlationKeyStr = $"FAIL:{detector.Id}";
                var existingIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                    correlationKeyStr,
                    connection,
                    transaction,
                    cancellationToken);

                var evidence = new TelemetryEvidence(
                    module: string.IsNullOrWhiteSpace(detector.TargetModule) ? "System" : detector.TargetModule,
                    sourceTable: null,
                    recordKeys: null,
                    debitTotal: null,
                    creditTotal: null,
                    varianceDelta: null,
                    trace: failureReason);

                if (existingIssue is null)
                {
                    var correlationKey = CorrelationKey.FromCanonical(correlationKeyStr);
                    var title = $"Execution Failure: {detector.Name}";
                    if (title.Length > 250)
                    {
                        title = title[..250];
                    }

                    var newIssue = Issue.Create(
                        correlationKey,
                        title,
                        detector.Id,
                        detector.Name,
                        MonitoringScope.Infrastructure,
                        IssueSeverity.High,
                        inspectionRun.Id,
                        evidence,
                        runTimeUtc);

                    try
                    {
                        await _issueRepository.InsertIssueAsync(newIssue, connection, transaction, cancellationToken);
                        await _issueRepository.InsertOccurrenceAsync(newIssue.Occurrences[0], connection, transaction, cancellationToken);
                        await _issueRepository.InsertActivityLogAsync(
                            connection,
                            transaction,
                            newIssue.Id,
                            "Created",
                            "System",
                            TruncateSummary($"Infrastructure anomaly synthesized due to execution failure: {safeFailureReason}."),
                            detailsJson: null,
                            cancellationToken: cancellationToken);
                    }
                    catch (SqlException ex) when (ex.Number is 2601 or 2627)
                    {
                        existingIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                            correlationKeyStr,
                            connection,
                            transaction,
                            cancellationToken);

                        if (existingIssue is not null)
                        {
                            await ProcessExistingFailureIssueAsync(
                                existingIssue,
                                inspectionRun.Id,
                                evidence,
                                runTimeUtc,
                                safeFailureReason,
                                connection,
                                transaction,
                                cancellationToken);
                        }
                    }
                }
                else
                {
                    await ProcessExistingFailureIssueAsync(
                        existingIssue,
                        inspectionRun.Id,
                        evidence,
                        runTimeUtc,
                        safeFailureReason,
                        connection,
                        transaction,
                        cancellationToken);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to persist webhook failure records for detector '{DetectorId}'.", detectorId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection error while recording webhook failure for detector '{DetectorId}'.", detectorId);
        }
    }

    private async Task ProcessExistingFailureIssueAsync(
        Issue issue,
        Guid inspectionRunId,
        TelemetryEvidence evidence,
        DateTime runTimeUtc,
        string safeFailureReason,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var isClosed = issue.IsClosed;
        var occurrence = issue.RecordRecurringOccurrence(inspectionRunId, evidence, runTimeUtc);

        if (isClosed)
        {
            await _issueRepository.ReopenIssueAsync(issue, connection, transaction, cancellationToken);
            await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);
            await _issueRepository.InsertActivityLogAsync(
                connection,
                transaction,
                issue.Id,
                "Reopened",
                "System",
                TruncateSummary($"Execution failure recurred on previously closed issue. Issue reopened (Occurrence #{occurrence.OccurrenceNumber}). Reason: {safeFailureReason}."),
                detailsJson: null,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _issueRepository.UpdateRecurringIssueAsync(issue, connection, transaction, cancellationToken);
            await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);

            if (issue.Status == IssueStatus.Investigating)
            {
                await _issueRepository.SupersedeActiveVerificationsAsync(issue.Id, connection, transaction, cancellationToken);
                await _issueRepository.InsertActivityLogAsync(
                    connection,
                    transaction,
                    issue.Id,
                    "VerificationInvalidated",
                    "System",
                    TruncateSummary($"Execution failure recurred during investigation (Occurrence #{occurrence.OccurrenceNumber}). Active verifications superseded."),
                    detailsJson: null,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _issueRepository.InsertActivityLogAsync(
                    connection,
                    transaction,
                    issue.Id,
                    "RecurrenceObserved",
                    "System",
                    TruncateSummary($"Execution failure recurred (Occurrence #{occurrence.OccurrenceNumber}). Reason: {safeFailureReason}."),
                    detailsJson: null,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static string TruncateSummary(string summary) =>
        summary.Length > 500 ? summary[..500] : summary;

    private sealed class TelemetryResponsePayload
    {
        public Guid? DetectorId { get; set; }
        public DateTime? ExecutionTimestampUtc { get; set; }
        public string? TriggerSource { get; set; }
        public string? Outcome { get; set; }
        public int? DurationMs { get; set; }
        public string? FailureReason { get; set; }
        public List<WebhookFindingDto>? Findings { get; set; }
    }
}
