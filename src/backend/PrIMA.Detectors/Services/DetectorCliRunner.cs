using Microsoft.Extensions.Logging;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// CLI execution runner contract for scheduled task mode (--run-once).
/// </summary>
public interface IDetectorCliRunner
{
    /// <summary>
    /// Executes one inspection pass for all registered probes, ingests telemetry,
    /// and returns exit code 0 (success) or 1 (on failure) for Windows Task Scheduler / cron.
    /// </summary>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}

/// <summary>
/// Implements single-pass CLI execution (--run-once) for Windows Task Scheduler or cron jobs (DEC-005).
/// </summary>
public sealed class DetectorCliRunner : IDetectorCliRunner
{
    private readonly IEnumerable<IDetectorProbe> _probes;
    private readonly IBootstrapHandshakeService _handshakeService;
    private readonly IDetectorRunner _detectorRunner;
    private readonly ILogger<DetectorCliRunner> _logger;

    public DetectorCliRunner(
        IEnumerable<IDetectorProbe> probes,
        IBootstrapHandshakeService handshakeService,
        IDetectorRunner detectorRunner,
        ILogger<DetectorCliRunner>? logger = null)
    {
        _probes = probes ?? throw new ArgumentNullException(nameof(probes));
        _handshakeService = handshakeService ?? throw new ArgumentNullException(nameof(handshakeService));
        _detectorRunner = detectorRunner ?? throw new ArgumentNullException(nameof(detectorRunner));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DetectorCliRunner>.Instance;
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("PrIMA Reference Detector Runner executing in single-pass CLI mode (--run-once)...");

        var probeList = _probes.ToList();
        if (probeList.Count == 0)
        {
            _logger.LogWarning("No detector probes registered. Exiting with code 0.");
            return 0;
        }

        var anyFailed = false;

        foreach (var probe in probeList)
        {
            _logger.LogInformation("Executing CLI inspection for probe '{ProbeName}'...", probe.Manifest.Name);

            DetectorCredential? credential;
            try
            {
                credential = await _handshakeService.EnsureCredentialsAsync(probe, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve credentials for probe '{ProbeName}'.", probe.Manifest.Name);
                anyFailed = true;
                continue;
            }

            if (credential == null)
            {
                _logger.LogError("Credentials missing for probe '{ProbeName}'. Run cannot proceed.", probe.Manifest.Name);
                anyFailed = true;
                continue;
            }

            try
            {
                var result = await _detectorRunner.RunAndIngestAsync(
                    probe,
                    credential.DetectorId,
                    credential.ApiKey,
                    triggerSource: "Scheduled",
                    serverUrl: credential.ServerUrl,
                    ct: ct);

                if (result.Report.Outcome == TelemetryOutcome.ExecutionFailed)
                {
                    _logger.LogError("Probe '{ProbeName}' inspection failed: {Error}",
                        probe.Manifest.Name, result.Report.ErrorMessage);
                    anyFailed = true;
                }
                else
                {
                    _logger.LogInformation("Probe '{ProbeName}' completed with outcome '{Outcome}' ({DurationMs}ms, {FindingsCount} findings).",
                        probe.Manifest.Name,
                        result.Report.Outcome,
                        result.Report.ExecutionDurationMs,
                        result.Report.Findings.Count);
                }

                if (result.IngestionResponse?.Warnings?.Count > 0)
                {
                    foreach (var warning in result.IngestionResponse.Warnings)
                    {
                        _logger.LogWarning("Telemetry ingestion warning for probe '{ProbeName}': {Warning}",
                            probe.Manifest.Name, warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing or ingesting probe '{ProbeName}'.", probe.Manifest.Name);
                anyFailed = true;
            }
        }

        var exitCode = anyFailed ? 1 : 0;
        _logger.LogInformation("PrIMA CLI run-once execution completed with exit code {ExitCode}.", exitCode);
        return exitCode;
    }
}
