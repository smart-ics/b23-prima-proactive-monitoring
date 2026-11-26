namespace PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;

public sealed record DetectorHandshakeRequest(
    string EnrollmentToken,
    string Name,
    string TargetModule,
    string Scope,
    int ExpectedCadenceSeconds,
    string? WebhookUrl = null,
    string? Description = null,
    string? Version = null);
