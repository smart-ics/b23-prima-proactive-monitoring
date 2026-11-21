using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.TakeIt;

public static class TakeItEndpoint
{
    public static IEndpointRouteBuilder MapTakeItEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/take-it", async (
            Guid id,
            TakeItHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, httpContext, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("TakeIt")
        .WithTags("Issues")
        .WithSummary("Single-tap triage flow: assign to caller and start investigation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
