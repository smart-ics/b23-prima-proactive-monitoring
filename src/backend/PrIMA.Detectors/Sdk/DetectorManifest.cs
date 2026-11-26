namespace PrIMA.Detectors.Sdk;

/// <summary>
/// Manifest metadata describing a detector probe for registration and supervision.
/// </summary>
public sealed record DetectorManifest
{
    public string Name { get; init; } = string.Empty;
    public string TargetModule { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public int ExpectedCadenceSeconds { get; init; }
    public string? Description { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string WebhookEndpointKey { get; init; } = string.Empty;

    public DetectorManifest()
    {
    }

    public DetectorManifest(
        string name,
        string targetModule,
        string scope,
        int expectedCadenceSeconds,
        string? description = null,
        string version = "1.0.0",
        string webhookEndpointKey = "")
    {
        Name = name;
        TargetModule = targetModule;
        Scope = scope;
        ExpectedCadenceSeconds = expectedCadenceSeconds;
        Description = description;
        Version = version;
        WebhookEndpointKey = string.IsNullOrWhiteSpace(webhookEndpointKey) ? name.ToLowerInvariant().Replace(" ", "-") : webhookEndpointKey;
    }
}
