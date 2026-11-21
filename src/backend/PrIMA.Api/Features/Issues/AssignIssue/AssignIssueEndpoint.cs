using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.AssignIssue;

public static class AssignIssueEndpoint
{
    public static IEndpointRouteBuilder MapAssignIssueEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/assign", async (
            Guid id,
            AssignIssueRequest request,
            IValidator<AssignIssueRequest> validator,
            AssignIssueHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Unprocessable Entity",
                    instance: httpContext.Request.Path);
            }

            var result = await handler.HandleAsync(id, request, httpContext, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("AssignIssue")
        .WithTags("Issues")
        .WithSummary("Explicitly assign an issue to an active officer")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
