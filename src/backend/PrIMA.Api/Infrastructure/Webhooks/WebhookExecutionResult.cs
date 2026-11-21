namespace PrIMA.Api.Infrastructure.Webhooks;

public sealed record WebhookExecutionResult(
    string Outcome,
    int DurationMs,
    string? FailureReason,
    IReadOnlyList<WebhookFindingDto> Findings);
