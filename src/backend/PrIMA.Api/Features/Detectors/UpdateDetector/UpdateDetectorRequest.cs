namespace PrIMA.Api.Features.Detectors.UpdateDetector;

public sealed record UpdateDetectorRequest(
    string Description,
    string Scope,
    string TargetModule,
    int ExpectedCadenceSeconds,
    string? WebhookUrl);
