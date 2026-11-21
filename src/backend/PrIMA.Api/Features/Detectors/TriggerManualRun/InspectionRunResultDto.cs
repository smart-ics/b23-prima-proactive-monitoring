namespace PrIMA.Api.Features.Detectors.TriggerManualRun;

public sealed record InspectionRunResultDto(
    Guid RunId,
    string Outcome,
    int DurationMs,
    string? FailureReason,
    int FindingsCount);
