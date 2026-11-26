using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PrIMA.Detectors.Sdk;

/// <summary>
/// Executes detector probes with stopwatch instrumentation and automatic exception trapping into ExecutionFailed.
/// </summary>
public interface IProbeExecutor
{
    /// <summary>
    /// Executes the specified probe, trapping any unhandled exception, SQL timeout, or cancellation into ExecutionFailed.
    /// </summary>
    Task<TelemetryReport> ExecuteAsync(IDetectorProbe probe, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of IProbeExecutor with automatic exception trapping.
/// </summary>
public sealed class ProbeExecutor : IProbeExecutor
{
    private readonly ILogger<ProbeExecutor>? _logger;

    public ProbeExecutor(ILogger<ProbeExecutor>? logger = null)
    {
        _logger = logger;
    }

    public async Task<TelemetryReport> ExecuteAsync(IDetectorProbe probe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var report = await probe.ExecuteInspectionAsync(ct);
            stopwatch.Stop();

            var durationMs = report.ExecutionDurationMs > 0
                ? report.ExecutionDurationMs
                : (int)stopwatch.ElapsedMilliseconds;

            return report with { ExecutionDurationMs = durationMs };
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            _logger?.LogWarning(ex, "Probe '{Name}' cancelled after {Duration}ms", probe.Manifest.Name, durationMs);

            return TelemetryReport.ExecutionFailed(
                errorMessage: $"Probe inspection timed out or was cancelled after {durationMs}ms.",
                traceString: ex.ToString(),
                durationMs: durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = (int)stopwatch.ElapsedMilliseconds;
            _logger?.LogError(ex, "Probe '{Name}' failed with unhandled exception after {Duration}ms", probe.Manifest.Name, durationMs);

            return TelemetryReport.ExecutionFailed(
                errorMessage: ex.Message,
                traceString: ex.ToString(),
                durationMs: durationMs);
        }
    }
}
