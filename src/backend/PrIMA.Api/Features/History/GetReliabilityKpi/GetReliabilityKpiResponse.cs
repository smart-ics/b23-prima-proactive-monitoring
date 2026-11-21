namespace PrIMA.Api.Features.History.GetReliabilityKpi;

public sealed record BreakdownItemDto(
    string Category,
    int Count,
    double Percentage);

public sealed record ReliabilityKpiResponse(
    int TotalIssuesCreated,
    double ResolutionRatePercentage,
    double? AvgTimeToAcknowledgeHours,
    double? AvgTimeToResolveHours,
    double DetectorPassRatePercentage,
    int ReopenedIssuesCount,
    IReadOnlyList<BreakdownItemDto> ScopeBreakdown,
    IReadOnlyList<BreakdownItemDto> SeverityBreakdown);
