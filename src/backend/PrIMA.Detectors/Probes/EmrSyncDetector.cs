using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Pilot SIMRS detector probe for monitoring EMR synchronization logs (DEC-007, BR-PRIMA-007).
/// Flags failed transmissions or stalled pending synchronizations exceeding 30 minutes.
/// </summary>
public sealed class EmrSyncDetector : IDetectorProbe
{
    public const string ProbeName = "EMR Sync Detector";
    public const string ProbeModule = "EMR";
    public const string ProbeScope = "DataSync";
    public const int DefaultCadenceSeconds = 300;
    public const string ProbeWebhookKey = "emr-sync";

    public const string InspectionQuery = @"
        SELECT encounter_no AS TargetEntityReference,
               sync_type AS SyncType,
               sync_status AS SyncStatus,
               error_message AS ErrorMessage,
               DATEDIFF(minute, created_at, GETUTCDATE()) AS StalledMinutes
        FROM simrs.emr_sync_logs
        WHERE sync_status = 'Failed'
           OR (sync_status = 'Pending' AND created_at <= DATEADD(minute, -30, GETUTCDATE()));";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Monitors hospital EMR synchronization logs for failed transmissions or stalled pending transactions exceeding 30 minutes.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public EmrSyncDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<EmrSyncRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "All EMR synchronization transactions are healthy and up to date.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"EMR Sync Failure: Encounter {r.TargetEntityReference} ({r.SyncType})",
            baseSeverity: "Medium",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.emr_sync_logs",
                recordKeys: $"encounter_no={r.TargetEntityReference}",
                debitTotal: null,
                creditTotal: null,
                varianceDelta: null,
                trace: $"SyncType: {r.SyncType}, Status: {r.SyncStatus}, Stalled: {r.StalledMinutes} min. Error: {r.ErrorMessage ?? "Pending sync stalled > 30 minutes without error details."}"
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} failed or stalled EMR synchronization transaction(s).");
    }

    public sealed record EmrSyncRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public string SyncType { get; init; } = string.Empty;
        public string SyncStatus { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }
        public int StalledMinutes { get; init; }
    }
}
