namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public sealed record DetectorDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Scope,
    string TargetModule,
    int ExpectedCadenceSeconds,
    string? WebhookUrl,
    string AdminStatus,
    string HealthStatus,
    DateTime? LastRunAtUtc,
    DateTime? LastHealthyHeartbeatUtc,
    DateTime CreatedAtUtc,
    DetectorReliabilityStatsDto ReliabilityStats,
    IReadOnlyList<RecentRunSummaryDto> RecentRuns,
    IReadOnlyList<LinkedActiveIssueDto> ActiveIssues);
