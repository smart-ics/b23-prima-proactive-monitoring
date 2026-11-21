namespace PrIMA.Api.Infrastructure.Webhooks;

public sealed record WebhookFindingDto(
    string TargetEntityReference,
    string Title,
    string BaseSeverity,
    WebhookEvidenceDto Evidence);
