using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Result of running a detector probe and submitting its telemetry.
/// </summary>
public sealed record DetectorRunResult(TelemetryReport Report, IngestTelemetryResponse? IngestionResponse);

/// <summary>
/// Contract for orchestrating probe execution with automatic ExecutionFailed exception trapping and server submission.
/// </summary>
public interface IDetectorRunner
{
    /// <summary>
    /// Executes a probe with automatic exception trapping and submits telemetry to PrIMA.
    /// </summary>
    Task<DetectorRunResult> RunAndIngestAsync(
        IDetectorProbe probe,
        Guid detectorId,
        string apiKey,
        string triggerSource = "Scheduled",
        string? serverUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a probe using configured options and submits telemetry to PrIMA.
    /// </summary>
    Task<DetectorRunResult> RunAndIngestAsync(
        IDetectorProbe probe,
        string triggerSource = "Scheduled",
        CancellationToken ct = default);
}

/// <summary>
/// Implements automatic ExecutionFailed exception trapping and submission to PrIMA server (BR-PRIMA-005).
/// </summary>
public sealed class DetectorRunner : IDetectorRunner
{
    private readonly IProbeExecutor _probeExecutor;
    private readonly TelemetryIngestionClient _ingestionClient;
    private readonly IBootstrapHandshakeService? _handshakeService;
    private readonly DetectorSdkOptions _options;
    private readonly ILogger<DetectorRunner> _logger;

    public DetectorRunner(
        IProbeExecutor probeExecutor,
        TelemetryIngestionClient ingestionClient,
        IOptions<DetectorSdkOptions>? options = null,
        ILogger<DetectorRunner>? logger = null)
        : this(probeExecutor, ingestionClient, null, options, logger)
    {
    }

    public DetectorRunner(
        IProbeExecutor probeExecutor,
        TelemetryIngestionClient ingestionClient,
        IBootstrapHandshakeService? handshakeService = null,
        IOptions<DetectorSdkOptions>? options = null,
        ILogger<DetectorRunner>? logger = null)
    {
        _probeExecutor = probeExecutor ?? throw new ArgumentNullException(nameof(probeExecutor));
        _ingestionClient = ingestionClient ?? throw new ArgumentNullException(nameof(ingestionClient));
        _handshakeService = handshakeService;
        _options = options?.Value ?? new DetectorSdkOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DetectorRunner>.Instance;
    }

    public async Task<DetectorRunResult> RunAndIngestAsync(
        IDetectorProbe probe,
        Guid detectorId,
        string apiKey,
        string triggerSource = "Scheduled",
        string? serverUrl = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        TelemetryReport report;
        try
        {
            // Execute inspection via probe executor (which wraps timeouts and unhandled probe exceptions)
            report = await _probeExecutor.ExecuteAsync(probe, ct);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Execution of probe '{ProbeName}' was cancelled", probe.Manifest.Name);
            report = TelemetryReport.ExecutionFailed(
                errorMessage: $"Probe inspection cancelled or timed out: {ex.Message}",
                traceString: ex.ToString());
        }
        catch (Exception ex)
        {
            // Failsafe exception trapping (BR-PRIMA-005)
            _logger.LogError(ex, "Unhandled error during execution of probe '{ProbeName}'", probe.Manifest.Name);
            report = TelemetryReport.ExecutionFailed(
                errorMessage: ex.Message,
                traceString: ex.ToString());
        }

        IngestTelemetryResponse? ingestionResponse = null;
        try
        {
            // Submit telemetry report to PrIMA server
            ingestionResponse = await _ingestionClient.IngestAsync(
                detectorId,
                apiKey,
                report,
                triggerSource,
                serverUrl,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit telemetry for detector '{DetectorName}' ({DetectorId}) to PrIMA server",
                probe.Manifest.Name, detectorId);
        }

        return new DetectorRunResult(report, ingestionResponse);
    }

    public async Task<DetectorRunResult> RunAndIngestAsync(
        IDetectorProbe probe,
        string triggerSource = "Scheduled",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (_options.DetectorId.HasValue && _options.DetectorId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return await RunAndIngestAsync(
                probe,
                _options.DetectorId.Value,
                _options.ApiKey,
                triggerSource,
                _options.ServerUrl,
                ct);
        }

        if (_handshakeService != null)
        {
            var credential = await _handshakeService.EnsureCredentialsAsync(probe, ct);
            if (credential != null)
            {
                return await RunAndIngestAsync(
                    probe,
                    credential.DetectorId,
                    credential.ApiKey,
                    triggerSource,
                    credential.ServerUrl,
                    ct);
            }
        }

        throw new InvalidOperationException(
            $"DetectorId and ApiKey must be configured in DetectorSdkOptions or obtainable via BootstrapHandshakeService for probe '{probe.Manifest.Name}'.");
    }
}
