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

    public static readonly DomainError CannotModifyRetiredDetector =
        DomainError.Conflict("Detectors.CannotModifyRetiredDetector", "Cannot modify or transition a retired detector.");

    public static readonly DomainError EmptyDescription =
        DomainError.Unprocessable("Detectors.EmptyDescription", "Detector description cannot be empty.");

    public static readonly DomainError EmptyTargetModule =
        DomainError.Unprocessable("Detectors.EmptyTargetModule", "Detector target module cannot be empty.");

    public static readonly DomainError InvalidCadence =
        DomainError.Unprocessable("Detectors.InvalidCadence", "Detector cadence must be greater than zero seconds.");

    public static readonly DomainError AlreadyPaused =
        DomainError.Conflict("Detectors.AlreadyPaused", "Detector is already paused.");

    public static readonly DomainError AlreadyActive =
        DomainError.Conflict("Detectors.AlreadyActive", "Detector is already active.");

    public static readonly DomainError AlreadyRetired =
        DomainError.Conflict("Detectors.AlreadyRetired", "Detector is already retired.");

    public static DomainError NoWebhookConfigured(Guid id) =>
        DomainError.Conflict("Detectors.NoWebhookConfigured", $"Detector with ID '{id}' does not have a webhook URL configured.");

    public static readonly DomainError DuplicateName =
        DomainError.Conflict("Detectors.DuplicateName", "A detector with this name already exists.");
}
