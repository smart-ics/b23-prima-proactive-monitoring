namespace PrIMA.Api.Features.History.GetHistoricalIssues;

public sealed record HistoricalIssuesResponse(
    IReadOnlyList<HistoricalIssueItemDto> Items,
    int TotalReturned,
    int ClampedLimit);
