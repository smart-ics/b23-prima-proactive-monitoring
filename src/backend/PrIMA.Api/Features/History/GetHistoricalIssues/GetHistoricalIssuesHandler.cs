using System.Data;
using System.Data.Common;
using System.Text;
using Dapper;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.History.GetHistoricalIssues;

/// <summary>
/// CQRS read handler for querying the historical issue ledger with status-aware lifecycle date filtering and clamped limits.
/// </summary>
public sealed class GetHistoricalIssuesHandler
{
    private const int DefaultLimit = 500;
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;

    private readonly ISqlConnectionFactory _connectionFactory;

    public GetHistoricalIssuesHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<HistoricalIssuesResponse> HandleAsync(
        GetHistoricalIssuesQuery query,
        CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(query.Limit ?? DefaultLimit, MinLimit, MaxLimit);
        var fromUtc = GetHistoricalIssuesValidator.NormalizeToUtc(query.FromUtc);
        var toUtc = GetHistoricalIssuesValidator.NormalizeToUtc(query.ToUtc);

        var statusParam = NormalizeFilterEnum<IssueStatus>(query.Status);
        var scopeParam = NormalizeFilterEnum<MonitoringScope>(query.Scope);
        var severityParam = NormalizeFilterEnum<IssueSeverity>(query.Severity);
        var searchParam = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var sqlBuilder = new StringBuilder("""
            SELECT
                Id,
                CorrelationKey,
                Title,
                DetectorId,
                DetectorName,
                Scope,
                CurrentSeverity,
                SeverityRank,
                Status,
                EscalationLevel,
                OccurrenceCount,
                CurrentAssignee,
                CreatedAtUtc,
                ResolvedAtUtc,
                DismissedAtUtc,
                ReopenedAtUtc,
                AcknowledgedAtUtc,
                LastOccurrenceAtUtc
            FROM dbo.Issues
            WHERE 1 = 1
            """);

        var parameters = new DynamicParameters();
        parameters.Add("FromUtc", fromUtc, DbType.DateTime2);
        parameters.Add("ToUtc", toUtc, DbType.DateTime2);
        parameters.Add("Limit", clampedLimit);

        // Status-aware lifecycle date filtering (DEC-004)
        if (string.Equals(statusParam, nameof(IssueStatus.Resolved), StringComparison.OrdinalIgnoreCase))
        {
            sqlBuilder.Append(" AND ResolvedAtUtc IS NOT NULL AND ResolvedAtUtc >= @FromUtc AND ResolvedAtUtc <= @ToUtc");
        }
        else if (string.Equals(statusParam, nameof(IssueStatus.Dismissed), StringComparison.OrdinalIgnoreCase))
        {
            sqlBuilder.Append(" AND DismissedAtUtc IS NOT NULL AND DismissedAtUtc >= @FromUtc AND DismissedAtUtc <= @ToUtc");
        }
        else if (string.Equals(statusParam, nameof(IssueStatus.Reopened), StringComparison.OrdinalIgnoreCase))
        {
            sqlBuilder.Append(" AND ReopenedAtUtc IS NOT NULL AND ReopenedAtUtc >= @FromUtc AND ReopenedAtUtc <= @ToUtc");
        }
        else
        {
            sqlBuilder.Append(" AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc <= @ToUtc");
        }

        if (!string.IsNullOrWhiteSpace(statusParam))
        {
            sqlBuilder.Append(" AND Status = @Status");
            parameters.Add("Status", statusParam);
        }

        if (!string.IsNullOrWhiteSpace(scopeParam))
        {
            sqlBuilder.Append(" AND Scope = @Scope");
            parameters.Add("Scope", scopeParam);
        }

        if (query.DetectorId.HasValue && query.DetectorId.Value != Guid.Empty)
        {
            sqlBuilder.Append(" AND DetectorId = @DetectorId");
            parameters.Add("DetectorId", query.DetectorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(severityParam))
        {
            sqlBuilder.Append(" AND CurrentSeverity = @Severity");
            parameters.Add("Severity", severityParam);
        }

        if (!string.IsNullOrWhiteSpace(searchParam))
        {
            sqlBuilder.Append(" AND (Title LIKE @SearchPattern ESCAPE '\\' OR DetectorName LIKE @SearchPattern ESCAPE '\\' OR CorrelationKey LIKE @SearchPattern ESCAPE '\\')");
            parameters.Add("SearchPattern", $"%{EscapeLikePattern(searchParam)}%");
        }

        sqlBuilder.Append(" ORDER BY LastOccurrenceAtUtc DESC OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;");

        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection && dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var items = (await connection.QueryAsync<HistoricalIssueItemDto>(
            new CommandDefinition(sqlBuilder.ToString(), parameters, cancellationToken: cancellationToken)))
            .AsList();

        return new HistoricalIssuesResponse(
            Items: items,
            TotalReturned: items.Count,
            ClampedLimit: clampedLimit);
    }

    private static string? NormalizeFilterEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<TEnum>(trimmed, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : trimmed;
    }

    private static string EscapeLikePattern(string input) =>
        input
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
}
