using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// HTTP client for ingesting telemetry reports into the PrIMA Server API.
/// </summary>
public class TelemetryIngestionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly DetectorSdkOptions _options;
    private readonly ILogger<TelemetryIngestionClient> _logger;

    public TelemetryIngestionClient(
        HttpClient httpClient,
        IOptions<DetectorSdkOptions>? options = null,
        ILogger<TelemetryIngestionClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new DetectorSdkOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetryIngestionClient>.Instance;
    }

    /// <summary>
    /// Ingests a telemetry report using explicitly provided detector ID and API key.
    /// </summary>
    public async Task<IngestTelemetryResponse?> IngestAsync(
        Guid detectorId,
        string apiKey,
        TelemetryReport report,
        string triggerSource = "Scheduled",
        string? serverUrl = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(report);

        if (detectorId == Guid.Empty)
        {
            throw new ArgumentException("Detector ID cannot be empty.", nameof(detectorId));
        }

        var requestPayload = BuildApiRequest(detectorId, report, triggerSource);
        var targetUri = ResolveEndpointUri(serverUrl);

        var maxRetries = Math.Max(1, _options.MaxRetries);
        var retryDelay = Math.Max(50, _options.RetryDelayMilliseconds);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
            {
                Content = JsonContent.Create(requestPayload, options: JsonOptions)
            };
            request.Headers.Add("X-Detector-API-Key", apiKey);

            try
            {
                var response = await _httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions, ct);

                    if (result?.Warnings != null && result.Warnings.Count > 0)
                    {
                        foreach (var warning in result.Warnings)
                        {
                            _logger.LogWarning("PrIMA Telemetry warning for detector {DetectorId}: {Warning}", detectorId, warning);
                        }
                    }

                    _logger.LogInformation(
                        "Telemetry ingested successfully for detector {DetectorId}. Outcome: {Outcome}, Run ID: {RunId}, Warnings: {WarningCount}",
                        detectorId, report.Outcome, result?.InspectionRunId, result?.Warnings?.Count ?? 0);

                    return result;
                }

                // If client error (4xx) other than 408 / 429, permanent error - do not retry
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500
                    && response.StatusCode != HttpStatusCode.RequestTimeout
                    && (int)response.StatusCode != 429)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError(
                        "PrIMA Telemetry ingestion rejected with status {StatusCode} for detector {DetectorId}: {Error}",
                        response.StatusCode, detectorId, errorBody);

                    throw new HttpRequestException(
                        $"Telemetry ingestion rejected with status {response.StatusCode}: {errorBody}",
                        null,
                        response.StatusCode);
                }

                // Server error (5xx) or 408/429: retry if attempts remain
                if (attempt == maxRetries)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError(
                        "PrIMA Telemetry ingestion failed after {MaxRetries} attempts with status {StatusCode}: {Error}",
                        maxRetries, response.StatusCode, errorBody);
                    response.EnsureSuccessStatusCode();
                }

                _logger.LogWarning(
                    "PrIMA Telemetry ingestion attempt {Attempt} returned transient status {StatusCode}. Retrying in {Delay}ms...",
                    attempt, response.StatusCode, retryDelay * attempt);

                await Task.Delay(retryDelay * attempt, ct);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries && (ex.StatusCode is null or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout or HttpStatusCode.BadGateway or HttpStatusCode.RequestTimeout))
            {
                _logger.LogWarning(
                    ex,
                    "PrIMA Telemetry ingestion attempt {Attempt} failed with network error. Retrying in {Delay}ms...",
                    attempt, retryDelay * attempt);

                await Task.Delay(retryDelay * attempt, ct);
            }
        }

        return null;
    }

    /// <summary>
    /// Ingests a telemetry report using configured SDK options.
    /// </summary>
    public Task<IngestTelemetryResponse?> IngestAsync(
        TelemetryReport report,
        string triggerSource = "Scheduled",
        CancellationToken ct = default)
    {
        if (!_options.DetectorId.HasValue || _options.DetectorId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("DetectorId must be configured in DetectorSdkOptions when invoking IngestAsync without explicit detector ID.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("ApiKey must be configured in DetectorSdkOptions when invoking IngestAsync without explicit API key.");
        }

        return IngestAsync(_options.DetectorId.Value, _options.ApiKey, report, triggerSource, _options.ServerUrl, ct);
    }

    private static object BuildApiRequest(Guid detectorId, TelemetryReport report, string triggerSource)
    {
        string? failureReason = null;
        IReadOnlyList<FindingItem>? findings = null;

        if (report.Outcome == TelemetryOutcome.ExecutionFailed)
        {
            var rawReason = !string.IsNullOrWhiteSpace(report.ErrorMessage)
                ? report.ErrorMessage
                : (report.TraceString ?? "Detector reported execution failure.");

            failureReason = rawReason.Length > 1000 ? rawReason[..997] + "..." : rawReason;
            findings = null;
        }
        else if (report.Outcome == TelemetryOutcome.FindingDiscovered)
        {
            findings = report.Findings;
        }
        else
        {
            // Passed
            findings = null;
        }

        return new
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = report.InspectionTimestampUtc.ToUniversalTime(),
            TriggerSource = triggerSource,
            Outcome = report.Outcome,
            DurationMs = Math.Max(0, report.ExecutionDurationMs),
            FailureReason = failureReason,
            Findings = findings
        };
    }

    private Uri ResolveEndpointUri(string? explicitServerUrl)
    {
        if (!string.IsNullOrWhiteSpace(explicitServerUrl))
        {
            return new Uri(new Uri(explicitServerUrl.TrimEnd('/') + "/"), "api/ingestion/telemetry");
        }

        if (_httpClient.BaseAddress != null)
        {
            return new Uri("api/ingestion/telemetry", UriKind.Relative);
        }

        var baseServer = string.IsNullOrWhiteSpace(_options.ServerUrl)
            ? "http://localhost:5000"
            : _options.ServerUrl.TrimEnd('/');

        return new Uri(new Uri(baseServer + "/"), "api/ingestion/telemetry");
    }
}
