using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Programmatic safety guardrail implementation of <see cref="IReadOnlySimrsConnectionFactory"/> enforcing BR-PRIMA-007.
/// Configures <c>ApplicationIntent=ReadOnly</c>, sets <c>READ UNCOMMITTED</c> isolation and <c>LOCK_TIMEOUT 2000</c>,
/// and applies a default 5-second <c>CommandTimeout</c> to prevent blocking hospital OLTP operations.
/// </summary>
public sealed class ReadOnlySimrsConnectionFactory : IReadOnlySimrsConnectionFactory
{
    public const int DefaultCommandTimeoutSeconds = 5;
    public const int DefaultLockTimeoutMs = 2000;

    private readonly string _connectionString;

    public int CommandTimeoutSeconds { get; }
    public int LockTimeoutMs { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ReadOnlySimrsConnectionFactory"/> with a connection string.
    /// </summary>
    /// <param name="connectionString">The raw connection string.</param>
    /// <param name="commandTimeoutSeconds">Query command timeout in seconds (defaults to 5 seconds per BR-PRIMA-007).</param>
    /// <param name="lockTimeoutMs">Session lock timeout in milliseconds (defaults to 2000ms).</param>
    public ReadOnlySimrsConnectionFactory(
        string connectionString,
        int commandTimeoutSeconds = DefaultCommandTimeoutSeconds,
        int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = EnsureReadOnlyIntent(connectionString);
        CommandTimeoutSeconds = commandTimeoutSeconds > 0 ? commandTimeoutSeconds : DefaultCommandTimeoutSeconds;
        LockTimeoutMs = lockTimeoutMs > 0 ? lockTimeoutMs : DefaultLockTimeoutMs;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ReadOnlySimrsConnectionFactory"/> from configuration.
    /// Prefers ConnectionStrings:ReadOnlySimrsDb with fallback to ConnectionStrings:PrIMADb.
    /// </summary>
    public ReadOnlySimrsConnectionFactory(
        IConfiguration configuration,
        int commandTimeoutSeconds = DefaultCommandTimeoutSeconds,
        int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connStr = configuration.GetConnectionString("ReadOnlySimrsDb")
            ?? configuration.GetConnectionString("PrIMADb")
            ?? configuration["PRIMA_SIMRS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("PRIMA_SIMRS_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Neither ConnectionStrings:ReadOnlySimrsDb nor ConnectionStrings:PrIMADb is configured.");

        _connectionString = EnsureReadOnlyIntent(connStr);
        CommandTimeoutSeconds = commandTimeoutSeconds > 0 ? commandTimeoutSeconds : DefaultCommandTimeoutSeconds;
        LockTimeoutMs = lockTimeoutMs > 0 ? lockTimeoutMs : DefaultLockTimeoutMs;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await ConfigureSessionAsync(conn, ct);
        return conn;
    }

    public SqlConnection OpenConnection()
    {
        var conn = CreateConnection();
        conn.Open();
        ConfigureSession(conn);
        return conn;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken ct = default)
    {
        SimrsSqlSafetyGuard.EnsureReadOnlySelect(sql);

        using var conn = await OpenConnectionAsync(ct);
        var commandDef = new CommandDefinition(
            commandText: sql,
            parameters: param,
            commandTimeout: CommandTimeoutSeconds,
            cancellationToken: ct);

        return await conn.QueryAsync<T>(commandDef);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default)
    {
        SimrsSqlSafetyGuard.EnsureReadOnlySelect(sql);

        using var conn = await OpenConnectionAsync(ct);
        var commandDef = new CommandDefinition(
            commandText: sql,
            parameters: param,
            commandTimeout: CommandTimeoutSeconds,
            cancellationToken: ct);

        return await conn.QuerySingleOrDefaultAsync<T>(commandDef);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default)
    {
        SimrsSqlSafetyGuard.EnsureReadOnlySelect(sql);

        using var conn = await OpenConnectionAsync(ct);
        var commandDef = new CommandDefinition(
            commandText: sql,
            parameters: param,
            commandTimeout: CommandTimeoutSeconds,
            cancellationToken: ct);

        return await conn.QueryFirstOrDefaultAsync<T>(commandDef);
    }

    private async Task ConfigureSessionAsync(SqlConnection conn, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT {LockTimeoutMs};";
        cmd.CommandTimeout = CommandTimeoutSeconds;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void ConfigureSession(SqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT {LockTimeoutMs};";
        cmd.CommandTimeout = CommandTimeoutSeconds;
        cmd.ExecuteNonQuery();
    }

    private static string EnsureReadOnlyIntent(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        return builder.ConnectionString;
    }
}
