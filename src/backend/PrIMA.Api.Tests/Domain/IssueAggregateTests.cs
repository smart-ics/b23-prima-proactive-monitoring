using FluentAssertions;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Tests.Domain;

public class IssueAggregateTests
{
    private static TelemetryEvidence CreateSampleEvidence(string recordKey = "REC-100")
    {
        return new TelemetryEvidence(
            module: "Billing",
            sourceTable: "BillingJournals",
            recordKeys: recordKey,
            debitTotal: 1000000m,
            creditTotal: 950000m,
            varianceDelta: 50000m,
            trace: "Debit credit imbalance detected in ledger entry.");
    }

    [Theory]
    [InlineData(IssueSeverity.Critical, (byte)4)]
    [InlineData(IssueSeverity.High, (byte)3)]
    [InlineData(IssueSeverity.Medium, (byte)2)]
    [InlineData(IssueSeverity.Low, (byte)1)]
    public void Create_InitializesIssueWithOpenStatusAndDefaultInvariants(
        IssueSeverity severity,
        byte expectedRank)
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var key = CorrelationKey.Create(detectorId, "BILLING-ENTRY-001");
        var now = DateTime.UtcNow;
        var runId = Guid.NewGuid();
        var evidence = CreateSampleEvidence();

        // Act
        var issue = Issue.Create(
            correlationKey: key,
            title: "Unbalanced Ledger Entry",
            detectorId: detectorId,
            detectorName: "Billing Journal Balance Checker",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: severity,
            inspectionRunId: runId,
            evidence: evidence,
            timestampUtc: now);

        // Assert
        issue.Id.Should().NotBeEmpty();
        issue.CorrelationKey.Should().Be(key);
        issue.Title.Should().Be("Unbalanced Ledger Entry");
        issue.DetectorId.Should().Be(detectorId);
        issue.DetectorName.Should().Be("Billing Journal Balance Checker");
        issue.Scope.Should().Be(MonitoringScope.DataIntegrity);
        issue.BaseSeverity.Should().Be(severity);
        issue.CurrentSeverity.Should().Be(severity);
        issue.SeverityRank.Should().Be(expectedRank);
        issue.Status.Should().Be(IssueStatus.Open);
        issue.EscalationLevel.Should().Be(0);
        issue.OccurrenceCount.Should().Be(1);
        issue.CurrentAssignee.Should().BeNull();
        issue.CreatedAtUtc.Should().Be(now);
        issue.LastOccurrenceAtUtc.Should().Be(now);
        issue.AcknowledgedAtUtc.Should().BeNull();
        issue.ResolvedAtUtc.Should().BeNull();
        issue.DismissedAtUtc.Should().BeNull();
        issue.ReopenedAtUtc.Should().BeNull();
        issue.IsActive.Should().BeTrue();
        issue.IsClosed.Should().BeFalse();

