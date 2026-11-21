namespace PrIMA.Api.Features.History.GetReliabilityKpi;

public sealed record GetReliabilityKpiQuery(
    DateTime FromUtc,
    DateTime ToUtc);
