namespace PrIMA.Api.Features.Detectors.GetInspectionRuns;

public sealed record GetInspectionRunsQuery(
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    string? Outcome = null,
    string? TriggerSource = null,
    int? Limit = null,
    int? Offset = null);
