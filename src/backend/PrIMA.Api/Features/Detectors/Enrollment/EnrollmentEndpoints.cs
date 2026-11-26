using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Features.Detectors.Enrollment.GetPendingEnrollmentTokens;
using PrIMA.Api.Features.Detectors.Enrollment.RevokeEnrollmentToken;

namespace PrIMA.Api.Features.Detectors.Enrollment;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/detectors/enrollment-tokens", async (
            GenerateEnrollmentTokenRequest? request,
            IValidator<GenerateEnrollmentTokenRequest> validator,
            GenerateEnrollmentTokenHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            request ??= new GenerateEnrollmentTokenRequest();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await handler.HandleAsync(request, httpContext, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.Created($"/api/detectors/enrollment-tokens/{result.Value.Id}", result.Value);
        })
        .RequireAuthorization()
        .WithName("GenerateEnrollmentToken")
        .WithTags("Detectors", "Enrollment")
        .WithSummary("Generate a short-lived 24-hour detector enrollment token")
        .Produces<GenerateEnrollmentTokenResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/detectors/enrollment-tokens", async (
            GetPendingEnrollmentTokensHandler handler,
            CancellationToken cancellationToken) =>
        {
            var tokens = await handler.HandleAsync(cancellationToken);
            return Results.Ok(tokens);
        })
        .RequireAuthorization()
        .WithName("GetPendingEnrollmentTokens")
        .WithTags("Detectors", "Enrollment")
        .WithSummary("List active pending enrollment tokens awaiting detector handshake")
        .Produces<IReadOnlyList<PendingEnrollmentTokenDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/detectors/enrollment-tokens/{id:guid}", async (
            Guid id,
            RevokeEnrollmentTokenHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(httpContext);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("RevokeEnrollmentToken")
        .WithTags("Detectors", "Enrollment")
        .WithSummary("Revoke an unconsumed detector enrollment token")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost("/api/detectors/handshake", async (
            DetectorHandshakeRequest? request,
            IValidator<DetectorHandshakeRequest> validator,
            DetectorHandshakeHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: "Handshake request body is required.");
            }

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

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("DetectorHandshake")
        .WithTags("Detectors", "Enrollment")
        .WithSummary("Automated detector handshake: validates enrollment token, idempotently synchronizes manifest, and provisions API key")
        .Produces<DetectorHandshakeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
