using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.Enrollment;

public static class EnrollmentTokenErrors
{
    public static DomainError NotFound(Guid id) =>
        DomainError.NotFound("EnrollmentToken.NotFound", $"Enrollment token with ID '{id}' was not found.");

    public static readonly DomainError AlreadyConsumed =
        DomainError.Conflict("EnrollmentToken.AlreadyConsumed", "Cannot revoke an enrollment token that has already been consumed.");

    public static readonly DomainError AlreadyRevoked =
        DomainError.Conflict("EnrollmentToken.AlreadyRevoked", "Enrollment token has already been revoked.");

    public static readonly DomainError Expired =
        DomainError.Unauthorized("EnrollmentToken.Expired", "Enrollment token has expired.");

    public static readonly DomainError InvalidToken =
        DomainError.Unauthorized("EnrollmentToken.InvalidToken", "Enrollment token is invalid or does not exist.");
}
