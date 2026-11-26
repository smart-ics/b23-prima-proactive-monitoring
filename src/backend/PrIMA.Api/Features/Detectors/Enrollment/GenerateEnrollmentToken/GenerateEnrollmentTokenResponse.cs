namespace PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;

public sealed record GenerateEnrollmentTokenResponse(
    Guid Id,
    string Token,
    string TokenPrefix,
    DateTime ExpiresAtUtc,
    string? Label)
{
    public string Prefix => TokenPrefix;
}
