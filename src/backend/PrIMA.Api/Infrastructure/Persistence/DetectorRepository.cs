using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Infrastructure.Persistence;

public sealed class DetectorRepository : IDetectorRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DetectorRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Task<Detector?> GetByIdAsync(Guid id, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, transaction?.Connection!, transaction, cancellationToken);
    }

    public Task<Detector?> GetByIdAsync(Guid id, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            FROM dbo.Detectors
            WHERE Id = @Id;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<DetectorRow>(
                new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task<Detector?> GetByNameAsync(string name, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return GetByNameAsync(name, transaction?.Connection!, transaction, cancellationToken);
    }

    public Task<Detector?> GetByNameAsync(string name, IDbConnection conn, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            FROM dbo.Detectors
            WHERE Name = @Name;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<DetectorRow>(
                new CommandDefinition(sql, new { Name = name.Trim() }, transaction, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task<IReadOnlyList<Detector>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            FROM dbo.Detectors
            ORDER BY Name ASC;
            """;

        return ExecuteWithConnectionAsync(null, null, async (connection, _) =>
        {
            var rows = await connection.QueryAsync<DetectorRow>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            return (IReadOnlyList<Detector>)rows.Select(MapToDomain).ToList();
        });
    }

    public Task InsertAsync(Detector detector, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return InsertAsync(detector, transaction?.Connection!, transaction!, cancellationToken);
    }

    public Task InsertAsync(Detector detector, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detector);

        const string sql = """
            INSERT INTO dbo.Detectors (
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            ) VALUES (
                @Id, @Name, @Description, @Scope, @TargetModule, @ExpectedCadenceSeconds,
                @WebhookUrl, @AdminStatus, @HealthStatus, @LastRunAtUtc, @LastHealthyHeartbeatUtc, @CreatedAtUtc,
                @ApiKeyHash, @Version, @LastActualIntervalSeconds, @CadenceDriftWarning
            );
            """;

        var parameters = new
        {
            detector.Id,
            detector.Name,
            detector.Description,
            Scope = detector.Scope.ToString(),
            detector.TargetModule,
            detector.ExpectedCadenceSeconds,
            detector.WebhookUrl,
            AdminStatus = detector.AdminStatus.ToString(),
            HealthStatus = detector.HealthStatus.ToString(),
            detector.LastRunAtUtc,
            detector.LastHealthyHeartbeatUtc,
            detector.CreatedAtUtc,
            detector.ApiKeyHash,
            detector.Version,
            detector.LastActualIntervalSeconds,
            detector.CadenceDriftWarning
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task UpdateAsync(Detector detector, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detector);

        const string sql = """
            UPDATE dbo.Detectors
            SET Name = @Name,
                Description = @Description,
                Scope = @Scope,
                TargetModule = @TargetModule,
                ExpectedCadenceSeconds = @ExpectedCadenceSeconds,
                WebhookUrl = @WebhookUrl,
                AdminStatus = @AdminStatus,
                HealthStatus = @HealthStatus,
                LastRunAtUtc = @LastRunAtUtc,
                LastHealthyHeartbeatUtc = @LastHealthyHeartbeatUtc
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            detector.Id,
            detector.Name,
            detector.Description,
            Scope = detector.Scope.ToString(),
            detector.TargetModule,
            detector.ExpectedCadenceSeconds,
            detector.WebhookUrl,
            AdminStatus = detector.AdminStatus.ToString(),
            HealthStatus = detector.HealthStatus.ToString(),
            detector.LastRunAtUtc,
            detector.LastHealthyHeartbeatUtc
        };

        return ExecuteWithConnectionAsync(transaction?.Connection, transaction, (connection, tx) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, tx, cancellationToken: cancellationToken)));
    }

    public Task UpdateConfigurationAsync(Detector detector, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detector);

        const string sql = """
            UPDATE dbo.Detectors
            SET Description = @Description,
                Scope = @Scope,
                TargetModule = @TargetModule,
                ExpectedCadenceSeconds = @ExpectedCadenceSeconds,
                WebhookUrl = @WebhookUrl
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            detector.Id,
            detector.Description,
            Scope = detector.Scope.ToString(),
            detector.TargetModule,
            detector.ExpectedCadenceSeconds,
            detector.WebhookUrl
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task UpdateAdminStatusAsync(Guid id, AdminStatus status, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Detectors
            SET AdminStatus = @AdminStatus
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            Id = id,
            AdminStatus = status.ToString()
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task UpdateHealthAndHeartbeatAsync(Guid id, DetectorHealth health, DateTime? lastRunUtc, DateTime? lastHealthyUtc, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        return UpdateHealthAndHeartbeatAsync(id, health, lastRunUtc, lastHealthyUtc, conn, tx, null, null, false, cancellationToken);
    }

    public Task UpdateHealthAndHeartbeatAsync(
        Guid id,
        DetectorHealth health,
        DateTime? lastRunUtc,
        DateTime? lastHealthyUtc,
        IDbConnection conn,
        IDbTransaction tx,
        int? lastActualIntervalSeconds,
        string? cadenceDriftWarning,
        bool updateCadence,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Detectors
            SET HealthStatus = @HealthStatus,
                LastRunAtUtc = COALESCE(@LastRunAtUtc, LastRunAtUtc),
                LastHealthyHeartbeatUtc = COALESCE(@LastHealthyHeartbeatUtc, LastHealthyHeartbeatUtc),
                LastActualIntervalSeconds = CASE WHEN @UpdateCadence = 1 THEN @LastActualIntervalSeconds ELSE LastActualIntervalSeconds END,
                CadenceDriftWarning = CASE WHEN @UpdateCadence = 1 THEN @CadenceDriftWarning ELSE CadenceDriftWarning END
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            Id = id,
            HealthStatus = health.ToString(),
            LastRunAtUtc = lastRunUtc,
            LastHealthyHeartbeatUtc = lastHealthyUtc,
            LastActualIntervalSeconds = lastActualIntervalSeconds,
            CadenceDriftWarning = cadenceDriftWarning,
            UpdateCadence = updateCadence ? 1 : 0
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task UpdateCadenceDriftAsync(Guid id, int? lastActualIntervalSeconds, string? cadenceDriftWarning, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Detectors
            SET LastActualIntervalSeconds = @LastActualIntervalSeconds,
                CadenceDriftWarning = @CadenceDriftWarning
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            Id = id,
            LastActualIntervalSeconds = lastActualIntervalSeconds,
            CadenceDriftWarning = cadenceDriftWarning
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task<IReadOnlyList<Detector>> GetActiveSilentCandidatesAsync(DateTime silenceCutoffUtc, IDbConnection conn, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            FROM dbo.Detectors
            WHERE AdminStatus = 'Active'
              AND COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < @SilenceCutoffUtc;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            var rows = await connection.QueryAsync<DetectorRow>(
                new CommandDefinition(sql, new { SilenceCutoffUtc = silenceCutoffUtc }, transaction, cancellationToken: cancellationToken));

            return (IReadOnlyList<Detector>)rows.Select(MapToDomain).ToList();
        });
    }

    public Task<Detector?> LockDetectorForUpdateAsync(Guid id, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash, Version, LastActualIntervalSeconds, CadenceDriftWarning
            FROM dbo.Detectors WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id;
            """;

        return ExecuteWithConnectionAsync(conn, tx, async (connection, transaction) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<DetectorRow>(
                new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken));

            return row is null ? null : MapToDomain(row);
        });
    }

    public Task RecordInspectionRunAsync(InspectionRun run, IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken = default)
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

    public Task UpdateLivenessAsync(Guid id, DateTime lastRunAtUtc, DetectorHealth health, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return UpdateLivenessAsync(id, lastRunAtUtc, health, transaction?.Connection, transaction, cancellationToken);
    }

    public Task UpdateLivenessAsync(Guid detectorId, DateTime runTimeUtc, DetectorHealth health, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Detectors
            SET LastRunAtUtc = @RunTimeUtc,
                LastHealthyHeartbeatUtc = CASE WHEN @Health = 'Healthy' THEN @RunTimeUtc ELSE LastHealthyHeartbeatUtc END,
                HealthStatus = @Health
            WHERE Id = @Id;
            """;

        var parameters = new
        {
            Id = detectorId,
            RunTimeUtc = runTimeUtc,
            Health = health.ToString()
        };

        return ExecuteWithConnectionAsync(conn, tx, (connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken)));
    }

    public Task InsertInspectionRunAsync(InspectionRun run, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return InsertInspectionRunAsync(run, transaction?.Connection, transaction, cancellationToken);
    }

    public Task InsertInspectionRunAsync(InspectionRun run, IDbConnection? conn = null, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        return RecordInspectionRunAsync(run, conn!, tx!, cancellationToken);
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

    private static Detector MapToDomain(DetectorRow row)
    {
        return new Detector(
            row.Id,
            row.Name,
            row.Description,
            Enum.Parse<MonitoringScope>(row.Scope, ignoreCase: true),
            row.TargetModule,
            row.ExpectedCadenceSeconds,
            row.WebhookUrl,
            Enum.Parse<AdminStatus>(row.AdminStatus, ignoreCase: true),
            Enum.Parse<DetectorHealth>(row.HealthStatus, ignoreCase: true),
            row.LastRunAtUtc,
            row.LastHealthyHeartbeatUtc,
            row.CreatedAtUtc,
            row.ApiKeyHash,
            row.Version,
            row.LastActualIntervalSeconds,
            row.CadenceDriftWarning);
    }

    private sealed record DetectorRow(
        Guid Id,
        string Name,
        string Description,
        string Scope,
        string TargetModule,
        int ExpectedCadenceSeconds,
        string? WebhookUrl,
        string AdminStatus,
        string HealthStatus,
        DateTime? LastRunAtUtc,
        DateTime? LastHealthyHeartbeatUtc,
        DateTime CreatedAtUtc,
        string? ApiKeyHash = null,
        string? Version = null,
        int? LastActualIntervalSeconds = null,
        string? CadenceDriftWarning = null);
}
