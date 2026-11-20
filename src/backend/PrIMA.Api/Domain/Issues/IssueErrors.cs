using PrIMA.Api.Common;

namespace PrIMA.Api.Domain.Issues;

public static class IssueErrors
{
    public static DomainError NotFound(Guid id) =>
        DomainError.NotFound("Issues.NotFound", $"Issue with ID '{id}' was not found.");

    public static DomainError NotFoundByCorrelationKey(string correlationKey) =>
        DomainError.NotFound("Issues.NotFoundByCorrelationKey", $"Issue with correlation key '{correlationKey}' was not found.");

    public static DomainError InvalidStatusTransition(IssueStatus current, IssueStatus target) =>
        DomainError.Conflict("Issues.InvalidStatusTransition", $"Cannot transition issue from '{current}' to '{target}'.");

    public static DomainError VerificationGateRequired =>
        DomainError.Unprocessable("Issues.VerificationGateRequired", "Cannot resolve issue without a clean verification record.");

    public static DomainError DismissalJustificationRequired =>
        DomainError.Unprocessable("Issues.DismissalJustificationRequired", "Cannot dismiss issue without justification rationale.");

    public static DomainError ClosedStatusFeedFilter =>
        DomainError.Unprocessable("Issues.ClosedStatusFeedFilter", "Dashboard feed displays active issues only. Closed issues are not accessible via this endpoint.");
}