        // Initial Occurrence Check
        issue.Occurrences.Should().HaveCount(1);
        var initial = issue.Occurrences[0];
        initial.IssueId.Should().Be(issue.Id);
        initial.InspectionRunId.Should().Be(runId);
        initial.OccurrenceNumber.Should().Be(1);
        initial.Evidence.Should().Be(evidence);
        initial.TimestampUtc.Should().Be(now);
    }

    [Fact]
    public void RecordRecurringOccurrence_OnActiveIssue_AppendsOccurrenceAndIncrementsCountWhilePreservingImmutables()
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var key = CorrelationKey.Create(detectorId, "BILLING-ENTRY-002");
        var initialTime = DateTime.UtcNow.AddMinutes(-30);
        var issue = Issue.Create(
            correlationKey: key,
            title: "Original Issue Title",
            detectorId: detectorId,
            detectorName: "Billing Journal Balance Checker",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.High,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence("REC-001"),
            timestampUtc: initialTime);

        // Acknowledge the issue to put it in an active non-Open state
        var ackTime = DateTime.UtcNow.AddMinutes(-20);
        issue.Acknowledge("ahmad.budi", ackTime);

        var recurrenceTime = DateTime.UtcNow;
        var runId2 = Guid.NewGuid();
        var evidence2 = CreateSampleEvidence("REC-002");

        // Act
        var occurrence = issue.RecordRecurringOccurrence(runId2, evidence2, recurrenceTime);

        // Assert
        occurrence.OccurrenceNumber.Should().Be(2);
        occurrence.InspectionRunId.Should().Be(runId2);
        occurrence.TimestampUtc.Should().Be(recurrenceTime);

        issue.OccurrenceCount.Should().Be(2);
        issue.LastOccurrenceAtUtc.Should().Be(recurrenceTime);
        issue.Occurrences.Should().HaveCount(2);

        // Immutables & state preservation on active issue
        issue.Title.Should().Be("Original Issue Title");
        issue.BaseSeverity.Should().Be(IssueSeverity.High);
        issue.CurrentSeverity.Should().Be(IssueSeverity.High);
        issue.SeverityRank.Should().Be(SeverityRank.High);
        issue.Status.Should().Be(IssueStatus.Acknowledged); // Status remains active
        issue.CurrentAssignee.Should().Be("ahmad.budi");
        issue.AcknowledgedAtUtc.Should().Be(ackTime);
        issue.ReopenedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void RecordRecurringOccurrence_OnClosedIssue_TriggersBRPRIMA002ReopenAndResetsGovernanceState(
        IssueStatus closedStatus)
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var key = CorrelationKey.Create(detectorId, "PATIENT-ADM-0099");
        var initialTime = DateTime.UtcNow.AddHours(-5);
        var issue = Issue.Create(
            correlationKey: key,
            title: "Patient Admission Discrepancy",
            detectorId: detectorId,
            detectorName: "Patient Admission Discharge Sync",
            scope: MonitoringScope.ProcessIntegrity,
            baseSeverity: IssueSeverity.Critical,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: initialTime);

        // Assign and acknowledge
        issue.Acknowledge("siti.rahma", initialTime.AddMinutes(10));

        // Close the issue
        var closureTime = initialTime.AddHours(2);
        if (closedStatus == IssueStatus.Resolved)
        {
            issue.Resolve(closureTime);
        }
        else
        {
            issue.Dismiss(closureTime);
        }

        issue.IsClosed.Should().BeTrue();

        var recurrenceTime = DateTime.UtcNow;
        var recurrenceRunId = Guid.NewGuid();
        var recurrenceEvidence = CreateSampleEvidence("PATIENT-99-RECURRED");

        // Act - BR-PRIMA-002: Automatic Recurrence Reopening
        var occurrence = issue.RecordRecurringOccurrence(recurrenceRunId, recurrenceEvidence, recurrenceTime);

        // Assert
        occurrence.OccurrenceNumber.Should().Be(2);
        occurrence.IssueId.Should().Be(issue.Id);
        occurrence.InspectionRunId.Should().Be(recurrenceRunId);

        // Reopening transitions
        issue.Status.Should().Be(IssueStatus.Reopened);
        issue.IsActive.Should().BeTrue();
        issue.IsClosed.Should().BeFalse();
        issue.OccurrenceCount.Should().Be(2);
        issue.LastOccurrenceAtUtc.Should().Be(recurrenceTime);
        issue.ReopenedAtUtc.Should().Be(recurrenceTime);

        // Cleared governance & assignee fields
        issue.CurrentAssignee.Should().BeNull();
        issue.AcknowledgedAtUtc.Should().BeNull();
        issue.ResolvedAtUtc.Should().BeNull();
        issue.DismissedAtUtc.Should().BeNull();

        // Preserved severity, title, escalation
        issue.Title.Should().Be("Patient Admission Discrepancy");
        issue.BaseSeverity.Should().Be(IssueSeverity.Critical);
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.SeverityRank.Should().Be(SeverityRank.Critical);
        issue.EscalationLevel.Should().Be(0);
    }

    [Fact]
    public void EscalateSeverity_StepsThroughSeveritiesAndCapsAtCritical()
    {
        // Arrange
        var issue = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "ESCALATE-01"),
            title: "Low Severity Issue",
            detectorId: Guid.NewGuid(),
            detectorName: "Escalation Test Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Low,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: DateTime.UtcNow);

        issue.CurrentSeverity.Should().Be(IssueSeverity.Low);
        issue.SeverityRank.Should().Be(SeverityRank.Low);
        issue.EscalationLevel.Should().Be(0);

        // Act & Assert 1: Low -> Medium
        var res1 = issue.EscalateSeverity("SLA breach", DateTime.UtcNow);
        res1.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Medium);
        issue.SeverityRank.Should().Be(SeverityRank.Medium);
        issue.EscalationLevel.Should().Be(1);

        // Act & Assert 2: Medium -> High
        var res2 = issue.EscalateSeverity("Recurrence", DateTime.UtcNow);
        res2.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.High);
        issue.SeverityRank.Should().Be(SeverityRank.High);
        issue.EscalationLevel.Should().Be(2);

        // Act & Assert 3: High -> Critical
        var res3 = issue.EscalateSeverity("Repeated SLA breach", DateTime.UtcNow);
        res3.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.SeverityRank.Should().Be(SeverityRank.Critical);
        issue.EscalationLevel.Should().Be(3);

        // Act & Assert 4: Critical -> Capped (returns Success, remains Critical, escalation level remains 3)
        var res4 = issue.EscalateSeverity("Another breach", DateTime.UtcNow);
        res4.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.SeverityRank.Should().Be(SeverityRank.Critical);
        issue.EscalationLevel.Should().Be(3);
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void EscalateSeverity_OnClosedIssue_ReturnsFailure(IssueStatus status)
    {
        // Arrange
        var issue = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "ESCALATE-CLOSED"),
            title: "Closed Issue",
            detectorId: Guid.NewGuid(),
            detectorName: "Escalation Test Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Low,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: DateTime.UtcNow.AddHours(-2));

        if (status == IssueStatus.Resolved)
        {
            issue.Resolve(DateTime.UtcNow.AddHours(-1));
        }
        else
        {
            issue.Dismiss(DateTime.UtcNow.AddHours(-1));
        }

        // Act
        var result = issue.EscalateSeverity("SLA breach", DateTime.UtcNow);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(IssueErrors.ActionRequiresActiveStatus().Code);
    }

    [Fact]
    public void CheckSlaEscalation_EvaluatesSlaTimeoutAndWindowMultiplesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.AddMinutes(-40);
        var issue = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "SLA-01"),
            title: "Unack Issue",
            detectorId: Guid.NewGuid(),
            detectorName: "SLA Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Low,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: baseTime);

        var slaTimeout = TimeSpan.FromMinutes(30);

        // Act: 40 minutes elapsed > 30 minutes (slaTimeout * (0 + 1))
        var breached = issue.CheckSlaEscalation(slaTimeout, DateTime.UtcNow, out var reason);

        // Assert
        breached.Should().BeTrue();
        reason.Should().Contain("Issue unacknowledged for 40 minutes");
        reason.Should().Contain("SLA window: 30m");

        // Now escalate it once (level becomes 1)
        issue.EscalateSeverity("SLA breach", DateTime.UtcNow);
        issue.EscalationLevel.Should().Be(1);

        // Now check at 40 minutes: required window is 30 * (1 + 1) = 60 minutes
        var breachedAfterLevel1 = issue.CheckSlaEscalation(slaTimeout, DateTime.UtcNow, out reason);
        breachedAfterLevel1.Should().BeFalse();
        reason.Should().BeEmpty();

        // Check at 70 minutes: 70 > 60
        var breachedAt70m = issue.CheckSlaEscalation(slaTimeout, baseTime.AddMinutes(70), out reason);
        breachedAt70m.Should().BeTrue();
        reason.Should().Contain("Issue unacknowledged for 70 minutes");
    }

    [Fact]
    public void CheckSlaEscalation_WhenAcknowledgedOrCriticalOrNotOpen_ReturnsFalse()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var slaTimeout = TimeSpan.FromMinutes(30);

        var issue = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "SLA-02"),
            title: "Issue",
            detectorId: Guid.NewGuid(),
            detectorName: "SLA Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Critical,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: now.AddHours(-2));

        // When critical -> false
        issue.CheckSlaEscalation(slaTimeout, now, out _).Should().BeFalse();

        // When acknowledged -> false
        var issue2 = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "SLA-03"),
            title: "Issue 2",
            detectorId: Guid.NewGuid(),
            detectorName: "SLA Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Medium,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: now.AddHours(-2));
        issue2.Acknowledge("officer", now.AddHours(-1));
        issue2.CheckSlaEscalation(slaTimeout, now, out _).Should().BeFalse();
    }

    [Fact]
    public void CheckRecurrenceEscalation_WhenOccurrenceThresholdReached_ReturnsTrue()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var issue = Issue.Create(
            correlationKey: CorrelationKey.Create(Guid.NewGuid(), "RECUR-01"),
            title: "Recurring Issue",
            detectorId: Guid.NewGuid(),
            detectorName: "Recur Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.Medium,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: now.AddHours(-1));

        // 1 occurrence initially -> false
        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();

        // Add 3 more (total 4) -> false
        for (int i = 2; i <= 4; i++)
        {
            issue.RecordRecurringOccurrence(Guid.NewGuid(), CreateSampleEvidence(), now);
        }
        issue.OccurrenceCount.Should().Be(4);
        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();

        // Add 5th occurrence -> true
        issue.RecordRecurringOccurrence(Guid.NewGuid(), CreateSampleEvidence(), now);
        issue.OccurrenceCount.Should().Be(5);
        var breached = issue.CheckRecurrenceEscalation(now, out var reason);
        breached.Should().BeTrue();
        reason.Should().Be("Issue recurred 5 times across monitoring cycles.");

        // When Critical -> false
        issue.EscalateSeverity("step to high", now);
        issue.EscalateSeverity("step to critical", now);
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();
    }
}
