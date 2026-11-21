namespace PrIMA.Api.Domain.Issues;

public sealed class DismissalRecord
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string DismissalReason { get; private set; } = null!;
    public string JustificationRationale { get; private set; } = null!;
    public string DismissedBy { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
    public bool IsSuperseded { get; private set; }

    private DismissalRecord() { }

    public DismissalRecord(
        Guid id,
        Guid issueId,
        string dismissalReason,
        string justificationRationale,
        string dismissedBy,
        DateTime timestampUtc,
        bool isSuperseded = false)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        IssueId = issueId;
        DismissalReason = dismissalReason.Trim();
        JustificationRationale = justificationRationale.Trim();
        DismissedBy = dismissedBy.Trim();
        TimestampUtc = timestampUtc;
        IsSuperseded = isSuperseded;
    }

    public DismissalRecord(
        string dismissalReason,
        string justificationRationale,
        string dismissedBy,
        DateTime timestampUtc)
        : this(Guid.NewGuid(), Guid.Empty, dismissalReason, justificationRationale, dismissedBy, timestampUtc, false)
    {
    }

    public void MarkSuperseded()
    {
        IsSuperseded = true;
    }
}
