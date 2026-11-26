namespace PrIMA.Api.Features.Detectors.Enrollment.GetPendingEnrollmentTokens;

public sealed record PendingEnrollmentTokenDto(
    Guid Id,
    string TokenPrefix,
    string? Label,
    string Status,
    DateTime ExpiresAtUtc,
    string CreatedByOfficer,
    DateTime CreatedAtUtc);
