using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.GetIssueById;

public static class GetIssueByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetIssueByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/issues/{id:guid}", async (
            Guid id,
            GetIssueByIdHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var issue = await handler.HandleAsync(id, cancellationToken);
            if (issue is null)
            {
                return IssueErrors.NotFound(id).ToProblemDetails(httpContext);
            }

            return Results.Ok(issue);
        })
        .RequireAuthorization()
        .WithName("GetIssueById")
        .WithTags("Issues")
        .WithSummary("Get composite issue detail by ID")
        .Produces<IssueDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
