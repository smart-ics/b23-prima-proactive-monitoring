namespace PrIMA.Api.Features.History.GetHistoricalIssues;

public sealed record GetHistoricalIssuesQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    string? Status = null,
    string? Scope = null,
    Guid? DetectorId = null,
    string? Severity = null,
    string? Search = null,
    int? Limit = null);
