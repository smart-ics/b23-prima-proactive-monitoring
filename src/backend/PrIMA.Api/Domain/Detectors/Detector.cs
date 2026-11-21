using PrIMA.Api.Common;

namespace PrIMA.Api.Domain.Detectors;

public sealed class Detector
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public MonitoringScope Scope { get; private set; }
    public string TargetModule { get; private set; } = null!;
    public int ExpectedCadenceSeconds { get; private set; }
    public string? WebhookUrl { get; private set; }
    public AdminStatus AdminStatus { get; private set; }
    public DetectorHealth HealthStatus { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }
    public DateTime? LastHealthyHeartbeatUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive => AdminStatus == AdminStatus.Active;
    public bool IsPaused => AdminStatus == AdminStatus.Paused;
    public bool IsRetired => AdminStatus == AdminStatus.Retired;

    // Parameterless constructor for Dapper / reflection hydration
    private Detector() { }

    public Detector(
        Guid id,
        string name,
        string description,
        MonitoringScope scope,
        string targetModule,
        int expectedCadenceSeconds,
        string? webhookUrl,
        AdminStatus adminStatus,
        DetectorHealth healthStatus,
        DateTime? lastRunAtUtc,
        DateTime? lastHealthyHeartbeatUtc,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Detector ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Detector name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Detector description cannot be empty.", nameof(description));
        if (string.IsNullOrWhiteSpace(targetModule)) throw new ArgumentException("Target module cannot be empty.", nameof(targetModule));
        if (expectedCadenceSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(expectedCadenceSeconds), "Expected cadence must be greater than zero.");

        Id = id;
        Name = name.Trim();
        Description = description.Trim();
        Scope = scope;
        TargetModule = targetModule.Trim();
        ExpectedCadenceSeconds = expectedCadenceSeconds;
        WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl.Trim();
        AdminStatus = adminStatus;
        HealthStatus = healthStatus;
        LastRunAtUtc = lastRunAtUtc;
        LastHealthyHeartbeatUtc = lastHealthyHeartbeatUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public static Detector Create(
        string name,
        string description,
        MonitoringScope scope,
        string targetModule,
        int expectedCadenceSeconds,
        string? webhookUrl = null,
        DateTime? createdAtUtc = null)
    {
        return new Detector(
            Guid.NewGuid(),
            name,
            description,
            scope,
            targetModule,
            expectedCadenceSeconds,
            webhookUrl,
            AdminStatus.Active,
            DetectorHealth.Healthy,
            lastRunAtUtc: null,
            lastHealthyHeartbeatUtc: null,
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    public Result UpdateConfiguration(
        string description,
        MonitoringScope scope,
        string targetModule,
        int expectedCadenceSeconds,
        string? webhookUrl)
    {
        if (IsRetired)
            return Result.Failure(DetectorErrors.CannotModifyRetiredDetector);

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure(DetectorErrors.EmptyDescription);

        if (string.IsNullOrWhiteSpace(targetModule))
            return Result.Failure(DetectorErrors.EmptyTargetModule);

        if (expectedCadenceSeconds <= 0)
            return Result.Failure(DetectorErrors.InvalidCadence);

        Description = description.Trim();
        Scope = scope;
        TargetModule = targetModule.Trim();
        ExpectedCadenceSeconds = expectedCadenceSeconds;
        WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl.Trim();
        return Result.Success();
    }

    public Result Pause()
    {
        if (IsRetired) return Result.Failure(DetectorErrors.CannotModifyRetiredDetector);
        if (IsPaused) return Result.Failure(DetectorErrors.AlreadyPaused);

        AdminStatus = AdminStatus.Paused;
        return Result.Success();
    }

    public Result Resume()
    {
        if (IsRetired) return Result.Failure(DetectorErrors.CannotModifyRetiredDetector);
        if (IsActive) return Result.Failure(DetectorErrors.AlreadyActive);

        AdminStatus = AdminStatus.Active;
        return Result.Success();
    }

    public Result Retire()
    {
        if (IsRetired) return Result.Failure(DetectorErrors.AlreadyRetired);

        AdminStatus = AdminStatus.Retired;
        return Result.Success();
    }

    public bool IsSilenceThresholdExceeded(DateTime currentUtc)
    {
        if (!IsActive) return false;
        var baseline = LastHealthyHeartbeatUtc ?? CreatedAtUtc;
        var elapsed = (currentUtc - baseline).TotalSeconds;
        return elapsed > (ExpectedCadenceSeconds * 2.0);
    }

    public void MarkSilent(DateTime timestampUtc)
    {
        HealthStatus = DetectorHealth.Silent;
    }

    public void MarkFailing(DateTime timestampUtc)
    {
        LastRunAtUtc = timestampUtc;
        HealthStatus = DetectorHealth.Failing;
    }

    public void RecordSuccessfulRun(DateTime timestampUtc)
    {
        LastRunAtUtc = timestampUtc;
        LastHealthyHeartbeatUtc = timestampUtc;
        HealthStatus = DetectorHealth.Healthy;
    }

    public void RecordHeartbeat(DateTime timestamp, DetectorHealth health)
    {
        if (IsRetired)
        {
            throw new InvalidOperationException($"Cannot record heartbeat for retired detector '{Name}' ({Id}).");
        }

        LastRunAtUtc = timestamp;
        HealthStatus = health;

        if (health == DetectorHealth.Healthy)
        {
            LastHealthyHeartbeatUtc = timestamp;
        }
    }
}
