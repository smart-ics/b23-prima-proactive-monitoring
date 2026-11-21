namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed record OccurrenceItemDto(
    Guid Id,
    Guid IssueId,
    Guid InspectionRunId,
    int OccurrenceNumber,
    DateTime TimestampUtc,
    string Module,
    string? SourceTable,
    string? RecordKeys,
    decimal? DebitTotal,
    decimal? CreditTotal,
    decimal? VarianceDelta,
    string? TraceString);
