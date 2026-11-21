using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.ResolveIssue;

public static class ResolveIssueEndpoint
{
    public static IEndpointRouteBuilder MapResolveIssueEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/resolve", async (
            Guid id,
            ResolveIssueHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var officer = httpContext.User.GetCurrentOfficer();
            var actorName = !string.IsNullOrWhiteSpace(officer?.FullName)
                ? officer.FullName
                : (!string.IsNullOrWhiteSpace(httpContext.User.Identity?.Name) ? httpContext.User.Identity.Name : "EDP Officer");

            var result = await handler.HandleAsync(id, actorName, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ResolveIssue")
        .WithTags("Issues")
        .WithSummary("Mark an issue as resolved (enforces verification gate)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
