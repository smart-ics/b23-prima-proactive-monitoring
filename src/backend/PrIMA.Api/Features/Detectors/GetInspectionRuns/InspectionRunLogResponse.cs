namespace PrIMA.Api.Features.Detectors.GetInspectionRuns;

public sealed record InspectionRunLogResponse(
    int TotalCount,
    IReadOnlyList<InspectionRunDetailDto> Runs);
