namespace PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;

public sealed record DetectorHandshakeResponse(
    Guid DetectorId,
    string ApiKey,
    string Status,
    string Name,
    string? Version);
