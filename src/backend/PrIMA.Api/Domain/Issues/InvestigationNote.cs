namespace PrIMA.Api.Domain.Issues;

public sealed class InvestigationNote
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string OfficerName { get; private set; } = null!;
    public string NoteText { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private InvestigationNote() { }

    public InvestigationNote(Guid id, Guid issueId, string officerName, string noteText, DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("InvestigationNote ID cannot be empty.", nameof(id));
        if (issueId == Guid.Empty) throw new ArgumentException("Issue ID cannot be empty.", nameof(issueId));
        if (string.IsNullOrWhiteSpace(officerName)) throw new ArgumentException("Officer name cannot be empty.", nameof(officerName));
        if (string.IsNullOrWhiteSpace(noteText)) throw new ArgumentException("Note text cannot be empty.", nameof(noteText));

        Id = id;
        IssueId = issueId;
        OfficerName = officerName.Trim();
        NoteText = noteText.Trim();
        CreatedAtUtc = createdAtUtc;
    }
}
