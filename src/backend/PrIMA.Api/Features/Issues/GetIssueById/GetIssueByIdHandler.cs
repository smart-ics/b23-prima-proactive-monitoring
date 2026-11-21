using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Issues.GetIssueById;

public sealed class GetIssueByIdHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetIssueByIdSql = """
        -- 1. Issue Metadata
        SELECT 
            i.Id, i.CorrelationKey, i.Title, i.DetectorId, i.DetectorName, i.Scope,
            i.BaseSeverity, i.CurrentSeverity, i.SeverityRank, i.Status, i.EscalationLevel,
            i.OccurrenceCount, i.CurrentAssignee, i.CreatedAtUtc, i.LastOccurrenceAtUtc,
            i.AcknowledgedAtUtc, i.ResolvedAtUtc, i.DismissedAtUtc, i.ReopenedAtUtc
        FROM dbo.Issues i
        WHERE i.Id = @IssueId;

        -- 2. Occurrences (newest first)
        SELECT 
            o.Id, o.IssueId, o.InspectionRunId, o.OccurrenceNumber, o.TimestampUtc,
            o.Module, o.SourceTable, o.RecordKeys, o.DebitTotal, o.CreditTotal, o.VarianceDelta, o.TraceString
        FROM dbo.Occurrences o
        WHERE o.IssueId = @IssueId
        ORDER BY o.OccurrenceNumber DESC;

        -- 3. Investigation Notes (chronological)
        SELECT 
            n.Id, n.IssueId, n.OfficerName, n.NoteText, n.CreatedAtUtc
        FROM dbo.InvestigationNotes n
        WHERE n.IssueId = @IssueId
        ORDER BY n.CreatedAtUtc ASC;

        -- 4. Remediation Records (newest first)
        SELECT 
            r.Id, r.IssueId, r.ActionCategory, r.SummaryDescription, r.PerformedBy, r.TimestampUtc, r.IsSuperseded
        FROM dbo.RemediationRecords r
        WHERE r.IssueId = @IssueId
        ORDER BY r.TimestampUtc DESC;

        -- 5. Verification Records (newest first)
        SELECT 
            v.Id, v.IssueId, v.VerificationMethod, v.CleanPassed, v.EvidenceNotes, v.VerifiedBy, v.TimestampUtc, v.IsSuperseded
        FROM dbo.VerificationRecords v
        WHERE v.IssueId = @IssueId
        ORDER BY v.TimestampUtc DESC;

        -- 6. Dismissal Record (if any)
        SELECT 
            d.Id, d.IssueId, d.DismissalReason, d.JustificationRationale, d.DismissedBy, d.TimestampUtc, d.IsSuperseded
        FROM dbo.DismissalRecords d
        WHERE d.IssueId = @IssueId AND d.IsSuperseded = 0;

        -- 7. Activity Logs (newest first)
        SELECT 
            a.Id, a.IssueId, a.ActivityType, a.ActorName, a.TimestampUtc, a.Summary, a.DetailsJson
        FROM dbo.IssueActivityLogs a
        WHERE a.IssueId = @IssueId
        ORDER BY a.TimestampUtc DESC;
        """;

    public GetIssueByIdHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IssueDetailDto?> HandleAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn is DbConnection dbConnection && dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(GetIssueByIdSql, new { IssueId = issueId }, cancellationToken: cancellationToken));

        var issue = await multi.ReadSingleOrDefaultAsync<IssueDetailHeaderRaw>();
        if (issue is null)
        {
            return null;
        }

        var occurrences = (await multi.ReadAsync<OccurrenceItemDto>()).ToList();
        var notes = (await multi.ReadAsync<InvestigationNoteDto>()).ToList();
        var remediations = (await multi.ReadAsync<RemediationRecordDto>()).ToList();
        var verifications = (await multi.ReadAsync<VerificationRecordDto>()).ToList();
        var dismissal = await multi.ReadSingleOrDefaultAsync<DismissalRecordDto>();
        var activityLogs = (await multi.ReadAsync<IssueActivityLogDto>()).ToList();

        return new IssueDetailDto(
            issue,
            occurrences,
            notes,
            remediations,
            verifications,
            dismissal,
            activityLogs
        );
    }

    public Task<IssueDetailDto?> Handle(Guid issueId, CancellationToken cancellationToken = default) =>
        HandleAsync(issueId, cancellationToken);
}
