namespace PrIMA.Api.Features.Ingestion;

public sealed record IngestTelemetryRequest
{
    public Guid DetectorId { get; init; }
    public DateTime ExecutionTimestampUtc { get; init; }
    public string TriggerSource { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public int DurationMs { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<FindingItem>? Findings { get; init; }
}
