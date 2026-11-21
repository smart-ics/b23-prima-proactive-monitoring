namespace PrIMA.Api.Features.Issues.DismissIssue;

public sealed record DismissIssueRequest(
    string DismissalReason,
    string JustificationRationale);
