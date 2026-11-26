using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Continuous daemon background service executing periodic cadence timers for registered probes (DEC-005).
/// </summary>
public sealed class DetectorDaemonWorker : BackgroundService
{
    private readonly IEnumerable<IDetectorProbe> _probes;
    private readonly IBootstrapHandshakeService _handshakeService;
    private readonly IDetectorRunner _detectorRunner;
    private readonly ILogger<DetectorDaemonWorker> _logger;

    public DetectorDaemonWorker(
        IEnumerable<IDetectorProbe> probes,
        IBootstrapHandshakeService handshakeService,
        IDetectorRunner detectorRunner,
        ILogger<DetectorDaemonWorker>? logger = null)
    {
        _probes = probes ?? throw new ArgumentNullException(nameof(probes));
        _handshakeService = handshakeService ?? throw new ArgumentNullException(nameof(handshakeService));
        _detectorRunner = detectorRunner ?? throw new ArgumentNullException(nameof(detectorRunner));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DetectorDaemonWorker>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var probeList = _probes.ToList();
        _logger.LogInformation("PrIMA Detector Daemon Worker starting with {Count} registered probe(s)...", probeList.Count);

        if (probeList.Count == 0)
        {
            _logger.LogWarning("No detector probes registered in daemon worker. Waiting for shutdown.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var tasks = probeList.Select(probe => RunProbeCadenceLoopAsync(probe, stoppingToken)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task RunProbeCadenceLoopAsync(IDetectorProbe probe, CancellationToken stoppingToken)
    {
        DetectorCredential? credential = null;
        try
        {
            credential = await _handshakeService.EnsureCredentialsAsync(probe, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve credentials for probe '{ProbeName}'. Cadence loop will not start.", probe.Manifest.Name);
            return;
        }

        if (credential == null)
        {
            _logger.LogError("Credentials missing for probe '{ProbeName}'. Cadence loop aborted.", probe.Manifest.Name);
            return;
        }

        var cadenceSeconds = Math.Max(30, probe.Manifest.ExpectedCadenceSeconds);
        _logger.LogInformation("Detector probe '{ProbeName}' registered for cadence interval: {Cadence}s.",
            probe.Manifest.Name, cadenceSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(cadenceSeconds));

        // Initial execution pass on startup
        try
        {
            await ExecutePassAsync(probe, credential, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in initial execution pass for probe '{ProbeName}'.", probe.Manifest.Name);
        }

        // Periodic cadence timer loop
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecutePassAsync(probe, credential, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Cadence loop gracefully stopped for probe '{ProbeName}'.", probe.Manifest.Name);
        }
    }

    private async Task ExecutePassAsync(IDetectorProbe probe, DetectorCredential credential, CancellationToken ct)
    {
        _logger.LogDebug("Executing scheduled cadence inspection for '{ProbeName}'...", probe.Manifest.Name);

        var result = await _detectorRunner.RunAndIngestAsync(
            probe,
            credential.DetectorId,
            credential.ApiKey,
            triggerSource: "Scheduled",
            serverUrl: credential.ServerUrl,
            ct: ct);

        if (result.Report.Outcome == TelemetryOutcome.ExecutionFailed)
        {
            _logger.LogError("Scheduled pass for probe '{ProbeName}' failed: {Error}",
                probe.Manifest.Name, result.Report.ErrorMessage);
        }
        else
        {
            _logger.LogInformation("Scheduled pass for probe '{ProbeName}' completed: {Outcome} ({DurationMs}ms).",
                probe.Manifest.Name, result.Report.Outcome, result.Report.ExecutionDurationMs);
        }

        if (result.IngestionResponse?.Warnings?.Count > 0)
        {
            foreach (var warning in result.IngestionResponse.Warnings)
            {
                _logger.LogWarning("Cadence drift warning for '{ProbeName}': {Warning}",
                    probe.Manifest.Name, warning);
            }
        }
    }
}
