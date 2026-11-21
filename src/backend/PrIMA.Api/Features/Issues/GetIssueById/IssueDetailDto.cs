namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record IssueDetailHeaderRaw(
    Guid Id,
    string CorrelationKey,
    string Title,
    Guid DetectorId,
    string DetectorName,
    string Scope,
    string BaseSeverity,
    string CurrentSeverity,
    byte SeverityRank,
    string Status,
    int EscalationLevel,
    int OccurrenceCount,
    string? CurrentAssignee,
    DateTime CreatedAtUtc,
    DateTime LastOccurrenceAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc,
    DateTime? ReopenedAtUtc);

public sealed record IssueDetailDto(
    IssueDetailHeaderRaw Issue,
    IReadOnlyList<OccurrenceItemDto> Occurrences,
    IReadOnlyList<InvestigationNoteDto> Notes,
    IReadOnlyList<RemediationRecordDto> Remediations,
    IReadOnlyList<VerificationRecordDto> Verifications,
    DismissalRecordDto? Dismissal,
    IReadOnlyList<IssueActivityLogDto> ActivityLogs)
{
    public IReadOnlyList<RemediationRecordDto> RemediationRecords => Remediations;
    public IReadOnlyList<VerificationRecordDto> VerificationRecords => Verifications;

    // Header forwarding accessors for top-level convenience
    public Guid Id => Issue.Id;
    public string CorrelationKey => Issue.CorrelationKey;
    public string Title => Issue.Title;
    public Guid DetectorId => Issue.DetectorId;
    public string DetectorName => Issue.DetectorName;
    public string Scope => Issue.Scope;
    public string BaseSeverity => Issue.BaseSeverity;
    public string CurrentSeverity => Issue.CurrentSeverity;
    public byte SeverityRank => Issue.SeverityRank;
    public string Status => Issue.Status;
    public int EscalationLevel => Issue.EscalationLevel;
    public int OccurrenceCount => Issue.OccurrenceCount;
    public string? CurrentAssignee => Issue.CurrentAssignee;
    public DateTime CreatedAtUtc => Issue.CreatedAtUtc;
    public DateTime LastOccurrenceAtUtc => Issue.LastOccurrenceAtUtc;
    public DateTime? AcknowledgedAtUtc => Issue.AcknowledgedAtUtc;
    public DateTime? ResolvedAtUtc => Issue.ResolvedAtUtc;
    public DateTime? DismissedAtUtc => Issue.DismissedAtUtc;
    public DateTime? ReopenedAtUtc => Issue.ReopenedAtUtc;
}
