using PrIMA.Api.Common;

namespace PrIMA.Api.Domain.Detectors;

public static class DetectorErrors
{
    public static DomainError NotFound(Guid id) =>
        DomainError.NotFound("Detectors.NotFound", $"Detector with ID '{id}' was not found.");

    public static DomainError Paused(Guid id) =>
        DomainError.Conflict("Detectors.Paused", $"Detector with ID '{id}' is paused and cannot accept inspection runs.");

    public static DomainError Retired(Guid id) =>
        DomainError.Conflict("Detectors.Retired", $"Detector with ID '{id}' is retired and cannot accept inspection runs.");

    public static DomainError ExecutionFailedUnsupported =>
        DomainError.Unprocessable("Detectors.ExecutionFailedUnsupported", "ExecutionFailed outcome is supported in Stage 3 with infrastructure anomaly synthesis.");

    public static DomainError DuplicateFindings =>
        DomainError.Unprocessable("Detectors.DuplicateFindings", "Payload contains duplicate targetEntityReference findings within the same batch.");
}
