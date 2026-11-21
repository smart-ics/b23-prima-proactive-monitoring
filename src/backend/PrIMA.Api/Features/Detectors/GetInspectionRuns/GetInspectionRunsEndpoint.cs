using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.GetInspectionRuns;

public static class GetInspectionRunsEndpoint
{
    public static IEndpointRouteBuilder MapGetInspectionRunsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/detectors/{id:guid}/runs", async (
            Guid id,
            [AsParameters] GetInspectionRunsQuery query,
            GetInspectionRunsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await handler.HandleAsync(id, query, cancellationToken);
            if (response is null)
            {
                return DetectorErrors.NotFound(id).ToProblemDetails(httpContext);
            }

            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetInspectionRuns")
        .WithTags("Detectors")
        .WithSummary("Get historical inspection runs for a detector")
        .Produces<InspectionRunLogResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
