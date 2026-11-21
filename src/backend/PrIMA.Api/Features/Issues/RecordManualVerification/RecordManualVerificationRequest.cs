namespace PrIMA.Api.Features.Issues.RecordManualVerification;

public sealed record RecordManualVerificationRequest(bool CleanPassed, string EvidenceNotes);
