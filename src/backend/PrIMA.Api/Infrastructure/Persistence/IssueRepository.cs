using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Infrastructure.Persistence;

public sealed class IssueRepository : IIssueRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public IssueRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Task<Issue?> GetByIdAsync(Guid id, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Id = @Id;
            """;

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, async (connection, tx) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<IssueRow>(
                new CommandDefinition(sql, new { Id = id }, tx, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task<Issue?> FindByCorrelationKeyAsync(string correlationKey, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE CorrelationKey = @CorrelationKey;
            """;

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, async (connection, tx) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<IssueRow>(
                new CommandDefinition(sql, new { CorrelationKey = correlationKey.Trim() }, tx, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task<Issue?> FindByCorrelationKeyWithLockAsync(string correlationKey, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return FindByCorrelationKeyWithLockAsync(correlationKey, transaction.Connection!, transaction, cancellationToken);
    }

    public Task<Issue?> FindByCorrelationKeyWithLockAsync(string correlationKey, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues WITH (UPDLOCK, HOLDLOCK)
            WHERE CorrelationKey = @Key;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<IssueRow>(
                new CommandDefinition(sql, new { Key = correlationKey.Trim() }, transaction, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task InsertAsync(Issue issue, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return InsertIssueAsync(issue, transaction?.Connection, transaction, cancellationToken);
    }

    public Task InsertIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope,
                @BaseSeverity, @CurrentSeverity, @SeverityRank, @Status, @EscalationLevel,
                @OccurrenceCount, @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc,
                @AcknowledgedAtUtc, @ResolvedAtUtc, @DismissedAtUtc, @ReopenedAtUtc
            );
            """;

        var parameters = new
        {
            issue.Id,
            CorrelationKey = issue.CorrelationKey.Value,
            issue.Title,
            issue.DetectorId,
            issue.DetectorName,
            Scope = issue.Scope.ToString(),
            BaseSeverity = issue.BaseSeverity.ToString(),
            CurrentSeverity = issue.CurrentSeverity.ToString(),
            issue.SeverityRank,
            Status = issue.Status.ToString(),
            issue.EscalationLevel,
            issue.OccurrenceCount,
            issue.CurrentAssignee,
            issue.CreatedAtUtc,
            issue.LastOccurrenceAtUtc,
            issue.AcknowledgedAtUtc,
            issue.ResolvedAtUtc,
            issue.DismissedAtUtc,
            issue.ReopenedAtUtc
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task UpdateAsync(Issue issue, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        const string sql = """
            UPDATE dbo.Issues
            SET Title = @Title,
                DetectorName = @DetectorName,
                Scope = @Scope,
                BaseSeverity = @BaseSeverity,
                CurrentSeverity = @CurrentSeverity,
                SeverityRank = @SeverityRank,
                Status = @Status,
                EscalationLevel = @EscalationLevel,
                OccurrenceCount = @OccurrenceCount,
                CurrentAssignee = @CurrentAssignee,
                LastOccurrenceAtUtc = @LastOccurrenceAtUtc,
                AcknowledgedAtUtc = @AcknowledgedAtUtc,
                ResolvedAtUtc = @ResolvedAtUtc,
                DismissedAtUtc = @DismissedAtUtc,
                ReopenedAtUtc = @ReopenedAtUtc
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            issue.Id,
            issue.Title,
            issue.DetectorName,
            Scope = issue.Scope.ToString(),
            BaseSeverity = issue.BaseSeverity.ToString(),
            CurrentSeverity = issue.CurrentSeverity.ToString(),
            issue.SeverityRank,
            Status = issue.Status.ToString(),
            issue.EscalationLevel,
            issue.OccurrenceCount,
            issue.CurrentAssignee,
            issue.LastOccurrenceAtUtc,
            issue.AcknowledgedAtUtc,
            issue.ResolvedAtUtc,
            issue.DismissedAtUtc,
            issue.ReopenedAtUtc
        };

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, (connection, tx) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, tx, cancellationToken: cancellationToken)));
    }

    public Task UpdateRecurringIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        const string sql = """
            UPDATE dbo.Issues
            SET OccurrenceCount = @OccurrenceCount,
                LastOccurrenceAtUtc = @LastOccurrenceAtUtc
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            issue.Id,
            issue.OccurrenceCount,
            issue.LastOccurrenceAtUtc
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task ReopenIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Reopened',
                ReopenedAtUtc = @ReopenedAtUtc,
                OccurrenceCount = @OccurrenceCount,
                LastOccurrenceAtUtc = @LastOccurrenceAtUtc,
                CurrentAssignee = NULL,
                AcknowledgedAtUtc = NULL,
                ResolvedAtUtc = NULL,
                DismissedAtUtc = NULL
            WHERE Id = @Id;

            UPDATE dbo.RemediationRecords SET IsSuperseded = 1 WHERE IssueId = @Id;
            UPDATE dbo.VerificationRecords SET IsSuperseded = 1 WHERE IssueId = @Id;
            UPDATE dbo.DismissalRecords SET IsSuperseded = 1 WHERE IssueId = @Id;
            """;

        var parameters = new
        {
            issue.Id,
            ReopenedAtUtc = issue.ReopenedAtUtc ?? issue.LastOccurrenceAtUtc,
            issue.OccurrenceCount,
            issue.LastOccurrenceAtUtc
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task InsertOccurrenceAsync(Occurrence occurrence, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return InsertOccurrenceAsync(occurrence, transaction?.Connection, transaction, cancellationToken);
    }

    public Task InsertOccurrenceAsync(Occurrence occurrence, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        const string sql = """
            INSERT INTO dbo.Occurrences (
                Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc,
                Module, SourceTable, RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString
            ) VALUES (
                @Id, @IssueId, @InspectionRunId, @OccurrenceNumber, @TimestampUtc,
                @Module, @SourceTable, @RecordKeys, @DebitTotal, @CreditTotal, @VarianceDelta, @TraceString
            );
            """;

        var parameters = new
        {
            occurrence.Id,
            occurrence.IssueId,
            occurrence.InspectionRunId,
            occurrence.OccurrenceNumber,
            occurrence.TimestampUtc,
            occurrence.Module,
            occurrence.SourceTable,
            occurrence.RecordKeys,
            occurrence.DebitTotal,
            occurrence.CreditTotal,
            occurrence.VarianceDelta,
            occurrence.TraceString
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task InsertInspectionRunAsync(InspectionRun run, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        const string sql = """
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc
            ) VALUES (
                @Id, @DetectorId, @ExecutionTimestampUtc, @TriggerSource, @Outcome, @DurationMs, @FailureReason, @CreatedAtUtc
            );
            """;

        var parameters = new
        {
            run.Id,
            run.DetectorId,
            run.ExecutionTimestampUtc,
            TriggerSource = run.TriggerSource.ToString(),
            Outcome = run.Outcome.ToString(),
            run.DurationMs,
            run.FailureReason,
            run.CreatedAtUtc
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task SupersedePriorGovernanceRecordsAsync(Guid issueId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.RemediationRecords SET IsSuperseded = 1 WHERE IssueId = @IssueId;
            UPDATE dbo.VerificationRecords SET IsSuperseded = 1 WHERE IssueId = @IssueId;
            UPDATE dbo.DismissalRecords SET IsSuperseded = 1 WHERE IssueId = @IssueId;
            """;

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, (connection, tx) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new { IssueId = issueId }, tx, cancellationToken: cancellationToken)));
    }

    public Task<IReadOnlyList<Occurrence>> GetOccurrencesByIssueIdAsync(Guid issueId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc,
                Module, SourceTable, RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString
            FROM dbo.Occurrences
            WHERE IssueId = @IssueId
            ORDER BY OccurrenceNumber ASC;
            """;

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, async (connection, tx) =>
        {
            var rows = await connection.QueryAsync<OccurrenceRow>(
                new CommandDefinition(sql, new { IssueId = issueId }, tx, cancellationToken: cancellationToken));

            return (IReadOnlyList<Occurrence>)rows.Select(MapToDomain).ToList();
        });
    }

    public Task TakeItAsync(
        Guid issueId,
        string officerName,
        DateTime timestampUtc,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officerName);

        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Investigating',
                CurrentAssignee = @OfficerName,
                AcknowledgedAtUtc = COALESCE(AcknowledgedAtUtc, @TimestampUtc)
            WHERE Id = @IssueId;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                OfficerName = officerName.Trim(),
                TimestampUtc = timestampUtc
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task AssignAsync(
        Guid issueId,
        string officerName,
        IssueStatus newStatus,
        DateTime timestampUtc,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officerName);

        const string sql = """
            UPDATE dbo.Issues
            SET CurrentAssignee = @OfficerName,
                Status = @Status,
                AcknowledgedAtUtc = CASE
                    WHEN @Status = 'Acknowledged' AND AcknowledgedAtUtc IS NULL THEN @TimestampUtc
                    ELSE AcknowledgedAtUtc
                END
            WHERE Id = @IssueId;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                OfficerName = officerName.Trim(),
                Status = newStatus.ToString(),
                TimestampUtc = timestampUtc
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task AcknowledgeAsync(
        Guid issueId,
        string officerName,
        DateTime timestampUtc,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officerName);

        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Acknowledged',
                CurrentAssignee = COALESCE(CurrentAssignee, @OfficerName),
                AcknowledgedAtUtc = @TimestampUtc
            WHERE Id = @IssueId;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                OfficerName = officerName.Trim(),
                TimestampUtc = timestampUtc
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task StartInvestigationAsync(
        Guid issueId,
        string officerName,
        DateTime timestampUtc,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officerName);

        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Investigating',
                CurrentAssignee = @OfficerName
            WHERE Id = @IssueId;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                OfficerName = officerName.Trim()
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task AddNoteAsync(
        InvestigationNote note,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        const string sql = """
            INSERT INTO dbo.InvestigationNotes (
                Id, IssueId, OfficerName, NoteText, CreatedAtUtc
            ) VALUES (
                @Id, @IssueId, @OfficerName, @NoteText, @CreatedAtUtc
            );
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                note.Id,
                note.IssueId,
                note.OfficerName,
                note.NoteText,
                note.CreatedAtUtc
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task RecordRemediationAsync(
        RemediationRecord remediation,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remediation);

        const string sql = """
            INSERT INTO dbo.RemediationRecords (
                Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                @Id, @IssueId, @ActionCategory, @SummaryDescription, @PerformedBy, @TimestampUtc, @IsSuperseded
            );
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                remediation.Id,
                remediation.IssueId,
                remediation.ActionCategory,
                remediation.SummaryDescription,
                remediation.PerformedBy,
                remediation.TimestampUtc,
                remediation.IsSuperseded
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task RecordVerificationAsync(
        VerificationRecord verification,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verification);

        const string sql = """
            INSERT INTO dbo.VerificationRecords (
                Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                @Id, @IssueId, @VerificationMethod, @CleanPassed, @EvidenceNotes, @VerifiedBy, @TimestampUtc, @IsSuperseded
            );
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                verification.Id,
                verification.IssueId,
                verification.VerificationMethod,
                verification.CleanPassed,
                verification.EvidenceNotes,
                verification.VerifiedBy,
                verification.TimestampUtc,
                verification.IsSuperseded
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task ResolveIssueAsync(
        Guid issueId,
        DateTime resolvedAtUtc,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Resolved',
                ResolvedAtUtc = @ResolvedAtUtc
            WHERE Id = @IssueId;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                ResolvedAtUtc = resolvedAtUtc
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task DismissIssueAsync(
        Guid issueId,
        DismissalRecord dismissal,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dismissal);

        const string sql = """
            UPDATE dbo.Issues
            SET Status = 'Dismissed',
                DismissedAtUtc = @TimestampUtc
            WHERE Id = @IssueId;

            INSERT INTO dbo.DismissalRecords (
                Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                @Id, @IssueId, @DismissalReason, @JustificationRationale, @DismissedBy, @TimestampUtc, @IsSuperseded
            );
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                IssueId = issueId,
                TimestampUtc = dismissal.TimestampUtc,
                Id = dismissal.Id == Guid.Empty ? Guid.NewGuid() : dismissal.Id,
                dismissal.DismissalReason,
                dismissal.JustificationRationale,
                dismissal.DismissedBy,
                dismissal.IsSuperseded
            }, transaction, cancellationToken: cancellationToken)));
    }

    public Task SupersedeActiveVerificationsAsync(
        Guid issueId,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.VerificationRecords
            SET IsSuperseded = 1
            WHERE IssueId = @IssueId AND IsSuperseded = 0;
            """;

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, new { IssueId = issueId }, transaction, cancellationToken: cancellationToken)));
    }

    public Task<Issue?> GetIssueWithGovernanceByIdAsync(
        Guid issueId,
        IDbConnection? conn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Id = @IssueId;

            SELECT TOP 1
                Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
            FROM dbo.RemediationRecords
            WHERE IssueId = @IssueId AND IsSuperseded = 0
            ORDER BY TimestampUtc DESC;

            SELECT TOP 1
                Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
            FROM dbo.VerificationRecords
            WHERE IssueId = @IssueId AND IsSuperseded = 0
            ORDER BY TimestampUtc DESC;

            SELECT TOP 1
                Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded
            FROM dbo.DismissalRecords
            WHERE IssueId = @IssueId AND IsSuperseded = 0
            ORDER BY TimestampUtc DESC;

            SELECT
                Id, IssueId, OfficerName, NoteText, CreatedAtUtc
            FROM dbo.InvestigationNotes
            WHERE IssueId = @IssueId
            ORDER BY CreatedAtUtc ASC;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            using var multi = await connection.QueryMultipleAsync(
                new CommandDefinition(sql, new { IssueId = issueId }, transaction, cancellationToken: cancellationToken));

            var issueRow = await multi.ReadSingleOrDefaultAsync<IssueRow>();
            if (issueRow is null)
            {
                return null;
            }

            var issue = MapToDomain(issueRow);

            var remediationRow = await multi.ReadSingleOrDefaultAsync<RemediationRow>();
            if (remediationRow is not null)
            {
                issue.LoadRemediation(new RemediationRecord(
                    remediationRow.Id,
                    remediationRow.IssueId,
                    remediationRow.ActionCategory,
                    remediationRow.SummaryDescription,
                    remediationRow.PerformedBy,
                    remediationRow.TimestampUtc,
                    remediationRow.IsSuperseded));
            }

            var verificationRow = await multi.ReadSingleOrDefaultAsync<VerificationRow>();
            if (verificationRow is not null)
            {
                issue.LoadVerification(new VerificationRecord(
                    verificationRow.Id,
                    verificationRow.IssueId,
                    verificationRow.VerificationMethod,
                    verificationRow.CleanPassed,
                    verificationRow.EvidenceNotes,
                    verificationRow.VerifiedBy,
                    verificationRow.TimestampUtc,
                    verificationRow.IsSuperseded));
            }

            var dismissalRow = await multi.ReadSingleOrDefaultAsync<DismissalRow>();
            if (dismissalRow is not null)
            {
                issue.LoadDismissal(new DismissalRecord(
                    dismissalRow.Id,
                    dismissalRow.IssueId,
                    dismissalRow.DismissalReason,
                    dismissalRow.JustificationRationale,
                    dismissalRow.DismissedBy,
                    dismissalRow.TimestampUtc,
                    dismissalRow.IsSuperseded));
            }

            var noteRows = await multi.ReadAsync<NoteRow>();
            foreach (var noteRow in noteRows)
            {
                issue.LoadNote(new InvestigationNote(
                    noteRow.Id,
                    noteRow.IssueId,
                    noteRow.OfficerName,
                    noteRow.NoteText,
                    noteRow.CreatedAtUtc));
            }

            return issue;
        });
    }

    public Task InsertActivityLogAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid issueId,
        string activityType,
        string actorName,
        string summary,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        return InsertActivityLogInternalAsync(conn, tx, issueId, activityType, actorName, summary, detailsJson, cancellationToken);
    }

    public static Task InsertActivityLogAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid issueId,
        string activityType,
        string actorName,
        string summary,
        string? detailsJson = null)
    {
        return InsertActivityLogInternalAsync(conn, tx, issueId, activityType, actorName, summary, detailsJson, CancellationToken.None);
    }

    private static Task InsertActivityLogInternalAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid issueId,
        string activityType,
        string actorName,
        string summary,
        string? detailsJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        const string sql = """
            INSERT INTO dbo.IssueActivityLogs (
                Id, IssueId, ActivityType, ActorName, TimestampUtc, Summary, DetailsJson
            ) VALUES (
                @Id, @IssueId, @ActivityType, @ActorName, SYSUTCDATETIME(), @Summary, @DetailsJson
            );
            """;

        return conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            ActivityType = activityType,
            ActorName = actorName,
            Summary = summary,
            DetailsJson = detailsJson
        }, transaction: tx, cancellationToken: cancellationToken));
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        IDbConnection? connection,
        IDbTransaction? transaction,
        Func<IDbConnection, IDbTransaction?, Task<T>> action)
    {
        if (connection != null)
        {
            return await action(connection, transaction);
        }

        if (transaction?.Connection != null)
        {
            return await action(transaction.Connection, transaction);
        }

        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn)
            {
                await dbConn.OpenAsync();
            }
            else
            {
                conn.Open();
            }
        }

        return await action(conn, null);
    }

    private async Task ExecuteWithConnectionAsync(
        IDbConnection? connection,
        IDbTransaction? transaction,
        Func<IDbConnection, IDbTransaction?, Task> action)
    {
        if (connection != null)
        {
            await action(connection, transaction);
            return;
        }

        if (transaction?.Connection != null)
        {
            await action(transaction.Connection, transaction);
            return;
        }

        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn)
            {
                await dbConn.OpenAsync();
            }
            else
            {
                conn.Open();
            }
        }

        await action(conn, null);
    }

    private static Issue MapToDomain(IssueRow row)
    {
        return new Issue(
            row.Id,
            CorrelationKey.FromCanonical(row.CorrelationKey),
            row.Title,
            row.DetectorId,
            row.DetectorName,
            Enum.Parse<MonitoringScope>(row.Scope, ignoreCase: true),
            Enum.Parse<IssueSeverity>(row.BaseSeverity, ignoreCase: true),
            Enum.Parse<IssueSeverity>(row.CurrentSeverity, ignoreCase: true),
            row.SeverityRank,
            Enum.Parse<IssueStatus>(row.Status, ignoreCase: true),
            row.EscalationLevel,
            row.OccurrenceCount,
            row.CurrentAssignee,
            row.CreatedAtUtc,
            row.LastOccurrenceAtUtc,
            row.AcknowledgedAtUtc,
            row.ResolvedAtUtc,
            row.DismissedAtUtc,
            row.ReopenedAtUtc);
    }

    private static Occurrence MapToDomain(OccurrenceRow row)
    {
        return new Occurrence(
            row.Id,
            row.IssueId,
            row.InspectionRunId,
            row.OccurrenceNumber,
            row.TimestampUtc,
            row.Module,
            row.SourceTable,
            row.RecordKeys,
            row.DebitTotal,
            row.CreditTotal,
            row.VarianceDelta,
            row.TraceString);
    }

    private sealed record IssueRow(
        Guid Id,
        string CorrelationKey,
        string Title,
        Guid DetectorId,
        string DetectorName,
        string Scope,
        string BaseSeverity,
        string CurrentSeverity,
        byte SeverityRank,
        string Status,
        int EscalationLevel,
        int OccurrenceCount,
        string? CurrentAssignee,
        DateTime CreatedAtUtc,
        DateTime LastOccurrenceAtUtc,
        DateTime? AcknowledgedAtUtc,
        DateTime? ResolvedAtUtc,
        DateTime? DismissedAtUtc,
        DateTime? ReopenedAtUtc);

    private sealed record OccurrenceRow(
        Guid Id,
        Guid IssueId,
        Guid InspectionRunId,
        int OccurrenceNumber,
        DateTime TimestampUtc,
        string Module,
        string? SourceTable,
        string? RecordKeys,
        decimal? DebitTotal,
        decimal? CreditTotal,
        decimal? VarianceDelta,
        string? TraceString);

    private sealed record RemediationRow(
        Guid Id,
        Guid IssueId,
        string ActionCategory,
        string SummaryDescription,
        string PerformedBy,
        DateTime TimestampUtc,
        bool IsSuperseded);

    private sealed record VerificationRow(
        Guid Id,
        Guid IssueId,
        string VerificationMethod,
        bool CleanPassed,
        string EvidenceNotes,
        string VerifiedBy,
        DateTime TimestampUtc,
        bool IsSuperseded);

    private sealed record DismissalRow(
        Guid Id,
        Guid IssueId,
        string DismissalReason,
        string JustificationRationale,
        string DismissedBy,
        DateTime TimestampUtc,
        bool IsSuperseded);

    private sealed record NoteRow(
        Guid Id,
        Guid IssueId,
        string OfficerName,
        string NoteText,
        DateTime CreatedAtUtc);
}
