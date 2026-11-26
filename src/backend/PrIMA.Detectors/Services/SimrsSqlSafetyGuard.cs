using System.Text.RegularExpressions;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Static safety guard enforcing BR-PRIMA-007 non-intrusive read-only SQL execution against hospital SIMRS tables.
/// Scans every query string to ensure no mutation (DML/DDL) statements are executed.
/// </summary>
public static class SimrsSqlSafetyGuard
{
    private static readonly Regex ForbiddenMutationRegex = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|EXEC|EXECUTE|MERGE|CREATE|GRANT|REVOKE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Validates that the provided SQL query does not contain mutation keywords.
    /// Throws <see cref="InvalidOperationException"/> if any forbidden DML/DDL keyword is detected.
    /// </summary>
    /// <param name="sql">The SQL statement to validate.</param>
    /// <returns>The validated SQL statement.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a forbidden keyword is detected or query is empty.</exception>
    public static string EnsureReadOnlySelect(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("SQL query cannot be null or empty.");
        }

        var match = ForbiddenMutationRegex.Match(sql);
        if (match.Success)
        {
            throw new InvalidOperationException(
                $"Forbidden SQL mutation statement detected: '{match.Value.ToUpperInvariant()}'. Only read-only SELECT queries are permitted on SIMRS tables (BR-PRIMA-007).");
        }

        return sql;
    }

    /// <summary>
    /// Checks whether the SQL query contains no forbidden mutation keywords.
    /// </summary>
    /// <param name="sql">The SQL query to check.</param>
    /// <returns>True if the query is safe for read-only execution; otherwise false.</returns>
    public static bool IsReadOnlySelect(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        return !ForbiddenMutationRegex.IsMatch(sql);
    }
}
