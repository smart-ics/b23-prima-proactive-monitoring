namespace PrIMA.Api.Features.Detectors.GetInspectionRuns;

public sealed record InspectionRunDetailDto(
    Guid Id,
    Guid DetectorId,
    DateTime ExecutionTimestampUtc,
    string TriggerSource,
    string Outcome,
    int DurationMs,
    string? FailureReason,
    int FindingsCount,
    IReadOnlyList<Guid> DiscoveredIssueIds);
