using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.AcknowledgeIssue;

public static class AcknowledgeIssueEndpoint
{
    public static IEndpointRouteBuilder MapAcknowledgeIssueEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/acknowledge", async (
            Guid id,
            AcknowledgeIssueHandler handler,
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
        .WithName("AcknowledgeIssue")
        .WithTags("Issues")
        .WithSummary("Acknowledge an issue without changing assignment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
