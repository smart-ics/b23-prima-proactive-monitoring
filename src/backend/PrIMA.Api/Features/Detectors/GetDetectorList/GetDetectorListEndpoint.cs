using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PrIMA.Api.Features.Detectors.GetDetectorList;

public static class GetDetectorListEndpoint
{
    public static IEndpointRouteBuilder MapGetDetectorListEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/detectors", async (
            [AsParameters] GetDetectorListQuery query,
            GetDetectorListHandler handler,
            CancellationToken cancellationToken) =>
        {
            var detectors = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(detectors);
        })
        .RequireAuthorization()
        .WithName("GetDetectors")
        .WithTags("Detectors")
        .WithSummary("Get detector portfolio list")
        .Produces<List<DetectorListItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
