using System.Data;

namespace PrIMA.Api.Domain.Detectors;

public interface IDetectorRepository
{
    Task<Detector?> GetByIdAsync(Guid id, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<Detector?> GetByIdAsync(Guid id, IDbConnection conn, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<Detector?> GetByNameAsync(string name, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<Detector?> GetByNameAsync(string name, IDbConnection conn, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Detector>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InsertAsync(Detector detector, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InsertAsync(Detector detector, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);
    Task UpdateAsync(Detector detector, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateConfigurationAsync(Detector detector, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);
    Task UpdateAdminStatusAsync(Guid id, AdminStatus status, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);
    Task UpdateHealthAndHeartbeatAsync(Guid id, DetectorHealth health, DateTime? lastRunUtc, DateTime? lastHealthyUtc, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Detector>> GetActiveSilentCandidatesAsync(DateTime silenceCutoffUtc, IDbConnection conn, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<Detector?> LockDetectorForUpdateAsync(Guid id, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);
    Task RecordInspectionRunAsync(InspectionRun run, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default);

    Task UpdateLivenessAsync(Guid id, DateTime lastRunAtUtc, DetectorHealth health, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateLivenessAsync(Guid detectorId, DateTime runTimeUtc, DetectorHealth health, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task InsertInspectionRunAsync(InspectionRun run, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InsertInspectionRunAsync(InspectionRun run, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
}
