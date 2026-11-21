namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public sealed record RecentRunSummaryDto(
    Guid Id,
    DateTime ExecutionTimestampUtc,
    string TriggerSource,
    string Outcome,
    int DurationMs,
    string? FailureReason,
    int FindingsCount);
