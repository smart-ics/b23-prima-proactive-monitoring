namespace PrIMA.Api.Infrastructure.Webhooks;

public sealed record WebhookEvidenceDto(
    string Module,
    string? SourceTable = null,
    string? RecordKeys = null,
    decimal? DebitTotal = null,
    decimal? CreditTotal = null,
    decimal? VarianceDelta = null,
    string? Trace = null);
