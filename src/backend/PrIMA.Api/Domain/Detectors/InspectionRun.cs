namespace PrIMA.Api.Domain.Detectors;

public sealed class InspectionRun
{
    public Guid Id { get; private set; }
    public Guid DetectorId { get; private set; }
    public DateTime ExecutionTimestampUtc { get; private set; }
    public TriggerSource TriggerSource { get; private set; }
    public InspectionOutcome Outcome { get; private set; }
    public int DurationMs { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Parameterless constructor for Dapper / reflection hydration
    private InspectionRun() { }

    public InspectionRun(
        Guid id,
        Guid detectorId,
        DateTime executionTimestampUtc,
        TriggerSource triggerSource,
        InspectionOutcome outcome,
        int durationMs,
        string? failureReason,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Inspection run ID cannot be empty.", nameof(id));
        if (detectorId == Guid.Empty) throw new ArgumentException("Detector ID cannot be empty.", nameof(detectorId));
        if (durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs), "Duration cannot be negative.");

        Id = id;
        DetectorId = detectorId;
        ExecutionTimestampUtc = executionTimestampUtc;
        TriggerSource = triggerSource;
        Outcome = outcome;
        DurationMs = durationMs;
        FailureReason = failureReason;
        CreatedAtUtc = createdAtUtc;
    }

    public static InspectionRun Create(
        Guid detectorId,
        DateTime executionTimestampUtc,
        TriggerSource triggerSource,
        InspectionOutcome outcome,
        int durationMs,
        string? failureReason = null,
        DateTime? createdAtUtc = null)
    {
        return new InspectionRun(
            Guid.NewGuid(),
            detectorId,
            executionTimestampUtc,
            triggerSource,
            outcome,
            durationMs,
            failureReason,
            createdAtUtc ?? DateTime.UtcNow);
    }
}
