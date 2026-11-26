using Microsoft.Data.SqlClient;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Factory contract for creating and managing read-only, non-intrusive SQL connections to hospital SIMRS tables (BR-PRIMA-007).
/// </summary>
public interface IReadOnlySimrsConnectionFactory
{
    /// <summary>
    /// Default query execution timeout in seconds (5 seconds).
    /// </summary>
    int CommandTimeoutSeconds { get; }

    /// <summary>
    /// Default lock timeout in milliseconds (2000ms).
    /// </summary>
    int LockTimeoutMs { get; }

    /// <summary>
    /// Creates a new <see cref="SqlConnection"/> configured with <c>ApplicationIntent=ReadOnly</c>.
    /// </summary>
    SqlConnection CreateConnection();

    /// <summary>
    /// Opens a new <see cref="SqlConnection"/> configured with <c>ApplicationIntent=ReadOnly</c>,
    /// executes <c>SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT 2000;</c>,
    /// and returns the opened connection.
    /// </summary>
    Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Synchronously opens a new <see cref="SqlConnection"/> configured with <c>ApplicationIntent=ReadOnly</c>
    /// and initializes the session with <c>READ UNCOMMITTED</c> and <c>LOCK_TIMEOUT 2000</c>.
    /// </summary>
    SqlConnection OpenConnection();

    /// <summary>
    /// Safely executes a read-only query using Dapper, asserting static safety rules,
    /// enforcing read-uncommitted isolation, and applying the default 5-second command timeout.
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Safely executes a read-only query returning a single row or default, asserting static safety rules,
    /// enforcing read-uncommitted isolation, and applying the default 5-second command timeout.
    /// </summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Safely executes a read-only query returning the first row or default, asserting static safety rules,
    /// enforcing read-uncommitted isolation, and applying the default 5-second command timeout.
    /// </summary>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default);
}
