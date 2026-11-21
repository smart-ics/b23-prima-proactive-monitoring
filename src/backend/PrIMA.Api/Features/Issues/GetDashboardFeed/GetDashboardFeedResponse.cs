namespace PrIMA.Api.Features.Issues.GetDashboardFeed;

public sealed record DashboardFeedSummaryDto(
    int CriticalCount,
    int HighCount,
    int OpenCount,
    int ReopenedCount,
    int ResolvedTodayCount,
    int EscalatedCount);

public sealed record GetDashboardFeedResponse(
    DashboardFeedSummaryDto Summary,
    IReadOnlyList<IssueFeedItemDto> Items);
