namespace PrIMA.Api.Features.Issues.TriggerVerificationRun;

public sealed record VerificationResultDto(
    Guid VerificationId,
    Guid IssueId,
    Guid RunId,
    string VerificationMethod,
    bool CleanPassed,
    string EvidenceNotes,
    string VerifiedBy,
    DateTime TimestampUtc,
    bool GateUnlocked);
