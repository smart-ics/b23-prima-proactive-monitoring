using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Features.History.GetHistoricalIssues;
using PrIMA.Api.Features.History.GetReliabilityKpi;

namespace PrIMA.Api.Features.History;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetReliabilityKpiEndpoint();
        endpoints.MapGetHistoricalIssuesEndpoint();

        return endpoints;
    }
}
