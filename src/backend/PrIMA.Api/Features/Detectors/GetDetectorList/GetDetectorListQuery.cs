namespace PrIMA.Api.Features.Detectors.GetDetectorList;

public sealed record GetDetectorListQuery(
    string? Scope = null,
    string? AdminStatus = null,
    string? HealthStatus = null,
    string? Search = null);
