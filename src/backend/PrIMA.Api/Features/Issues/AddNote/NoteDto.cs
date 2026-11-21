namespace PrIMA.Api.Features.Issues.AddNote;

public sealed record NoteDto(
    Guid Id,
    Guid IssueId,
    string OfficerName,
    string NoteText,
    DateTime CreatedAtUtc);
