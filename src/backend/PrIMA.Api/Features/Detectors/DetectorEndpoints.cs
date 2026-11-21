using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetDetectorList;
using PrIMA.Api.Features.Detectors.GetDetectorSummary;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Features.Detectors.RegisterDetector;
using PrIMA.Api.Features.Detectors.ToggleDetectorStatus;
using PrIMA.Api.Features.Detectors.TriggerManualRun;
using PrIMA.Api.Features.Detectors.UpdateDetector;

namespace PrIMA.Api.Features.Detectors;

public static class DetectorEndpoints
{
    public static IEndpointRouteBuilder MapDetectorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetDetectorListEndpoint();
        endpoints.MapGetDetectorSummaryEndpoint();
        endpoints.MapGetDetectorByIdEndpoint();
        endpoints.MapGetInspectionRunsEndpoint();
        endpoints.MapRegisterDetectorEndpoint();
        endpoints.MapUpdateDetectorEndpoint();
        endpoints.MapToggleDetectorStatusEndpoint();
        endpoints.MapTriggerManualRunEndpoint();

        return endpoints;
    }
}
