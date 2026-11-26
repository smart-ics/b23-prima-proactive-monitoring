using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Pilot SIMRS detector probe for monitoring BPJS HFIS doctor schedule synchronization (DEC-007, BR-PRIMA-007).
/// Compares active outpatient doctor practice schedules and consultation quotas between SIMRS and BPJS HFIS.
/// </summary>
public sealed class HfisScheduleSyncDetector : IDetectorProbe
{
    public const string ProbeName = "HFIS Doctor Schedule Detector";
    public const string ProbeModule = "Outpatient";
    public const string ProbeScope = "DataSync";
    public const int DefaultCadenceSeconds = 300;
    public const string ProbeWebhookKey = "hfis-schedule-sync";

    public const string InspectionQuery = @"
        SELECT doctor_schedule_code AS TargetEntityReference,
               doctor_name AS DoctorName,
               clinic_name AS ClinicName,
               simrs_start_time AS SimrsStart,
               hfis_start_time AS HfisStart,
               simrs_end_time AS SimrsEnd,
               hfis_end_time AS HfisEnd,
               simrs_quota AS SimrsQuota,
               hfis_quota AS HfisQuota
        FROM simrs.hfis_doctor_schedules
        WHERE is_active = 1
          AND schedule_date >= CAST(GETUTCDATE() AS DATE)
          AND (simrs_start_time <> hfis_start_time
               OR simrs_end_time <> hfis_end_time
               OR simrs_quota <> hfis_quota);";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Compares outpatient doctor practice schedules and consultation quotas between SIMRS and BPJS HFIS records.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public HfisScheduleSyncDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<HfisScheduleRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "All active outpatient doctor schedules match BPJS HFIS records.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"BPJS HFIS Doctor Schedule Mismatch: {r.TargetEntityReference} ({r.DoctorName})",
            baseSeverity: "Medium",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.hfis_doctor_schedules",
                recordKeys: $"doctor_schedule_code={r.TargetEntityReference}",
                debitTotal: null,
                creditTotal: null,
                varianceDelta: null,
                trace: $"Doctor: {r.DoctorName}, Clinic: {r.ClinicName}. SIMRS ({FormatTime(r.SimrsStart)}-{FormatTime(r.SimrsEnd)}, Quota: {r.SimrsQuota}) vs HFIS ({FormatTime(r.HfisStart)}-{FormatTime(r.HfisEnd)}, Quota: {r.HfisQuota})."
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} mismatched outpatient doctor schedule(s) with BPJS HFIS.");
    }

    private static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm");

    public sealed record HfisScheduleRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public string DoctorName { get; init; } = string.Empty;
        public string ClinicName { get; init; } = string.Empty;
        public TimeSpan SimrsStart { get; init; }
        public TimeSpan HfisStart { get; init; }
        public TimeSpan SimrsEnd { get; init; }
        public TimeSpan HfisEnd { get; init; }
        public int SimrsQuota { get; init; }
        public int HfisQuota { get; init; }
    }
}