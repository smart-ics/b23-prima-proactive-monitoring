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
}
