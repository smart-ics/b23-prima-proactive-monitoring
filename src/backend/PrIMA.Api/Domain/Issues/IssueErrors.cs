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

    public static DomainError InvalidStateTransition(IssueStatus current, IssueStatus target) =>
        DomainError.Conflict("Issues.InvalidStateTransition", $"Cannot transition issue from '{current}' to '{target}'.");

    public static DomainError ActionRequiresActiveStatus() =>
        DomainError.Conflict("Issues.ActionRequiresActiveStatus", "Action cannot be performed on a closed issue.");

    public static DomainError ActionRequiresStatus(IssueStatus required) =>
        DomainError.Conflict("Issues.ActionRequiresStatus", $"Action requires issue to be in '{required}' status.");

    public static DomainError EmptyNoteText() =>
        DomainError.Unprocessable("Issues.EmptyNoteText", "Investigation note text cannot be empty.");

    public static DomainError EmptyRemediationDescription() =>
        DomainError.Unprocessable("Issues.EmptyRemediationDescription", "Remediation summary description cannot be empty.");

    public static DomainError RemediationRequiredBeforeVerification() =>
        DomainError.Conflict("Issues.RemediationRequiredBeforeVerification", "A remediation record is required before recording verification.");

    public static DomainError EmptyVerificationEvidence() =>
        DomainError.Unprocessable("Issues.EmptyVerificationEvidence", "Verification evidence notes cannot be empty.");

    public static DomainError VerificationGateFailed() =>
        DomainError.Conflict("Issues.VerificationGateFailed", "Cannot resolve issue. Mandatory verification gate requires a clean, passed verification record.");

    public static DomainError VerificationGateRequired =>
        DomainError.Unprocessable("Issues.VerificationGateRequired", "Cannot resolve issue without a clean verification record.");

    public static DomainError DismissalRationaleTooShort() =>
        DomainError.Unprocessable("Issues.DismissalRationaleTooShort", "Dismissal justification rationale must be at least 20 characters.");

    public static DomainError InvalidDismissalReason() =>
        DomainError.Unprocessable("Issues.InvalidDismissalReason", "Invalid dismissal reason. Allowed values are FalsePositive, AcceptedOperationalException, DuplicateManualReport.");

    public static DomainError DismissalJustificationRequired =>
        DomainError.Unprocessable("Issues.DismissalJustificationRequired", "Cannot dismiss issue without justification rationale.");

    public static DomainError ClosedStatusFeedFilter =>
        DomainError.Unprocessable("Issues.ClosedStatusFeedFilter", "Dashboard feed displays active issues only. Closed issues are not accessible via this endpoint.");
}
