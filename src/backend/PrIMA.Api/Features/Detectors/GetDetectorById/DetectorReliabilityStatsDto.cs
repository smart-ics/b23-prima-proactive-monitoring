namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public sealed record DetectorReliabilityStatsDto(
    DateTime? LastSuccessfulRunAtUtc,
    DateTime? LastFailureAtUtc,
    int TotalRuns30d,
    decimal PassRate30d);
