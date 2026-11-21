namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record RemediationRecordDto(
    Guid Id,
    Guid IssueId,
    string ActionCategory,
    string SummaryDescription,
    string PerformedBy,
    DateTime TimestampUtc,
    bool IsSuperseded);
