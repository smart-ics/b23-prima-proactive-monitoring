using Microsoft.AspNetCore.Http;
using PrIMA.Api.Common;

namespace PrIMA.Api.Infrastructure.Webhooks;

public static class WebhookErrors
{
    public static DomainError Timeout(string? message = null) =>
        new(
            "Webhooks.Timeout",
            string.IsNullOrWhiteSpace(message)
                ? "The external detector webhook timed out after 15 seconds."
                : message,
            StatusCodes.Status504GatewayTimeout);

    public static DomainError Unreachable(string? message = null) =>
        new(
            "Webhooks.Unreachable",
            string.IsNullOrWhiteSpace(message)
                ? "The external detector webhook endpoint is unreachable or returned a server error."
                : message,
            StatusCodes.Status502BadGateway);
}
