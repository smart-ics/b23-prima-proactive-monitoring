using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.TriggerVerificationRun;

public static class TriggerVerificationRunEndpoint
{
    public static IEndpointRouteBuilder MapTriggerVerificationRunEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/verification/trigger-run", async (
            Guid id,
            TriggerVerificationRunHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var officer = httpContext.GetCurrentOfficer();
            if (officer is null)
            {
                return Results.Unauthorized();
            }

            var result = await handler.HandleAsync(id, officer, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("TriggerVerificationRun")
        .WithTags("Issues")
        .WithSummary("Trigger an automated probe re-run to verify issue remediation")
        .Produces<VerificationResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }
}
