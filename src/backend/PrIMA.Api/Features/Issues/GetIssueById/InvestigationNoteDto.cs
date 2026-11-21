namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record InvestigationNoteDto(
    Guid Id,
    Guid IssueId,
    string OfficerName,
    string NoteText,
    DateTime CreatedAtUtc);
