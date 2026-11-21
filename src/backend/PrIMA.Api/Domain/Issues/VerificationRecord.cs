namespace PrIMA.Api.Domain.Issues;

public sealed class VerificationRecord
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string VerificationMethod { get; private set; } = null!;
    public bool CleanPassed { get; private set; }
    public string EvidenceNotes { get; private set; } = null!;
    public string VerifiedBy { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
    public bool IsSuperseded { get; private set; }

    private VerificationRecord() { }

    public VerificationRecord(
        Guid id,
        Guid issueId,
        string verificationMethod,
        bool cleanPassed,
        string evidenceNotes,
        string verifiedBy,
        DateTime timestampUtc,
        bool isSuperseded = false)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        IssueId = issueId;
        VerificationMethod = verificationMethod.Trim();
        CleanPassed = cleanPassed;
        EvidenceNotes = evidenceNotes.Trim();
        VerifiedBy = verifiedBy.Trim();
        TimestampUtc = timestampUtc;
        IsSuperseded = isSuperseded;
    }

    public VerificationRecord(
        string verificationMethod,
        bool cleanPassed,
        string evidenceNotes,
        string verifiedBy,
        DateTime timestampUtc)
        : this(Guid.NewGuid(), Guid.Empty, verificationMethod, cleanPassed, evidenceNotes, verifiedBy, timestampUtc, false)
    {
    }

    public void MarkSuperseded()
    {
        IsSuperseded = true;
    }
}
