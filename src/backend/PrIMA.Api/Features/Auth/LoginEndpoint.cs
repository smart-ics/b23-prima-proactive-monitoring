using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Auth;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            LoginHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await handler.HandleAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails(context);
            }

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithTags("Auth")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
