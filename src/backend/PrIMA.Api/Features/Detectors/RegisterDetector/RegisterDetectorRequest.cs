namespace PrIMA.Api.Features.Detectors.RegisterDetector;

public sealed record RegisterDetectorRequest(
    string Name,
    string Description,
    string Scope,
    string TargetModule,
    int ExpectedCadenceSeconds,
    string? WebhookUrl);
