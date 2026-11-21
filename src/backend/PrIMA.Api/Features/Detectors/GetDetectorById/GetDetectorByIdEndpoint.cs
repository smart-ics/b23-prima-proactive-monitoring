using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public static class GetDetectorByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetDetectorByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/detectors/{id:guid}", async (
            Guid id,
            GetDetectorByIdHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var detector = await handler.HandleAsync(id, cancellationToken);
            if (detector is null)
            {
                return DetectorErrors.NotFound(id).ToProblemDetails(httpContext);
            }

            return Results.Ok(detector);
        })
        .RequireAuthorization()
        .WithName("GetDetectorById")
        .WithTags("Detectors")
        .WithSummary("Get composite detector detail by ID")
        .Produces<DetectorDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
