namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record IssueActivityLogDto(
    Guid Id,
    Guid IssueId,
    string ActivityType,
    string ActorName,
    DateTime TimestampUtc,
    string Summary,
    string? DetailsJson);
