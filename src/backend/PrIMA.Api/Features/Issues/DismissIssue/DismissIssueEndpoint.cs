using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.DismissIssue;

public static class DismissIssueEndpoint
{
    public static IEndpointRouteBuilder MapDismissIssueEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/dismiss", async (
            Guid id,
            DismissIssueRequest request,
            IValidator<DismissIssueRequest> validator,
            DismissIssueHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var officer = httpContext.User.GetCurrentOfficer();
            var actorName = !string.IsNullOrWhiteSpace(officer?.FullName)
                ? officer.FullName
                : (!string.IsNullOrWhiteSpace(httpContext.User.Identity?.Name) ? httpContext.User.Identity.Name : "EDP Officer");

            var result = await handler.HandleAsync(id, request, actorName, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DismissIssue")
        .WithTags("Issues")
        .WithSummary("Dismiss an issue with documented justification rationale")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
