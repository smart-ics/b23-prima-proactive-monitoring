using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PrIMA.Api.Features.History.GetReliabilityKpi;

public static class GetReliabilityKpiEndpoint
{
    public static IEndpointRouteBuilder MapGetReliabilityKpiEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/history/reliability-kpi", async (
            [AsParameters] GetReliabilityKpiQuery query,
            IValidator<GetReliabilityKpiQuery> validator,
            GetReliabilityKpiHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(query, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Unprocessable Entity",
                    instance: httpContext.Request.Path);
            }

            var response = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetReliabilityKpi")
        .WithTags("History")
        .WithSummary("Get period-close historical reliability KPI metrics and Scope/Severity breakdowns")
        .Produces<ReliabilityKpiResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
