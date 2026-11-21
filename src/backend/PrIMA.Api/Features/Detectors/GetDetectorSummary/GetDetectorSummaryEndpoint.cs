using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Features.Detectors.GetDetectorList;

namespace PrIMA.Api.Features.Detectors.GetDetectorSummary;

public static class GetDetectorSummaryEndpoint
{
    public static IEndpointRouteBuilder MapGetDetectorSummaryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/detectors/summary", async (
            GetDetectorSummaryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var summary = await handler.HandleAsync(cancellationToken);
            return Results.Ok(summary);
        })
        .RequireAuthorization()
        .WithName("GetDetectorSummary")
        .WithTags("Detectors")
        .WithSummary("Get detector portfolio health summary counts")
        .Produces<DetectorSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
