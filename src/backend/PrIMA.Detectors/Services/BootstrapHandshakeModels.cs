using System.Text.Json.Serialization;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Cached credential details for an enrolled detector.
/// </summary>
public sealed class DetectorCredential
{
    [JsonPropertyName("detectorId")]
    public Guid DetectorId { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = string.Empty;

    [JsonPropertyName("detectorName")]
    public string? DetectorName { get; set; }
}

/// <summary>
/// Root schema for the local edge credential cache file (.prima-credentials.json).
/// </summary>
public sealed class DetectorCredentialsFile
{
    [JsonPropertyName("detectorId")]
    public Guid? DetectorId { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("serverUrl")]
    public string? ServerUrl { get; set; }

    [JsonPropertyName("detectorName")]
    public string? DetectorName { get; set; }

    [JsonPropertyName("probes")]
    public Dictionary<string, DetectorCredential> Probes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Handshake request payload sent to POST /api/detectors/handshake.
/// </summary>
public sealed record DetectorHandshakeApiRequest(
    string EnrollmentToken,
    string Name,
    string TargetModule,
    string Scope,
    int ExpectedCadenceSeconds,
    string? WebhookUrl = null,
    string? Description = null,
    string? Version = null);

/// <summary>
/// Handshake response payload returned from POST /api/detectors/handshake.
/// </summary>
public sealed record DetectorHandshakeApiResponse(
    Guid DetectorId,
    string ApiKey,
    string Status,
    string Name,
    string? Version);
