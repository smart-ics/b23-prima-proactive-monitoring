using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Detectors.Probes;

/// <summary>
/// Probes simrs.acc_journal_entries for general ledger journal batches where ABS(SUM(Debit) - SUM(Credit)) > 0.01.
/// Realizes DEC-007, BR-PRIMA-007.
/// </summary>
public sealed class JournalBalanceDetector : IDetectorProbe
{
    public const string ProbeName = "Journal Balance Detector";
    public const string ProbeModule = "Accounting";
    public const string ProbeScope = "DataIntegrity";
    public const int DefaultCadenceSeconds = 300;
    public const string ProbeWebhookKey = "journal-balance";

    public const string InspectionQuery = @"
        SELECT journal_batch_no AS TargetEntityReference,
               SUM(debit) AS DebitTotal,
               SUM(credit) AS CreditTotal,
               ABS(SUM(debit) - SUM(credit)) AS VarianceDelta,
               COUNT(*) AS LineCount
        FROM simrs.acc_journal_entries
        WHERE posted_status = 'Posted'
          AND entry_date >= DATEADD(day, -3, GETUTCDATE())
        GROUP BY journal_batch_no
        HAVING ABS(SUM(debit) - SUM(credit)) > 0.01;";

    private readonly IReadOnlySimrsConnectionFactory _connectionFactory;

    public DetectorManifest Manifest { get; } = new(
        name: ProbeName,
        targetModule: ProbeModule,
        scope: ProbeScope,
        expectedCadenceSeconds: DefaultCadenceSeconds,
        description: "Audits general ledger journal batches for debit and credit discrepancies.",
        version: "1.0.0",
        webhookEndpointKey: ProbeWebhookKey);

    public JournalBalanceDetector(IReadOnlySimrsConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
    {
        var records = (await _connectionFactory.QueryAsync<JournalBalanceRecord>(InspectionQuery, ct: ct)).ToList();

        if (records.Count == 0)
        {
            return TelemetryReport.Passed(
                traceString: "All journal batches in the past 3 days are balanced.");
        }

        var findings = records.Select(r => new FindingItem(
            targetEntityReference: r.TargetEntityReference,
            title: $"Unbalanced Journal Batch {r.TargetEntityReference}",
            baseSeverity: "High",
            evidence: new FindingEvidenceItem(
                module: ProbeModule,
                sourceTable: "simrs.acc_journal_entries",
                recordKeys: $"journal_batch_no={r.TargetEntityReference}",
                debitTotal: r.DebitTotal,
                creditTotal: r.CreditTotal,
                varianceDelta: r.VarianceDelta,
                trace: $"Journal batch '{r.TargetEntityReference}' has {r.LineCount} entries with Debit={r.DebitTotal:N2}, Credit={r.CreditTotal:N2}, Variance={r.VarianceDelta:N2}."
            )
        )).ToList();

        return TelemetryReport.FindingDiscovered(
            findings: findings,
            traceString: $"Discovered {findings.Count} unbalanced journal batch(es).");
    }

    public sealed record JournalBalanceRecord
    {
        public string TargetEntityReference { get; init; } = string.Empty;
        public decimal DebitTotal { get; init; }
        public decimal CreditTotal { get; init; }
        public decimal VarianceDelta { get; init; }
        public int LineCount { get; init; }
    }
}
