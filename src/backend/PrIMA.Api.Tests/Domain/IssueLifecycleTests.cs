using FluentAssertions;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Tests.Domain;

public class IssueLifecycleTests
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

    private static Issue CreateOpenIssue()
    {
        var detectorId = Guid.NewGuid();
        var key = CorrelationKey.Create(detectorId, "CORR-" + Guid.NewGuid().ToString("N"));
        var now = DateTime.UtcNow;
        return Issue.Create(
            correlationKey: key,
            title: "Test Issue",
            detectorId: detectorId,
            detectorName: "Test Detector",
            scope: MonitoringScope.DataIntegrity,
            baseSeverity: IssueSeverity.High,
            inspectionRunId: Guid.NewGuid(),
            evidence: CreateSampleEvidence(),
            timestampUtc: now);
    }

    private static Issue CreateReopenedIssue()
    {
        var issue = CreateOpenIssue();
        issue.Resolve(DateTime.UtcNow.AddHours(-1));
        issue.RecordRecurringOccurrence(Guid.NewGuid(), CreateSampleEvidence("REC-RECUR"), DateTime.UtcNow);
        return issue;
    }

    private static Issue CreateInvestigatingIssue(string officer = "ahmad.budi")
    {
        var issue = CreateOpenIssue();
        var result = issue.TakeIt(officer, DateTime.UtcNow);
        result.IsSuccess.Should().BeTrue();
        return issue;
    }

    // ==========================================
    // 1. TakeIt Tests
    // ==========================================

    [Fact]
    public void TakeIt_WhenOpen_TransitionsToInvestigating_AssignsCaller_AndSetsAcknowledgedAtUtc()
    {
        // Arrange
        var issue = CreateOpenIssue();
        var now = DateTime.UtcNow;

        // Act
        var result = issue.TakeIt("ahmad.budi", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("ahmad.budi");
        issue.AcknowledgedAtUtc.Should().Be(now);
    }

    [Fact]
    public void TakeIt_WhenReopened_TransitionsToInvestigating_AssignsCaller_AndSetsAcknowledgedAtUtc()
    {
        // Arrange
        var issue = CreateReopenedIssue();
        issue.Status.Should().Be(IssueStatus.Reopened);
        issue.AcknowledgedAtUtc.Should().BeNull();
        var now = DateTime.UtcNow;

        // Act
        var result = issue.TakeIt("siti.rahma", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("siti.rahma");
        issue.AcknowledgedAtUtc.Should().Be(now);
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void TakeIt_WhenIssueClosed_FailsWithInvalidStateTransition(IssueStatus closedStatus)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (closedStatus == IssueStatus.Resolved)
            issue.Resolve(DateTime.UtcNow);
        else
            issue.Dismiss(DateTime.UtcNow);

        // Act
        var result = issue.TakeIt("ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
    }

    // ==========================================
    // 2. Assign Tests
    // ==========================================

    [Fact]
    public void Assign_WhenOpen_TransitionsToAcknowledged_SetsAssignee_AndSetsAcknowledgedAtUtc()
    {
        // Arrange
        var issue = CreateOpenIssue();
        var now = DateTime.UtcNow;

        // Act
        var result = issue.Assign("siti.rahma", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Acknowledged);
        issue.CurrentAssignee.Should().Be("siti.rahma");
        issue.AcknowledgedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Assign_WhenReopened_TransitionsToAcknowledged_SetsAssignee_AndSetsAcknowledgedAtUtc()
    {
        // Arrange
        var issue = CreateReopenedIssue();
        var now = DateTime.UtcNow;

        // Act
        var result = issue.Assign("edp.lead", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Acknowledged);
        issue.CurrentAssignee.Should().Be("edp.lead");
        issue.AcknowledgedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Assign_WhenAlreadyInvestigating_PreservesInvestigatingStatus_OQ007()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var initialAck = issue.AcknowledgedAtUtc;
        var assignTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        var result = issue.Assign("siti.rahma", assignTime);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("siti.rahma");
        issue.AcknowledgedAtUtc.Should().Be(initialAck);
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void Assign_WhenClosed_FailsWithInvalidStateTransition(IssueStatus closedStatus)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (closedStatus == IssueStatus.Resolved)
            issue.Resolve(DateTime.UtcNow);
        else
            issue.Dismiss(DateTime.UtcNow);

        // Act
        var result = issue.Assign("siti.rahma", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
    }

    // ==========================================
    // 3. StartInvestigation Tests
    // ==========================================

    [Fact]
    public void StartInvestigation_WhenAcknowledged_TransitionsToInvestigating_AndAssignsOfficer()
    {
        // Arrange
        var issue = CreateOpenIssue();
        issue.Assign("ahmad.budi", DateTime.UtcNow);
        issue.Status.Should().Be(IssueStatus.Acknowledged);
        var now = DateTime.UtcNow.AddMinutes(1);

        // Act
        var result = issue.StartInvestigation("ahmad.budi", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("ahmad.budi");
    }

    [Fact]
    public void StartInvestigation_WhenOpen_FailsWithInvalidStateTransition()
    {
        // Arrange
        var issue = CreateOpenIssue();
        issue.Status.Should().Be(IssueStatus.Open);

        // Act
        var result = issue.StartInvestigation("ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
        issue.Status.Should().Be(IssueStatus.Open);
    }

    [Fact]
    public void StartInvestigation_WhenReopened_FailsWithInvalidStateTransition()
    {
        // Arrange
        var issue = CreateReopenedIssue();
        issue.Status.Should().Be(IssueStatus.Reopened);

        // Act
        var result = issue.StartInvestigation("ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
    }

    // ==========================================
    // 4. AddNote Tests
    // ==========================================

    [Theory]
    [InlineData(IssueStatus.Open)]
    [InlineData(IssueStatus.Acknowledged)]
    [InlineData(IssueStatus.Investigating)]
    [InlineData(IssueStatus.Reopened)]
    public void AddNote_WhenActive_AppendsNoteToCollection(IssueStatus activeStatus)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (activeStatus == IssueStatus.Acknowledged)
            issue.Assign("ahmad.budi", DateTime.UtcNow);
        else if (activeStatus == IssueStatus.Investigating)
            issue.TakeIt("ahmad.budi", DateTime.UtcNow);
        else if (activeStatus == IssueStatus.Reopened)
            issue = CreateReopenedIssue();

        var now = DateTime.UtcNow;

        // Act
        var result = issue.AddNote("Checked patient billing entries. Discrepancy verified.", "ahmad.budi", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Notes.Should().HaveCount(1);
        var note = issue.Notes[0];
        note.IssueId.Should().Be(issue.Id);
        note.OfficerName.Should().Be("ahmad.budi");
        note.NoteText.Should().Be("Checked patient billing entries. Discrepancy verified.");
        note.CreatedAtUtc.Should().Be(now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void AddNote_WhenNoteTextEmptyOrWhitespace_Fails(string emptyNote)
    {
        // Arrange
        var issue = CreateInvestigatingIssue();

        // Act
        var result = issue.AddNote(emptyNote, "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.EmptyNoteText");
        issue.Notes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void AddNote_WhenClosed_FailsWithActionRequiresActiveStatus(IssueStatus closedStatus)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (closedStatus == IssueStatus.Resolved)
            issue.Resolve(DateTime.UtcNow);
        else
            issue.Dismiss(DateTime.UtcNow);

        // Act
        var result = issue.AddNote("Valid note on closed issue", "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.ActionRequiresActiveStatus");
        issue.Notes.Should().BeEmpty();
    }

    // ==========================================
    // 5. RecordRemediation Tests
    // ==========================================

    [Fact]
    public void RecordRemediation_WhenInvestigating_RecordsRemediationAndClearsPriorVerification()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var now = DateTime.UtcNow;

        // Simulate an earlier remediation and failed verification
        issue.RecordRemediation("DataCorrection", "First attempt", "ahmad.budi", now.AddMinutes(-10));
        issue.RecordVerification("ManualQuery", false, "Still mismatched", "ahmad.budi", now.AddMinutes(-5));
        issue.LatestVerification.Should().NotBeNull();

        // Act - Retry remediation
        var result = issue.RecordRemediation("ConfigurationAdjustment", "Applied fix script SOP-BIL-004", "ahmad.budi", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.LatestRemediation.Should().NotBeNull();
        issue.LatestRemediation!.ActionCategory.Should().Be("ConfigurationAdjustment");
        issue.LatestRemediation.SummaryDescription.Should().Be("Applied fix script SOP-BIL-004");
        issue.LatestRemediation.PerformedBy.Should().Be("ahmad.budi");
        issue.LatestRemediation.TimestampUtc.Should().Be(now);
        // GAP-005 & OQ-005: Supersedes prior verification
        issue.LatestVerification.Should().BeNull();
    }

    [Theory]
    [InlineData(IssueStatus.Open)]
    [InlineData(IssueStatus.Acknowledged)]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void RecordRemediation_WhenNotInvestigating_FailsWithActionRequiresStatus(IssueStatus status)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (status == IssueStatus.Acknowledged)
            issue.Assign("ahmad.budi", DateTime.UtcNow);
        else if (status == IssueStatus.Resolved)
            issue.Resolve(DateTime.UtcNow);
        else if (status == IssueStatus.Dismissed)
            issue.Dismiss(DateTime.UtcNow);

        // Act
        var result = issue.RecordRemediation("DataCorrection", "Correction SOP", "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.ActionRequiresStatus");
        issue.LatestRemediation.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void RecordRemediation_WhenDescriptionEmptyOrWhitespace_Fails(string emptyDescription)
    {
        // Arrange
        var issue = CreateInvestigatingIssue();

        // Act
        var result = issue.RecordRemediation("DataCorrection", emptyDescription, "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.EmptyRemediationDescription");
        issue.LatestRemediation.Should().BeNull();
    }

    // ==========================================
    // 6. RecordVerification Tests
    // ==========================================

    [Fact]
    public void RecordVerification_WhenInvestigatingWithRemediation_RecordsVerificationSuccessfully()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var now = DateTime.UtcNow;
        issue.RecordRemediation("DataCorrection", "Fixed journal rows", "ahmad.budi", now.AddMinutes(-5));

        // Act
        var result = issue.RecordVerification("ManualQuery", true, "SELECT count(*) returned 0 variances", "siti.rahma", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.LatestVerification.Should().NotBeNull();
        issue.LatestVerification!.VerificationMethod.Should().Be("ManualQuery");
        issue.LatestVerification.CleanPassed.Should().BeTrue();
        issue.LatestVerification.EvidenceNotes.Should().Be("SELECT count(*) returned 0 variances");
        issue.LatestVerification.VerifiedBy.Should().Be("siti.rahma");
        issue.LatestVerification.TimestampUtc.Should().Be(now);
        issue.LatestVerification.IsSuperseded.Should().BeFalse();
    }

    [Fact]
    public void RecordVerification_WhenNoRemediationRecorded_FailsWithRemediationRequiredBeforeVerification()
    {
        // Arrange
        var issue = CreateInvestigatingIssue();
        issue.LatestRemediation.Should().BeNull();

        // Act
        var result = issue.RecordVerification("ManualQuery", true, "Query evidence", "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.RemediationRequiredBeforeVerification");
        issue.LatestVerification.Should().BeNull();
    }

    [Theory]
    [InlineData(IssueStatus.Open)]
    [InlineData(IssueStatus.Acknowledged)]
    public void RecordVerification_WhenNotInvestigating_FailsWithActionRequiresStatus(IssueStatus status)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (status == IssueStatus.Acknowledged)
            issue.Assign("ahmad.budi", DateTime.UtcNow);

        // Act
        var result = issue.RecordVerification("ManualQuery", true, "Evidence", "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.ActionRequiresStatus");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void RecordVerification_WhenEvidenceNotesEmptyOrWhitespace_Fails(string emptyEvidence)
    {
        // Arrange
        var issue = CreateInvestigatingIssue();
        issue.RecordRemediation("DataCorrection", "Fixed", "ahmad.budi", DateTime.UtcNow.AddMinutes(-1));

        // Act
        var result = issue.RecordVerification("ManualQuery", true, emptyEvidence, "ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.EmptyVerificationEvidence");
    }

    // ==========================================
    // 7. MarkResolved Verification Gate (BR-PRIMA-004) Tests
    // ==========================================

    [Fact]
    public void MarkResolved_WhenCleanPassedAndNotSuperseded_TransitionsToResolved_AndSetsResolvedAtUtc()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var now = DateTime.UtcNow;
        issue.RecordRemediation("DataCorrection", "Applied fix SOP", "ahmad.budi", now.AddMinutes(-5));
        issue.RecordVerification("ManualQuery", true, "Query returned clean 0 discrepancies", "siti.rahma", now.AddMinutes(-2));

        // Act
        var result = issue.MarkResolved("siti.rahma", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Resolved);
        issue.ResolvedAtUtc.Should().Be(now);
        issue.IsClosed.Should().BeTrue();
        issue.IsActive.Should().BeFalse();
    }

    [Fact]
    public void MarkResolved_WhenNoVerificationExists_FailsVerificationGate()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        issue.RecordRemediation("DataCorrection", "Fixed without verify", "ahmad.budi", DateTime.UtcNow);
        issue.LatestVerification.Should().BeNull();

        // Act
        var result = issue.MarkResolved("ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.VerificationGateFailed");
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.ResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkResolved_WhenVerificationFailedCleanPassedIsFalse_FailsVerificationGate()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var now = DateTime.UtcNow;
        issue.RecordRemediation("DataCorrection", "Fix attempt 1", "ahmad.budi", now.AddMinutes(-5));
        issue.RecordVerification("ManualQuery", false, "Still found 2 invalid records", "ahmad.budi", now.AddMinutes(-2));

        // Act
        var result = issue.MarkResolved("ahmad.budi", now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.VerificationGateFailed");
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.ResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkResolved_WhenVerificationIsSuperseded_FailsVerificationGate()
    {
        // Arrange
        var issue = CreateInvestigatingIssue("ahmad.budi");
        var now = DateTime.UtcNow;
        issue.RecordRemediation("DataCorrection", "Fix SOP", "ahmad.budi", now.AddMinutes(-10));
        issue.RecordVerification("ManualQuery", true, "Clean verified", "ahmad.budi", now.AddMinutes(-8));

        // Recurrence occurs while investigating, invalidating active verification
        issue.InvalidateVerification();
        issue.LatestVerification!.IsSuperseded.Should().BeTrue();

        // Act
        var result = issue.MarkResolved("ahmad.budi", now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.VerificationGateFailed");
        issue.Status.Should().Be(IssueStatus.Investigating);
        issue.ResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkResolved_WhenStatusIsNotInvestigating_FailsWithInvalidStateTransition()
    {
        // Arrange
        var issue = CreateOpenIssue();

        // Act
        var result = issue.MarkResolved("ahmad.budi", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
    }

    // ==========================================
    // 8. Dismiss Justification (BR-PRIMA-006) Tests
    // ==========================================

    [Theory]
    [InlineData("Valid justification rationale for false positive observation.")] // 63 chars
    [InlineData("12345678901234567890")] // exactly 20 chars
    public void Dismiss_WhenRationaleAtLeast20Characters_TransitionsToDismissed_AndCreatesDismissalRecord(string validRationale)
    {
        // Arrange
        var issue = CreateOpenIssue();
        var now = DateTime.UtcNow;

        // Act
        var result = issue.Dismiss("FalsePositive", validRationale, "siti.rahma", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatus.Dismissed);
        issue.DismissedAtUtc.Should().Be(now);
        issue.IsClosed.Should().BeTrue();
        issue.IsActive.Should().BeFalse();

        issue.Dismissal.Should().NotBeNull();
        issue.Dismissal!.DismissalReason.Should().Be("FalsePositive");
        issue.Dismissal.JustificationRationale.Should().Be(validRationale);
        issue.Dismissal.DismissedBy.Should().Be("siti.rahma");
        issue.Dismissal.TimestampUtc.Should().Be(now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Too short")] // 9 chars
    [InlineData("1234567890123456789")] // 19 chars
    public void Dismiss_WhenRationaleUnder20Characters_FailsWithDismissalRationaleTooShort(string invalidRationale)
    {
        // Arrange
        var issue = CreateOpenIssue();

        // Act
        var result = issue.Dismiss("FalsePositive", invalidRationale, "siti.rahma", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.DismissalRationaleTooShort");
        issue.Status.Should().Be(IssueStatus.Open);
        issue.DismissedAtUtc.Should().BeNull();
        issue.Dismissal.Should().BeNull();
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Dismissed)]
    public void Dismiss_WhenAlreadyClosed_FailsWithInvalidStateTransition(IssueStatus closedStatus)
    {
        // Arrange
        var issue = CreateOpenIssue();
        if (closedStatus == IssueStatus.Resolved)
            issue.Resolve(DateTime.UtcNow);
        else
            issue.Dismiss(DateTime.UtcNow);

        // Act
        var result = issue.Dismiss("FalsePositive", "Valid justification rationale that is long enough.", "siti.rahma", DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Issues.InvalidStateTransition");
    }
}
