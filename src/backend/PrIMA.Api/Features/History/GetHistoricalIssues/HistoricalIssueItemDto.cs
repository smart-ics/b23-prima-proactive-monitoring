namespace PrIMA.Api.Features.History.GetHistoricalIssues;

public sealed record HistoricalIssueItemDto(
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
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc,
    DateTime? ReopenedAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime LastOccurrenceAtUtc);
