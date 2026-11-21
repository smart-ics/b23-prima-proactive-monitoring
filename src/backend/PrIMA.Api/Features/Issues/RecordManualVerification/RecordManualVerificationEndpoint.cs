using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.RecordManualVerification;

public static class RecordManualVerificationEndpoint
{
    public static IEndpointRouteBuilder MapRecordManualVerificationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/verification/manual", async (
            Guid id,
            RecordManualVerificationRequest request,
            IValidator<RecordManualVerificationRequest> validator,
            RecordManualVerificationHandler handler,
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
        .WithName("RecordManualVerification")
        .WithTags("Issues")
        .WithSummary("Record a manual verification outcome on an issue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
