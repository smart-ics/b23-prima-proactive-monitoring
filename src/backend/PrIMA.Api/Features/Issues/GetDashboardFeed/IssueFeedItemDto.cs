namespace PrIMA.Api.Features.Issues.GetDashboardFeed;

public sealed record IssueFeedItemDto(
    Guid Id,
    string CorrelationKey,
    string Title,
    Guid DetectorId,
    string DetectorName,
    string Scope,
    string CurrentSeverity,
    byte SeverityRank,
    string Status,
    int EscalationLevel,
    int OccurrenceCount,
    string? CurrentAssignee,
    DateTime CreatedAtUtc,
    DateTime LastOccurrenceAtUtc);
