using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Probes simrs.ar_receivables for multiple active receivables linked to identical source transactions.
/// Realizes DEC-007, BR-PRIMA-007.
/// </summary>
public sealed class DuplicateReceivableDetector : IDetectorProbe
{
    public const string ProbeName = "Duplicate Receivable Detector";
    public const string ProbeModule = "Billing";
    public const string ProbeScope = "DataIntegrity";
    public const int DefaultCadenceSeconds = 300;
    public const string ProbeWebhookKey = "duplicate-receivable";

    public const string InspectionQuery = @"
        SELECT source_trx_no AS TargetEntityReference,
               COUNT(*) AS DuplicateCount,
               SUM(receivable_amount) AS DebitTotal,
               MAX(receivable_amount) AS CreditTotal,
               SUM(receivable_amount) - MAX(receivable_amount) AS VarianceDelta
        FROM simrs.ar_receivables
        WHERE status = 'Active'
          AND created_at >= DATEADD(day, -7, GETUTCDATE())
        GROUP BY source_trx_no
        HAVING COUNT(*) > 1;";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Detects multiple active patient receivable entries for identical source transactions.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public DuplicateReceivableDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<DuplicateReceivableRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "No duplicate active receivables found in the past 7 days.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"Duplicate Active Receivable for Transaction {r.TargetEntityReference}",
            baseSeverity: "High",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.ar_receivables",
                recordKeys: $"source_trx_no={r.TargetEntityReference}",
                debitTotal: r.DebitTotal,
                creditTotal: r.CreditTotal,
                varianceDelta: r.VarianceDelta,
                trace: $"Found {r.DuplicateCount} active receivable records for source transaction '{r.TargetEntityReference}'. Total sum={r.DebitTotal:N2}, Expected={r.CreditTotal:N2}, Excess Variance={r.VarianceDelta:N2}."
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} duplicate receivable transaction(s).");
    }

    public sealed record DuplicateReceivableRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public int DuplicateCount { get; init; }
        public decimal DebitTotal { get; init; }
        public decimal CreditTotal { get; init; }
        public decimal VarianceDelta { get; init; }
    }
}
