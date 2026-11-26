using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Probes simrs.trx_patient_bills for settled bills (>15 minutes ago) missing corresponding general ledger journal entries.
/// Realizes DEC-007, BR-PRIMA-007.
/// </summary>
public sealed class UnprocessedBillingPipelineDetector : IDetectorProbe
{
    public const string ProbeName = "Unprocessed Billing Pipeline Detector";
    public const string ProbeModule = "Billing";
    public const string ProbeScope = "ProcessIntegrity";
    public const int DefaultCadenceSeconds = 300;
    public const string ProbeWebhookKey = "unprocessed-billing";

    public const string InspectionQuery = @"
        SELECT b.bill_invoice_no AS TargetEntityReference,
               b.bill_amount AS DebitTotal,
               0.00 AS CreditTotal,
               b.bill_amount AS VarianceDelta,
               b.settled_at AS SettledAt
        FROM simrs.trx_patient_bills b
        WHERE b.settlement_status = 'Settled'
          AND b.settled_at <= DATEADD(minute, -15, GETUTCDATE())
          AND b.settled_at >= DATEADD(day, -3, GETUTCDATE())
          AND NOT EXISTS (
              SELECT 1 FROM simrs.acc_journal_entries j
              WHERE j.journal_batch_no = b.bill_invoice_no
          );";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Identifies settled patient bills missing corresponding general ledger journal entries.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public UnprocessedBillingPipelineDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<UnprocessedBillingRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "All settled patient bills have matching auto-journal entries.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"Unprocessed Settled Bill Missing Auto-Journal {r.TargetEntityReference}",
            baseSeverity: "High",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.trx_patient_bills",
                recordKeys: $"bill_invoice_no={r.TargetEntityReference}",
                debitTotal: r.DebitTotal,
                creditTotal: 0.00m,
                varianceDelta: r.VarianceDelta,
                trace: $"Settled patient bill '{r.TargetEntityReference}' with amount {r.DebitTotal:N2} settled at {r.SettledAt:u} is missing general ledger auto-journal batch after >15 minutes."
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} unprocessed settled bill(s) missing journal entries.");
    }

    public sealed record UnprocessedBillingRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public decimal DebitTotal { get; init; }
        public decimal CreditTotal { get; init; }
        public decimal VarianceDelta { get; init; }
        public DateTime SettledAt { get; init; }
    }
}
