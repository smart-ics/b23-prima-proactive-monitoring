using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Webhook trigger request received from PrIMA server (DetectorWebhookClient).
/// </summary>
public sealed record WebhookTriggerRequest(
    [property: JsonPropertyName("detectorId")] Guid? DetectorId,
    [property: JsonPropertyName("triggerSource")] string? TriggerSource,
    [property: JsonPropertyName("issueId")] Guid? IssueId,
    [property: JsonPropertyName("dispatchedAtUtc")] DateTime? DispatchedAtUtc);

/// <summary>
/// Webhook response payload returned to PrIMA server matching DetectorWebhookClient contract.
/// </summary>
public sealed record WebhookExecutionResponse(
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("durationMs")] int DurationMs,
    [property: JsonPropertyName("failureReason")] string? FailureReason,
    [property: JsonPropertyName("findings")] IReadOnlyList<FindingItem> Findings);

/// <summary>
/// Maps embedded Kestrel webhook listener endpoints matching DetectorWebhookClient 15s synchronous contract (DEC-005).
/// </summary>
public static class DetectorWebhookEndpoints
{
    public static IEndpointRouteBuilder MapDetectorWebhooks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/{detectorKey}", async (
            string detectorKey,
            HttpContext httpContext,
            IEnumerable<IDetectorProbe> probes,
            IBootstrapHandshakeService handshakeService,
            IDetectorRunner detectorRunner,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PrIMA.Detectors.WebhookListener");
            logger.LogInformation("Received webhook trigger request for detector key '{DetectorKey}'.", detectorKey);

            WebhookTriggerRequest? request = null;
            if (httpContext.Request.HasJsonContentType())
            {
                try
                {
                    request = await httpContext.Request.ReadFromJsonAsync<WebhookTriggerRequest>();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse webhook request JSON payload for '{DetectorKey}'.", detectorKey);
                }
            }

            // Find matching registered probe
            var matchedProbe = probes.FirstOrDefault(p =>
                string.Equals(p.Manifest.WebhookEndpointKey, detectorKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Manifest.Name, detectorKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Manifest.Name.Replace(" ", "-"), detectorKey, StringComparison.OrdinalIgnoreCase));

            if (matchedProbe == null)
            {
                logger.LogWarning("No registered detector probe matching key '{DetectorKey}' found.", detectorKey);
                return Results.NotFound(new { error = $"Detector probe matching key '{detectorKey}' not found." });
            }

            // Strict 15-second contract: Enforce 14-second cancellation budget
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(14));

            DetectorCredential? credential;
            try
            {
                credential = await handshakeService.EnsureCredentialsAsync(matchedProbe, cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Credentials resolution failed for probe '{ProbeName}'.", matchedProbe.Manifest.Name);
                return Results.BadRequest(new { error = $"Failed to resolve credentials: {ex.Message}" });
            }

            if (credential == null)
            {
                logger.LogError("Detector probe '{ProbeName}' has no credentials enrolled.", matchedProbe.Manifest.Name);
                return Results.BadRequest(new { error = $"Detector probe '{matchedProbe.Manifest.Name}' is not enrolled. Run handshake first." });
            }

            var triggerSource = !string.IsNullOrWhiteSpace(request?.TriggerSource)
                ? request.TriggerSource
                : "Manual";

            try
            {
                var runResult = await detectorRunner.RunAndIngestAsync(
                    matchedProbe,
                    credential.DetectorId,
                    credential.ApiKey,
                    triggerSource: triggerSource,
                    serverUrl: credential.ServerUrl,
                    ct: cts.Token);

                var report = runResult.Report;
                var responsePayload = new WebhookExecutionResponse(
                    Outcome: report.Outcome.ToString(),
                    DurationMs: report.ExecutionDurationMs,
                    FailureReason: report.ErrorMessage,
                    Findings: report.Findings);

                logger.LogInformation("Webhook execution for probe '{ProbeName}' completed: {Outcome} in {DurationMs}ms.",
                    matchedProbe.Manifest.Name, report.Outcome, report.ExecutionDurationMs);

                return Results.Ok(responsePayload);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                logger.LogError("Webhook execution for probe '{ProbeName}' timed out within 14-second budget.",
                    matchedProbe.Manifest.Name);

                var timeoutResponse = new WebhookExecutionResponse(
                    Outcome: TelemetryOutcome.ExecutionFailed.ToString(),
                    DurationMs: 14000,
                    FailureReason: "Webhook execution timed out before 15 second synchronous threshold.",
                    Findings: Array.Empty<FindingItem>());

                return Results.Ok(timeoutResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error during webhook execution for probe '{ProbeName}'.", matchedProbe.Manifest.Name);

                var errorResponse = new WebhookExecutionResponse(
                    Outcome: TelemetryOutcome.ExecutionFailed.ToString(),
                    DurationMs: 0,
                    FailureReason: ex.Message,
                    Findings: Array.Empty<FindingItem>());

                return Results.Ok(errorResponse);
            }
        });

        return endpoints;
    }
}
