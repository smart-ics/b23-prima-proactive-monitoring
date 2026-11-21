namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public sealed record LinkedActiveIssueDto(
    Guid Id,
    string CorrelationKey,
    string Title,
    string CurrentSeverity,
    byte SeverityRank,
    string Status,
    int EscalationLevel,
    int OccurrenceCount,
    DateTime LastOccurrenceAtUtc);
