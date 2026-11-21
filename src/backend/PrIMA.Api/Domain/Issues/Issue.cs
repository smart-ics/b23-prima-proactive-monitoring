using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Domain.Issues;

public sealed class Issue
{
    private readonly List<Occurrence> _occurrences = [];
    private readonly List<InvestigationNote> _notes = [];

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
    public IReadOnlyList<InvestigationNote> Notes => _notes.AsReadOnly();

    public RemediationRecord? LatestRemediation { get; private set; }
    public VerificationRecord? LatestVerification { get; private set; }
    public DismissalRecord? Dismissal { get; private set; }

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
            Dismissal = null;
            LatestVerification = null;
            // Preserves CurrentSeverity and EscalationLevel (0 in Stage 1)
        }
        else if (Status == IssueStatus.Investigating && LatestVerification != null)
        {
            // GAP-005 & OQ-005: Recurrence on active investigating issue supersedes active verification
            LatestVerification.MarkSuperseded();
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

    // --- Lifecycle Commands ---

    // B-1 & B-2: Single-tap triage flow
    public Result TakeIt(string officerName, DateTime timestampUtc)
    {
        if (Status is IssueStatus.Resolved or IssueStatus.Dismissed)
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Investigating));

        Status = IssueStatus.Investigating;
        CurrentAssignee = officerName.Trim();
        AcknowledgedAtUtc ??= timestampUtc;
        return Result.Success();
    }

    // B-2 & OQ-002 / OQ-007: Explicit assignment
    public Result Assign(string officerName, DateTime timestampUtc)
    {
        if (Status is IssueStatus.Resolved or IssueStatus.Dismissed)
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Acknowledged));

        CurrentAssignee = officerName.Trim();

        // If Open or Reopened, transition to Acknowledged and halt unacknowledged SLA timer
        if (Status is IssueStatus.Open or IssueStatus.Reopened)
        {
            Status = IssueStatus.Acknowledged;
            AcknowledgedAtUtc = timestampUtc;
        }
        // If already Investigating, CurrentAssignee is updated without state regression (OQ-007)

        return Result.Success();
    }

    public void Assign(string officer)
    {
        Assign(officer, DateTime.UtcNow);
    }

    // B-1: Explicit acknowledgement
    public Result Acknowledge(string officerName, DateTime timestampUtc)
    {
        if (Status is not (IssueStatus.Open or IssueStatus.Reopened))
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Acknowledged));

        Status = IssueStatus.Acknowledged;
        CurrentAssignee ??= officerName.Trim();
        AcknowledgedAtUtc = timestampUtc;
        return Result.Success();
    }

    // B-2: Start investigating from Acknowledged
    public Result StartInvestigation(string officerName, DateTime timestampUtc)
    {
        if (Status != IssueStatus.Acknowledged)
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Investigating));

        Status = IssueStatus.Investigating;
        CurrentAssignee = officerName.Trim();
        return Result.Success();
    }

    public void StartInvestigation(string officer)
    {
        StartInvestigation(officer, DateTime.UtcNow);
    }

    // C-3: Append investigation note
    public Result AddNote(string noteText, string officerName, DateTime timestampUtc)
    {
        if (Status is IssueStatus.Resolved or IssueStatus.Dismissed)
            return Result.Failure(IssueErrors.ActionRequiresActiveStatus());

        if (string.IsNullOrWhiteSpace(noteText))
            return Result.Failure(IssueErrors.EmptyNoteText());

        _notes.Add(new InvestigationNote(Guid.NewGuid(), Id, officerName.Trim(), noteText.Trim(), timestampUtc));
        return Result.Success();
    }

    // D-1: Record remediation action (M-02)
    public Result RecordRemediation(string category, string description, string officerName, DateTime timestampUtc)
    {
        if (Status != IssueStatus.Investigating)
            return Result.Failure(IssueErrors.ActionRequiresStatus(IssueStatus.Investigating));

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure(IssueErrors.EmptyRemediationDescription());

        LatestRemediation = new RemediationRecord(Guid.NewGuid(), Id, category.Trim(), description.Trim(), officerName.Trim(), timestampUtc, false);

        // GAP-005 & OQ-005: Supersede prior failed or stale verification record upon remediation retry
        if (LatestVerification != null)
        {
            LatestVerification = null;
        }

        return Result.Success();
    }

    // D-3: Record manual verification result (M-03)
    public Result RecordVerification(string method, bool cleanPassed, string evidenceNotes, string verifiedBy, DateTime timestampUtc)
    {
        if (Status != IssueStatus.Investigating)
            return Result.Failure(IssueErrors.ActionRequiresStatus(IssueStatus.Investigating));

        if (LatestRemediation == null)
            return Result.Failure(IssueErrors.RemediationRequiredBeforeVerification());

        if (string.IsNullOrWhiteSpace(evidenceNotes))
            return Result.Failure(IssueErrors.EmptyVerificationEvidence());

        LatestVerification = new VerificationRecord(Guid.NewGuid(), Id, method.Trim(), cleanPassed, evidenceNotes.Trim(), verifiedBy.Trim(), timestampUtc, false);
        return Result.Success();
    }

    // D-4 & BR-PRIMA-004: Mandatory Verification Gate
    public Result MarkResolved(string officerName, DateTime timestampUtc)
    {
        if (Status != IssueStatus.Investigating)
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Resolved));

        // Gate enforcement
        if (LatestVerification == null || !LatestVerification.CleanPassed || LatestVerification.IsSuperseded)
            return Result.Failure(IssueErrors.VerificationGateFailed());

        Status = IssueStatus.Resolved;
        ResolvedAtUtc = timestampUtc;
        return Result.Success();
    }

    public void Resolve(DateTime timestampUtc)
    {
        Status = IssueStatus.Resolved;
        ResolvedAtUtc = timestampUtc;
    }

    // B-3 & BR-PRIMA-006: Documented Dismissal
    public Result Dismiss(string reason, string rationale, string officerName, DateTime timestampUtc)
    {
        if (Status is IssueStatus.Resolved or IssueStatus.Dismissed)
            return Result.Failure(IssueErrors.InvalidStateTransition(Status, IssueStatus.Dismissed));

        if (string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length < 20)
            return Result.Failure(IssueErrors.DismissalRationaleTooShort());

        Status = IssueStatus.Dismissed;
        DismissedAtUtc = timestampUtc;
        Dismissal = new DismissalRecord(Guid.NewGuid(), Id, reason.Trim(), rationale.Trim(), officerName.Trim(), timestampUtc, false);
        return Result.Success();
    }

    public void Dismiss(DateTime timestampUtc)
    {
        Status = IssueStatus.Dismissed;
        DismissedAtUtc = timestampUtc;
    }

    public void InvalidateVerification()
    {
        if (LatestVerification != null)
        {
            LatestVerification.MarkSuperseded();
        }
    }

    public void AddOccurrence(Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        _occurrences.Add(occurrence);
    }

    public void LoadNote(InvestigationNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        _notes.Add(note);
    }

    public void LoadRemediation(RemediationRecord remediation)
    {
        ArgumentNullException.ThrowIfNull(remediation);
        LatestRemediation = remediation;
    }

    public void LoadVerification(VerificationRecord verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        LatestVerification = verification;
    }

    public void LoadDismissal(DismissalRecord dismissal)
    {
        ArgumentNullException.ThrowIfNull(dismissal);
        Dismissal = dismissal;
    }
}
