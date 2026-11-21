namespace PrIMA.Api.Domain.Issues;

public sealed class RemediationRecord
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string ActionCategory { get; private set; } = null!;
    public string SummaryDescription { get; private set; } = null!;
    public string PerformedBy { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
    public bool IsSuperseded { get; private set; }

    private RemediationRecord() { }

    public RemediationRecord(
        Guid id,
        Guid issueId,
        string actionCategory,
        string summaryDescription,
        string performedBy,
        DateTime timestampUtc,
        bool isSuperseded = false)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        IssueId = issueId;
        ActionCategory = actionCategory.Trim();
        SummaryDescription = summaryDescription.Trim();
        PerformedBy = performedBy.Trim();
        TimestampUtc = timestampUtc;
        IsSuperseded = isSuperseded;
    }

    public RemediationRecord(
        string actionCategory,
        string summaryDescription,
        string performedBy,
        DateTime timestampUtc)
        : this(Guid.NewGuid(), Guid.Empty, actionCategory, summaryDescription, performedBy, timestampUtc, false)
    {
    }

    public void MarkSuperseded()
    {
        IsSuperseded = true;
    }
}
