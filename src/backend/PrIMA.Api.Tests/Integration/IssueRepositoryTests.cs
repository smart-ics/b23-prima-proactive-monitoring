using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Infrastructure.Persistence;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class IssueRepositoryTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly IIssueRepository _repository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public IssueRepositoryTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        var scope = factory.Services.CreateScope();
        _repository = scope.ServiceProvider.GetRequiredService<IIssueRepository>();
        _sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid IssueId, Guid DetectorId)> CreateTestIssueAsync(IssueStatus initialStatus = IssueStatus.Open)
    {
        var detectorId = await _factory.SeedDetectorAsync();
        var issueId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        const string runSql = """
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
            VALUES (@Id, @DetectorId, @Now, 'ScheduledTimer', 'FailureWithAnomalies', 120, @Now);
            """;
        await conn.ExecuteAsync(runSql, new { Id = runId, DetectorId = detectorId, Now = now });

        var issue = Issue.Create(
            CorrelationKey.Create(detectorId, "INV-1001"),
            "Unbalanced Invoice INV-1001",
            detectorId,
            "Test Billing Detector",
            MonitoringScope.DataIntegrity,
            IssueSeverity.High,
            runId,
            new TelemetryEvidence("Billing", "Invoices", "INV-1001", 1000m, 500m, 500m, "Trace error"),
            now);

        await _repository.InsertIssueAsync(issue, conn);

        if (initialStatus != IssueStatus.Open)
        {
            await conn.ExecuteAsync("UPDATE dbo.Issues SET Status = @Status WHERE Id = @Id;", new { Status = initialStatus.ToString(), Id = issue.Id });
        }

        return (issue.Id, detectorId);
    }

    [Fact]
    public async Task TakeItAsync_TransitionsToInvestigating_SetsAssigneeAndAcknowledgedTime()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Open);
        var timestamp = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.TakeItAsync(issueId, "Ahmad Budi", timestamp, conn, tx);
        tx.Commit();

        var issue = await _repository.GetByIdAsync(issueId);
        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("Ahmad Budi");
        issue.AcknowledgedAtUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AssignAsync_FromOpen_TransitionsToAcknowledged()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Open);
        var timestamp = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.AssignAsync(issueId, "Siti Rahma", IssueStatus.Acknowledged, timestamp, conn, tx);
        tx.Commit();

        var issue = await _repository.GetByIdAsync(issueId);
        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Acknowledged);
        issue.CurrentAssignee.Should().Be("Siti Rahma");
        issue.AcknowledgedAtUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AcknowledgeAsync_TransitionsToAcknowledged_SetsAssigneeIfNull()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Open);
        var timestamp = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.AcknowledgeAsync(issueId, "Ahmad Budi", timestamp, conn, tx);
        tx.Commit();

        var issue = await _repository.GetByIdAsync(issueId);
        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Acknowledged);
        issue.CurrentAssignee.Should().Be("Ahmad Budi");
        issue.AcknowledgedAtUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StartInvestigationAsync_TransitionsAcknowledgedToInvestigating()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Acknowledged);
        var timestamp = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.StartInvestigationAsync(issueId, "Ahmad Budi", timestamp, conn, tx);
        tx.Commit();

        var issue = await _repository.GetByIdAsync(issueId);
        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Investigating);
        issue.CurrentAssignee.Should().Be("Ahmad Budi");
    }

    [Fact]
    public async Task AddNoteAsync_PersistsNote()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Investigating);
        var note = new InvestigationNote(Guid.NewGuid(), issueId, "Ahmad Budi", "Investigating ledger discrepancy.", DateTime.UtcNow);

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.AddNoteAsync(note, conn, tx);
        tx.Commit();

        var hydrated = await _repository.GetIssueWithGovernanceByIdAsync(issueId);
        hydrated.Should().NotBeNull();
        hydrated!.Notes.Should().HaveCount(1);
        hydrated.Notes[0].NoteText.Should().Be("Investigating ledger discrepancy.");
        hydrated.Notes[0].OfficerName.Should().Be("Ahmad Budi");
    }

    [Fact]
    public async Task RecordRemediationAsync_And_SupersedeActiveVerificationsAsync_WorkCorrectly()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Investigating);
        var now = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        var remediation = new RemediationRecord(
            Guid.NewGuid(),
            issueId,
            RemediationActionCategory.DataCorrection.ToString(),
            "Manual journal adjustment posted.",
            "Ahmad Budi",
            now,
            isSuperseded: false);

        await _repository.RecordRemediationAsync(remediation, conn, tx);

        var verification = new VerificationRecord(
            Guid.NewGuid(),
            issueId,
            "ManualQueryConfirmed",
            cleanPassed: false,
            evidenceNotes: "Variance still exists.",
            verifiedBy: "Ahmad Budi",
            timestampUtc: now,
            isSuperseded: false);

        await _repository.RecordVerificationAsync(verification, conn, tx);
        tx.Commit();

        var beforeSupersede = await _repository.GetIssueWithGovernanceByIdAsync(issueId);
        beforeSupersede!.LatestVerification.Should().NotBeNull();
        beforeSupersede.LatestVerification!.CleanPassed.Should().BeFalse();

        // Supersede active verifications
        using var tx2 = conn.BeginTransaction();
        await _repository.SupersedeActiveVerificationsAsync(issueId, conn, tx2);
        tx2.Commit();

        var afterSupersede = await _repository.GetIssueWithGovernanceByIdAsync(issueId);
        afterSupersede!.LatestVerification.Should().BeNull(); // No active (un-superseded) verification
        afterSupersede.LatestRemediation.Should().NotBeNull();
        afterSupersede.LatestRemediation!.ActionCategory.Should().Be("DataCorrection");
    }

    [Fact]
    public async Task ResolveIssueAsync_TransitionsToResolved()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Investigating);
        var resolvedAt = DateTime.UtcNow;

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.ResolveIssueAsync(issueId, resolvedAt, conn, tx);
        tx.Commit();

        var issue = await _repository.GetByIdAsync(issueId);
        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Resolved);
        issue.ResolvedAtUtc.Should().BeCloseTo(resolvedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DismissIssueAsync_TransitionsToDismissed_AndPersistsDismissalRecord()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Open);
        var dismissedAt = DateTime.UtcNow;

        var dismissal = new DismissalRecord(
            Guid.NewGuid(),
            issueId,
            DismissalReason.FalsePositive.ToString(),
            "Expected maintenance window variance confirmed by DBA lead.",
            "Ahmad Budi",
            dismissedAt,
            isSuperseded: false);

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.DismissIssueAsync(issueId, dismissal, conn, tx);
        tx.Commit();

        var hydrated = await _repository.GetIssueWithGovernanceByIdAsync(issueId);
        hydrated.Should().NotBeNull();
        hydrated!.Status.Should().Be(IssueStatus.Dismissed);
        hydrated.DismissedAtUtc.Should().BeCloseTo(dismissedAt, TimeSpan.FromSeconds(1));
        hydrated.Dismissal.Should().NotBeNull();
        hydrated.Dismissal!.DismissalReason.Should().Be("FalsePositive");
        hydrated.Dismissal.JustificationRationale.Should().Contain("maintenance window");
    }

    [Fact]
    public async Task InsertActivityLogAsync_PersistsLogCorrectly()
    {
        var (issueId, _) = await CreateTestIssueAsync(IssueStatus.Investigating);

        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertActivityLogAsync(
            conn,
            tx,
            issueId,
            "RemediationRecorded",
            "Ahmad Budi",
            "Remediation recorded: DataCorrection",
            "{\"category\":\"DataCorrection\"}");
        tx.Commit();

        const string logSql = "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId;";
        var logs = (await conn.QueryAsync(logSql, new { IssueId = issueId })).ToList();

        logs.Should().HaveCount(1);
        string actType = logs[0].ActivityType;
        string actor = logs[0].ActorName;
        string summary = logs[0].Summary;
        string details = logs[0].DetailsJson;

        actType.Should().Be("RemediationRecorded");
        actor.Should().Be("Ahmad Budi");
        summary.Should().Be("Remediation recorded: DataCorrection");
        details.Should().Contain("DataCorrection");
    }
}
