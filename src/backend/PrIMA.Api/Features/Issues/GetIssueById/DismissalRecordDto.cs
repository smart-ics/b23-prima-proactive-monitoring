namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record DismissalRecordDto(
    Guid Id,
    Guid IssueId,
    string DismissalReason,
    string JustificationRationale,
    string DismissedBy,
    DateTime TimestampUtc,
    bool IsSuperseded);
