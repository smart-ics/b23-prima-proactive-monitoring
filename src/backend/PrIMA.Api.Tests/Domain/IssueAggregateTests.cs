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
}
