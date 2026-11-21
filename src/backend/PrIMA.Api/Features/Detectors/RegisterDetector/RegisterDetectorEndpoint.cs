using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.GetDetectorById;

namespace PrIMA.Api.Features.Detectors.RegisterDetector;

public static class RegisterDetectorEndpoint
{
    public static IEndpointRouteBuilder MapRegisterDetectorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/detectors", async (
            RegisterDetectorRequest request,
            IValidator<RegisterDetectorRequest> validator,
            RegisterDetectorHandler handler,
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

            var result = await handler.HandleAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Created($"/api/detectors/{result.Value.Id}", result.Value);
        })
        .RequireAuthorization()
        .WithName("RegisterDetector")
        .WithTags("Detectors")
        .WithSummary("Register a new inspection detector")
        .Produces<DetectorDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
