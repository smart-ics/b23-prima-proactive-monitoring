using FluentAssertions;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Tests.Domain;

public class IssueEscalationTests
{
    private static TelemetryEvidence CreateSampleEvidence()
    {
        return new TelemetryEvidence(
            module: "Settlement",
            sourceTable: "dbo.SettlementBatches",
            recordKeys: "BATCH-4401",
            debitTotal: 5000000m,
            creditTotal: 4950000m,
            varianceDelta: 50000m,
            trace: "Batch reconciliation imbalance detected.");
    }

    private static Issue CreateSampleIssue(
        IssueSeverity severity = IssueSeverity.Low,
        DateTime? createdAtUtc = null)
    {
        var detectorId = Guid.NewGuid();
        return Issue.Create(
            correlationKey: CorrelationKey.Create(detectorId, "SETTLE-TEST-001"),
            title: "Settlement Imbalance",
            detectorId: detectorId,
            detectorName: "Settlement Balance Checker",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: severity,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    [Fact]
    public void EscalateSeverity_StepsUpExactlyOneLevelUntilCappedAtCritical()
    {
        // Arrange
        var issue = CreateSampleIssue(IssueSeverity.Low);
        issue.CurrentSeverity.Should().Be(IssueSeverity.Low);
        issue.SeverityRank.Should().Be(SeverityRank.Low);
        issue.EscalationLevel.Should().Be(0);

        // Act & Assert 1: Low -> Medium
        var step1 = issue.EscalateSeverity("Initial SLA breach", DateTime.UtcNow);
        step1.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Medium);
        issue.SeverityRank.Should().Be(SeverityRank.Medium);
        issue.EscalationLevel.Should().Be(1);

        // Act & Assert 2: Medium -> High
        var step2 = issue.EscalateSeverity("Second SLA breach", DateTime.UtcNow);
        step2.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.High);
        issue.SeverityRank.Should().Be(SeverityRank.High);
        issue.EscalationLevel.Should().Be(2);

        // Act & Assert 3: High -> Critical
        var step3 = issue.EscalateSeverity("Third SLA breach", DateTime.UtcNow);
        step3.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.SeverityRank.Should().Be(SeverityRank.Critical);
        issue.EscalationLevel.Should().Be(3);

        // Act & Assert 4: Already Critical -> Capped at Critical, remains escalation level 3
        var step4 = issue.EscalateSeverity("Fourth SLA breach", DateTime.UtcNow);
        step4.IsSuccess.Should().BeTrue();
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);
        issue.SeverityRank.Should().Be(SeverityRank.Critical);
        issue.EscalationLevel.Should().Be(3);
    }

    [Fact]
    public void EscalateSeverity_WhenIssueClosed_FailsPrecondition()
    {
        // Arrange
        var issue = CreateSampleIssue(IssueSeverity.Medium);
        issue.Resolve(DateTime.UtcNow);
        issue.IsClosed.Should().BeTrue();

        // Act
        var result = issue.EscalateSeverity("Late SLA check", DateTime.UtcNow);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(IssueErrors.ActionRequiresActiveStatus().Code);
    }

    [Fact]
    public void CheckSlaEscalation_RequiresUnacknowledgedOpenStatusAndWindowScaling()
    {
        var now = DateTime.UtcNow;
        var slaTimeout = TimeSpan.FromMinutes(30);

        // Created 45 minutes ago (45m > 30m * (0 + 1))
        var issue = CreateSampleIssue(IssueSeverity.Low, now.AddMinutes(-45));
        var breached = issue.CheckSlaEscalation(slaTimeout, now, out var reason);

        breached.Should().BeTrue();
        reason.Should().Contain("Issue unacknowledged for 45 minutes");
        reason.Should().Contain("SLA window: 30m");

        // Escalate to level 1: Required window becomes 30m * (1 + 1) = 60m
        issue.EscalateSeverity(reason, now);
        issue.EscalationLevel.Should().Be(1);

        // At 45m elapsed, 45m <= 60m -> no breach
        issue.CheckSlaEscalation(slaTimeout, now, out _).Should().BeFalse();

        // At 65m elapsed, 65m > 60m -> breach
        var breachedLevel1 = issue.CheckSlaEscalation(slaTimeout, issue.CreatedAtUtc.AddMinutes(65), out var reasonLevel1);
        breachedLevel1.Should().BeTrue();
        reasonLevel1.Should().Contain("Issue unacknowledged for 65 minutes");

        // If acknowledged, SLA escalation is disabled
        issue.Acknowledge("operator.one", now);
        issue.CheckSlaEscalation(slaTimeout, issue.CreatedAtUtc.AddMinutes(120), out _).Should().BeFalse();
    }

    [Fact]
    public void CheckRecurrenceEscalation_TriggersAtThresholdOfFiveOccurrencesUnlessCritical()
    {
        var now = DateTime.UtcNow;
        var issue = CreateSampleIssue(IssueSeverity.Medium, now.AddHours(-2));

        // Initial occurrence count = 1
        issue.OccurrenceCount.Should().Be(1);
        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();

        // Add occurrences 2, 3, 4
        for (int i = 2; i <= 4; i++)
        {
            issue.RecordRecurringOccurrence(Guid.NewGuid(), CreateSampleEvidence(), now);
        }
        issue.OccurrenceCount.Should().Be(4);
        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();

        // 5th occurrence triggers recurrence escalation
        issue.RecordRecurringOccurrence(Guid.NewGuid(), CreateSampleEvidence(), now);
        issue.OccurrenceCount.Should().Be(5);

        var breached = issue.CheckRecurrenceEscalation(now, out var reason);
        breached.Should().BeTrue();
        reason.Should().Be("Issue recurred 5 times across monitoring cycles.");

        // If already Critical, recurrence escalation does not trigger
        issue.EscalateSeverity("Step up to High", now);
        issue.EscalateSeverity("Step up to Critical", now);
        issue.CurrentSeverity.Should().Be(IssueSeverity.Critical);

        issue.CheckRecurrenceEscalation(now, out _).Should().BeFalse();
    }
}
