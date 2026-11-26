namespace PrIMA.Detectors.Services;

/// <summary>
/// Telemetry ingestion response returned by the PrIMA API.
/// </summary>
public record IngestTelemetryResponse
{
    public Guid InspectionRunId { get; init; }
    public Guid DetectorId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public int ProcessedFindingsCount { get; init; }
    public int TotalFindingsProcessed { get; init; }
    public int NewIssuesCreated { get; init; }
    public int RecurrencesRecorded { get; init; }
    public IReadOnlyList<FindingResultItem> Results { get; init; } = Array.Empty<FindingResultItem>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result item for a processed finding in an ingestion batch.
/// </summary>
public sealed record FindingResultItem
{
    public string CorrelationKey { get; init; } = string.Empty;
    public Guid IssueId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid OccurrenceId { get; init; }
    public int OccurrenceNumber { get; init; }
}

/// <summary>
/// Options for configuring the PrIMA Detector SDK.
/// </summary>
public sealed class DetectorSdkOptions
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public Guid? DetectorId { get; set; }
    public string? ApiKey { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 500;
    public string CredentialFilePath { get; set; } = ".prima-credentials.json";
    public string? EnrollmentToken { get; set; }
    public string? WebhookBaseUrl { get; set; }
    public int WebhookPort { get; set; } = 5090;
}
