using System.Data;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Domain.Issues;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(Guid id, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<Issue?> FindByCorrelationKeyAsync(string correlationKey, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<Issue?> FindByCorrelationKeyWithLockAsync(string correlationKey, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<Issue?> FindByCorrelationKeyWithLockAsync(string correlationKey, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task InsertAsync(Issue issue, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InsertIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Issue issue, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateRecurringIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task ReopenIssueAsync(Issue issue, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task InsertOccurrenceAsync(Occurrence occurrence, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InsertOccurrenceAsync(Occurrence occurrence, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task InsertInspectionRunAsync(InspectionRun run, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task SupersedePriorGovernanceRecordsAsync(Guid issueId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Occurrence>> GetOccurrencesByIssueIdAsync(Guid issueId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);

    Task TakeItAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid issueId, string officerName, IssueStatus newStatus, DateTime timestampUtc, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task StartInvestigationAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task AddNoteAsync(InvestigationNote note, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task RecordRemediationAsync(RemediationRecord remediation, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task RecordVerificationAsync(VerificationRecord verification, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task ResolveIssueAsync(Guid issueId, DateTime resolvedAtUtc, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task DismissIssueAsync(Guid issueId, DismissalRecord dismissal, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task SupersedeActiveVerificationsAsync(Guid issueId, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<Issue?> GetIssueWithGovernanceByIdAsync(Guid issueId, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task InsertActivityLogAsync(IDbConnection conn, IDbTransaction tx, Guid issueId, string activityType, string actorName, string summary, string? detailsJson = null, CancellationToken cancellationToken = default);
}
