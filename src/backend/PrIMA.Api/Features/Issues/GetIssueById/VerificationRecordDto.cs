namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record VerificationRecordDto(
    Guid Id,
    Guid IssueId,
    string VerificationMethod,
    bool CleanPassed,
    string EvidenceNotes,
    string VerifiedBy,
    DateTime TimestampUtc,
    bool IsSuperseded);
