namespace PrIMA.Api.Features.Ingestion;

public sealed record IngestTelemetryResponse
{
    public Guid InspectionRunId { get; init; }
    public Guid DetectorId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public int ProcessedFindingsCount { get; init; }
    public IReadOnlyList<FindingResultItem> Results { get; init; } = Array.Empty<FindingResultItem>();
}

public sealed record FindingResultItem
{
    public string CorrelationKey { get; init; } = string.Empty;
    public Guid IssueId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid OccurrenceId { get; init; }
    public int OccurrenceNumber { get; init; }
}
