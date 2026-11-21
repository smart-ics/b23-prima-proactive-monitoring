using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.GetDashboardFeed;

public static class GetDashboardFeedEndpoint
{
    public static IEndpointRouteBuilder MapGetDashboardFeedEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/issues/dashboard-feed", async (
            [AsParameters] GetDashboardFeedQuery query,
            GetDashboardFeedHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(query, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("GetDashboardFeed")
        .WithTags("Issues")
        .WithSummary("Get active issue dashboard feed with operational counters and multi-attribute filters")
        .Produces<GetDashboardFeedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
