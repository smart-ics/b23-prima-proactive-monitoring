namespace PrIMA.Api.Features.Issues.GetDashboardFeed;

public sealed record GetDashboardFeedQuery(
    string? Status = null,
    string? Severity = null,
    string? Scope = null,
    string? Search = null,
    bool? EscalatedOnly = null);
