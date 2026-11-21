using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.RecordRemediation;

public static class RecordRemediationEndpoint
{
    public static IEndpointRouteBuilder MapRecordRemediationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/remediation", async (
            Guid id,
            RecordRemediationRequest request,
            IValidator<RecordRemediationRequest> validator,
            RecordRemediationHandler handler,
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

            var officer = httpContext.GetCurrentOfficer();
            if (officer is null)
            {
                return Results.Unauthorized();
            }

            var result = await handler.HandleAsync(id, request, officer, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("RecordRemediation")
        .WithTags("Issues")
        .WithSummary("Record an operational remediation action on an issue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
