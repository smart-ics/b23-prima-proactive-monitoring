using PrIMA.Api.Common;

namespace PrIMA.Api.Infrastructure.Webhooks;

public interface IDetectorWebhookClient
{
    Task<Result<WebhookExecutionResult>> ExecuteRunAsync(
        Guid detectorId,
        string webhookUrl,
        string triggerSource,
        Guid? issueId = null,
        CancellationToken cancellationToken = default);
}
