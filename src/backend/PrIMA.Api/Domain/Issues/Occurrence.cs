namespace PrIMA.Api.Domain.Issues;

public sealed class Occurrence
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public Guid InspectionRunId { get; private set; }
    public int OccurrenceNumber { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public TelemetryEvidence Evidence { get; private set; } = null!;

    // Flattened projection properties matching dbo.Occurrences schema
    public string Module => Evidence.Module;
    public string? SourceTable => Evidence.SourceTable;
    public string? RecordKeys => Evidence.RecordKeys;
    public decimal? DebitTotal => Evidence.DebitTotal;
    public decimal? CreditTotal => Evidence.CreditTotal;
    public decimal? VarianceDelta => Evidence.VarianceDelta;
    public string? TraceString => Evidence.Trace;

    // Parameterless constructor for Dapper / reflection hydration
    private Occurrence() { }

    public Occurrence(
        Guid id,
        Guid issueId,
        Guid inspectionRunId,
        int occurrenceNumber,
        DateTime timestampUtc,
        TelemetryEvidence evidence)
    {
        if (id == Guid.Empty) throw new ArgumentException("Occurrence ID cannot be empty.", nameof(id));
        if (issueId == Guid.Empty) throw new ArgumentException("Issue ID cannot be empty.", nameof(issueId));
        if (inspectionRunId == Guid.Empty) throw new ArgumentException("Inspection run ID cannot be empty.", nameof(inspectionRunId));
        if (occurrenceNumber < 1) throw new ArgumentOutOfRangeException(nameof(occurrenceNumber), occurrenceNumber, "Occurrence number must be >= 1.");
        ArgumentNullException.ThrowIfNull(evidence);

        Id = id;
        IssueId = issueId;
        InspectionRunId = inspectionRunId;
        OccurrenceNumber = occurrenceNumber;
        TimestampUtc = timestampUtc;
        Evidence = evidence;
    }

    public Occurrence(
        Guid id,
        Guid issueId,
        Guid inspectionRunId,
        int occurrenceNumber,
        DateTime timestampUtc,
        string module,
        string? sourceTable = null,
        string? recordKeys = null,
        decimal? debitTotal = null,
        decimal? creditTotal = null,
        decimal? varianceDelta = null,
        string? traceString = null)
        : this(
            id,
            issueId,
            inspectionRunId,
            occurrenceNumber,
            timestampUtc,
            new TelemetryEvidence(module, sourceTable, recordKeys, debitTotal, creditTotal, varianceDelta, traceString))
    {
    }

    public static Occurrence Create(
        Guid issueId,
        Guid inspectionRunId,
        int occurrenceNumber,
        TelemetryEvidence evidence,
        DateTime timestampUtc)
    {
        return new Occurrence(
            Guid.NewGuid(),
            issueId,
            inspectionRunId,
            occurrenceNumber,
            timestampUtc,
            evidence);
    }
}
