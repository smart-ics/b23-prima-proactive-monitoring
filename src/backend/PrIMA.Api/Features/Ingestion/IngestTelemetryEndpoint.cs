using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Ingestion;

public static class IngestTelemetryEndpoint
{
    public static IEndpointRouteBuilder MapIngestTelemetryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ingestion/telemetry", async (
            IngestTelemetryRequest request,
            IValidator<IngestTelemetryRequest> validator,
            IngestTelemetryHandler handler,
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

            var result = await handler.HandleAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Ok(result.Value);
        })
        .AddEndpointFilter<ApiKeyEndpointFilter>()
        .AllowAnonymous()
        .WithName("IngestTelemetry")
        .WithTags("Ingestion")
        .Produces<IngestTelemetryResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
