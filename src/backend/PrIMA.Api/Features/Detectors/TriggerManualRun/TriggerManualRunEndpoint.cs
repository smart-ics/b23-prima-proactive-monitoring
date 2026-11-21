using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.TriggerManualRun;

public static class TriggerManualRunEndpoint
{
    public static IEndpointRouteBuilder MapTriggerManualRunEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/detectors/{id:guid}/trigger-run", async (
            Guid id,
            TriggerManualRunHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("TriggerManualRun")
        .WithTags("Detectors")
        .WithSummary("Trigger an immediate manual inspection run for a detector")
        .Produces<InspectionRunResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }
}
