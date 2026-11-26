namespace PrIMA.Detectors.Sdk;

/// <summary>
/// Defines the standard contract for a PrIMA detector probe.
/// </summary>
public interface IDetectorProbe
{
    /// <summary>
    /// Probe manifest metadata declaring identity, target module, scope, and expected cadence.
    /// </summary>
    DetectorManifest Manifest { get; }

    /// <summary>
    /// Executes the inspection routine against target systems and returns a telemetry report.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A telemetry report with the inspection outcome and any findings.</returns>
    Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default);
}
