using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.AddNote;

public static class AddNoteEndpoint
{
    public static IEndpointRouteBuilder MapAddNoteEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/issues/{id:guid}/notes", async (
            Guid id,
            AddNoteRequest request,
            IValidator<AddNoteRequest> validator,
            AddNoteHandler handler,
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

            return Results.Created($"/api/issues/{id}/notes/{result.Value.Id}", result.Value);
        })
        .RequireAuthorization()
        .WithName("AddNote")
        .WithTags("Issues")
        .WithSummary("Append an investigation note to an active issue")
        .Produces<NoteDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
