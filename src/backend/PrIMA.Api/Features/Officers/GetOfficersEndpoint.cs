using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PrIMA.Api.Features.Officers;

public static class GetOfficersEndpoint
{
    public static IEndpointRouteBuilder MapOfficerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/officers", async (
            GetOfficersHandler handler,
            CancellationToken cancellationToken) =>
        {
            var officers = await handler.HandleAsync(cancellationToken);
            return Results.Ok(officers);
        })
        .RequireAuthorization()
        .WithName("GetOfficers")
        .WithTags("Officers")
        .WithSummary("Get active EDP officers")
        .Produces<List<OfficerDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
