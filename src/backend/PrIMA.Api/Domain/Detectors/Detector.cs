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
        WebhookUrl = webhookUrl?.Trim();
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

    public void Pause()
    {
        if (IsRetired)
        {
            throw new InvalidOperationException($"Cannot pause retired detector '{Name}' ({Id}).");
        }

        AdminStatus = AdminStatus.Paused;
    }

    public void Resume()
    {
        if (IsRetired)
        {
            throw new InvalidOperationException($"Cannot resume retired detector '{Name}' ({Id}).");
        }

        AdminStatus = AdminStatus.Active;
    }

    public void Retire()
    {
        AdminStatus = AdminStatus.Retired;
    }
}
