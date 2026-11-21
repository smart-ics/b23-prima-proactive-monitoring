using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.UpdateDetector;

public static class UpdateDetectorEndpoint
{
    public static IEndpointRouteBuilder MapUpdateDetectorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/detectors/{id:guid}", async (
            Guid id,
            UpdateDetectorRequest request,
            IValidator<UpdateDetectorRequest> validator,
            UpdateDetectorHandler handler,
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
        .WithName("UpdateDetector")
        .WithTags("Detectors")
        .WithSummary("Update detector configuration (name is immutable)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
