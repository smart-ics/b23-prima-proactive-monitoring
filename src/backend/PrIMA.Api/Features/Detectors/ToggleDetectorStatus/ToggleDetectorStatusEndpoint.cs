using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.ToggleDetectorStatus;

public static class ToggleDetectorStatusEndpoint
{
    public static IEndpointRouteBuilder MapToggleDetectorStatusEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/detectors/{id:guid}/toggle-status", async (
            Guid id,
            ToggleDetectorStatusRequest request,
            IValidator<ToggleDetectorStatusRequest> validator,
            ToggleDetectorStatusHandler handler,
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

            var result = await handler.HandleAsync(id, request, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ToggleDetectorStatus")
        .WithTags("Detectors")
        .WithSummary("Transition detector administrative lifecycle state (Pause, Resume, Retire)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
