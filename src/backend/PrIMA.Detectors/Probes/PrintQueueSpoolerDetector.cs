using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Pilot SIMRS detector probe for monitoring remote print server queues and spoolers (DEC-007, BR-PRIMA-007).
/// Flags station print queues when spooler status is not 'Online' or pending jobs exceed 300 seconds age.
/// </summary>
public sealed class PrintQueueSpoolerDetector : IDetectorProbe
{
    public const string ProbeName = "Print Queue Spooler Detector";
    public const string ProbeModule = "PrintSpooler";
    public const string ProbeScope = "Infrastructure";
    public const int DefaultCadenceSeconds = 60;
    public const string ProbeWebhookKey = "print-queue-spooler";

    public const string InspectionQuery = @"
        SELECT station_queue_name AS TargetEntityReference,
               hospital_unit AS HospitalUnit,
               printer_name AS PrinterName,
               pending_jobs AS PendingJobs,
               oldest_job_age_seconds AS OldestJobAgeSeconds,
               spooler_status AS SpoolerStatus
        FROM simrs.infra_print_queues
        WHERE spooler_status <> 'Online'
           OR (pending_jobs > 0 AND oldest_job_age_seconds >= 300);";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Monitors hospital station print queues for spooler offline errors or stalled print jobs exceeding 300 seconds.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public PrintQueueSpoolerDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<PrintQueueRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "All hospital station print spoolers and queues are online and operating normally.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"Print Queue Spooler Anomaly: {r.TargetEntityReference} ({r.PrinterName})",
            baseSeverity: "Critical",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.infra_print_queues",
                recordKeys: $"station_queue_name={r.TargetEntityReference}",
                debitTotal: null,
                creditTotal: null,
                varianceDelta: null,
                trace: $"Unit: {r.HospitalUnit}, Printer: {r.PrinterName}, Spooler Status: '{r.SpoolerStatus}', Pending Jobs: {r.PendingJobs}, Oldest Job Age: {r.OldestJobAgeSeconds}s."
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} print queue spooler anomaly(ies).");
    }

    public sealed record PrintQueueRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public string HospitalUnit { get; init; } = string.Empty;
        public string PrinterName { get; init; } = string.Empty;
        public int PendingJobs { get; init; }
        public int OldestJobAgeSeconds { get; init; }
        public string SpoolerStatus { get; init; } = string.Empty;
    }
}
