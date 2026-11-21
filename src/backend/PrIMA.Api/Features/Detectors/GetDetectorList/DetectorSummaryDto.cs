namespace PrIMA.Api.Features.Detectors.GetDetectorList;

public sealed record DetectorSummaryDto(
    int TotalCount,
    int HealthyCount,
    int FailingCount,
    int SilentCount,
    int PausedCount);
