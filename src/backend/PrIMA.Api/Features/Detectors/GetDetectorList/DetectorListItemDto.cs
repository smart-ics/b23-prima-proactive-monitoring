namespace PrIMA.Api.Features.Detectors.GetDetectorList;

public sealed record DetectorListItemDto(
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
    decimal PassRate30d,
    int TotalRuns30d,
    int ActiveIssuesCount,
    string? ApiKeyHash = null,
    string? Version = null,
    int? LastActualIntervalSeconds = null,
    string? CadenceDriftWarning = null);
