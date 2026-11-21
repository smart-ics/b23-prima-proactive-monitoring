using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PrIMA.Api.Features.History.GetHistoricalIssues;

public static class GetHistoricalIssuesEndpoint
{
    public static IEndpointRouteBuilder MapGetHistoricalIssuesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/history/issues", async (
            [AsParameters] GetHistoricalIssuesQuery query,
            IValidator<GetHistoricalIssuesQuery> validator,
            GetHistoricalIssuesHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(query, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Unprocessable Entity",
                    instance: httpContext.Request.Path);
            }

            var response = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetHistoricalIssues")
        .WithTags("History")
        .WithSummary("Get filterable historical issue ledger with status-aware lifecycle date filtering")
        .Produces<HistoricalIssuesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
