using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Domain.Issues;

public sealed class Issue
{
    private readonly List<Occurrence> _occurrences = [];

    public Guid Id { get; private set; }
    public CorrelationKey CorrelationKey { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public Guid DetectorId { get; private set; }
    public string DetectorName { get; private set; } = null!;
    public MonitoringScope Scope { get; private set; }
    public IssueSeverity BaseSeverity { get; private set; }
    public IssueSeverity CurrentSeverity { get; private set; }
    public byte SeverityRank { get; private set; }
    public IssueStatus Status { get; private set; }
    public int EscalationLevel { get; private set; }
    public int OccurrenceCount { get; private set; }
    public string? CurrentAssignee { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastOccurrenceAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? DismissedAtUtc { get; private set; }
    public DateTime? ReopenedAtUtc { get; private set; }

    public IReadOnlyList<Occurrence> Occurrences => _occurrences.AsReadOnly();

    public bool IsActive => Status is IssueStatus.Open or IssueStatus.Acknowledged or IssueStatus.Investigating or IssueStatus.Reopened;
    public bool IsClosed => Status is IssueStatus.Resolved or IssueStatus.Dismissed;

    // Parameterless constructor for Dapper / reflection hydration
    private Issue() { }

    public Issue(
        Guid id,
        CorrelationKey correlationKey,
        string title,
        Guid detectorId,
        string detectorName,
        MonitoringScope scope,
        IssueSeverity baseSeverity,
        IssueSeverity currentSeverity,
        byte severityRank,
        IssueStatus status,
        int escalationLevel,
        int occurrenceCount,
        string? currentAssignee,
        DateTime createdAtUtc,
        DateTime lastOccurrenceAtUtc,
        DateTime? acknowledgedAtUtc = null,
        DateTime? resolvedAtUtc = null,
        DateTime? dismissedAtUtc = null,
        DateTime? reopenedAtUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Issue ID cannot be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(correlationKey);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Issue title cannot be empty.", nameof(title));
        if (detectorId == Guid.Empty) throw new ArgumentException("Detector ID cannot be empty.", nameof(detectorId));
        if (string.IsNullOrWhiteSpace(detectorName)) throw new ArgumentException("Detector name cannot be empty.", nameof(detectorName));
        if (occurrenceCount < 1) throw new ArgumentOutOfRangeException(nameof(occurrenceCount), occurrenceCount, "Occurrence count must be >= 1.");

        Id = id;
        CorrelationKey = correlationKey;
        Title = title.Trim();
        DetectorId = detectorId;
        DetectorName = detectorName.Trim();
        Scope = scope;
        BaseSeverity = baseSeverity;
        CurrentSeverity = currentSeverity;
        SeverityRank = severityRank;
        Status = status;
        EscalationLevel = escalationLevel;
        OccurrenceCount = occurrenceCount;
        CurrentAssignee = currentAssignee?.Trim();
        CreatedAtUtc = createdAtUtc;
        LastOccurrenceAtUtc = lastOccurrenceAtUtc;
        AcknowledgedAtUtc = acknowledgedAtUtc;
        ResolvedAtUtc = resolvedAtUtc;
        DismissedAtUtc = dismissedAtUtc;
        ReopenedAtUtc = reopenedAtUtc;
    }

    public static Issue Create(
        CorrelationKey correlationKey,
        string title,
        Guid detectorId,
        string detectorName,
        MonitoringScope scope,
        IssueSeverity baseSeverity,
        Guid inspectionRunId,
        TelemetryEvidence evidence,
        DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(correlationKey);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Issue title cannot be empty.", nameof(title));
        if (detectorId == Guid.Empty) throw new ArgumentException("Detector ID cannot be empty.", nameof(detectorId));
        if (string.IsNullOrWhiteSpace(detectorName)) throw new ArgumentException("Detector name cannot be empty.", nameof(detectorName));
        if (inspectionRunId == Guid.Empty) throw new ArgumentException("Inspection run ID cannot be empty.", nameof(inspectionRunId));
        ArgumentNullException.ThrowIfNull(evidence);

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            CorrelationKey = correlationKey,
            Title = title.Trim(),
            DetectorId = detectorId,
            DetectorName = detectorName.Trim(),
            Scope = scope,
            BaseSeverity = baseSeverity,
            CurrentSeverity = baseSeverity,
            SeverityRank = Domain.Issues.SeverityRank.From(baseSeverity),
            Status = IssueStatus.Open,
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = null,
            CreatedAtUtc = timestampUtc,
            LastOccurrenceAtUtc = timestampUtc,
            AcknowledgedAtUtc = null,
            ResolvedAtUtc = null,
            DismissedAtUtc = null,
            ReopenedAtUtc = null
        };

        var initialOccurrence = Occurrence.Create(
            issue.Id,
            inspectionRunId,
            occurrenceNumber: 1,
            evidence,
            timestampUtc);

        issue._occurrences.Add(initialOccurrence);
        return issue;
    }

    public Occurrence RecordRecurringOccurrence(
        Guid inspectionRunId,
        TelemetryEvidence evidence,
        DateTime timestampUtc)
    {
        if (inspectionRunId == Guid.Empty)
        {
            throw new ArgumentException("Inspection run ID cannot be empty.", nameof(inspectionRunId));
        }
        ArgumentNullException.ThrowIfNull(evidence);

        OccurrenceCount++;
        LastOccurrenceAtUtc = timestampUtc;

        if (Status is IssueStatus.Resolved or IssueStatus.Dismissed)
        {
            // BR-PRIMA-002: Automatic Recurrence Reopening & GAP-005 governance audit reset
            Status = IssueStatus.Reopened;
            ReopenedAtUtc = timestampUtc;
            CurrentAssignee = null;
            AcknowledgedAtUtc = null;
            ResolvedAtUtc = null;
            DismissedAtUtc = null;
            // Preserves CurrentSeverity and EscalationLevel (0 in Stage 1)
        }
        // else on active issues (Open, Acknowledged, Investigating, Reopened):
        // Preserves Title and BaseSeverity

        var occurrence = Occurrence.Create(
            Id,
            inspectionRunId,
            occurrenceNumber: OccurrenceCount,
            evidence,
            timestampUtc);

        _occurrences.Add(occurrence);
        return occurrence;
    }

    public void Acknowledge(string officer, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(officer))
        {
            throw new ArgumentException("Officer name cannot be empty.", nameof(officer));
        }

        if (Status != IssueStatus.Open && Status != IssueStatus.Reopened)
        {
            throw new InvalidOperationException($"Cannot acknowledge issue in status '{Status}'.");
        }

        Status = IssueStatus.Acknowledged;
        CurrentAssignee = officer.Trim();
        AcknowledgedAtUtc = timestampUtc;
    }

    public void Assign(string officer)
    {
        if (string.IsNullOrWhiteSpace(officer))
        {
            throw new ArgumentException("Officer name cannot be empty.", nameof(officer));
        }

        CurrentAssignee = officer.Trim();
    }

    public void StartInvestigation(string officer)
    {
        if (string.IsNullOrWhiteSpace(officer))
        {
            throw new ArgumentException("Officer name cannot be empty.", nameof(officer));
        }

        CurrentAssignee = officer.Trim();
        Status = IssueStatus.Investigating;
    }

    public void Resolve(DateTime timestampUtc)
    {
        Status = IssueStatus.Resolved;
        ResolvedAtUtc = timestampUtc;
    }

    public void Dismiss(DateTime timestampUtc)
    {
        Status = IssueStatus.Dismissed;
        DismissedAtUtc = timestampUtc;
    }

    public void AddOccurrence(Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        _occurrences.Add(occurrence);
    }
}
