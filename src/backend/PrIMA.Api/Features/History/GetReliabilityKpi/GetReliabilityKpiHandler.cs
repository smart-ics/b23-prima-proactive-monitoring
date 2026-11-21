using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.History.GetReliabilityKpi;

public sealed class GetReliabilityKpiHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetReliabilityKpiSql = """
        -- 1. Resolved issues & MTTR (seeks IX_Issues_ResolvedAtUtc)
        SELECT
            COUNT(*) AS ResolvedInPeriod,
            AVG(CAST(DATEDIFF_BIG(second, COALESCE(ReopenedAtUtc, CreatedAtUtc), ResolvedAtUtc) AS FLOAT)) / 3600.0 AS AvgTimeToResolveHours
        FROM dbo.Issues
        WHERE ResolvedAtUtc IS NOT NULL
          AND Status = 'Resolved'
          AND ResolvedAtUtc >= @FromUtc
          AND ResolvedAtUtc <= @ToUtc;

        -- 2. Dismissed issues in period (seeks IX_Issues_DismissedAtUtc)
        SELECT COUNT(*)
        FROM dbo.Issues
        WHERE DismissedAtUtc IS NOT NULL
          AND Status = 'Dismissed'
          AND DismissedAtUtc >= @FromUtc
          AND DismissedAtUtc <= @ToUtc;

        -- 3. MTTA for issues acknowledged in period (seeks IX_Issues_AcknowledgedAtUtc)
        SELECT
            AVG(CAST(DATEDIFF_BIG(second, CreatedAtUtc, AcknowledgedAtUtc) AS FLOAT)) / 3600.0 AS AvgTimeToAcknowledgeHours
        FROM dbo.Issues
        WHERE AcknowledgedAtUtc IS NOT NULL
          AND AcknowledgedAtUtc >= @FromUtc
          AND AcknowledgedAtUtc <= @ToUtc;

        -- 4. Reopened issues in period (seeks IX_Issues_ReopenedAtUtc)
        SELECT COUNT(*)
        FROM dbo.Issues
        WHERE ReopenedAtUtc IS NOT NULL
          AND ReopenedAtUtc >= @FromUtc
          AND ReopenedAtUtc <= @ToUtc;

        -- 5. Detector pass rate across inspection runs in period (seeks IX_InspectionRuns_ExecutionTimestampUtc)
        SELECT
            COUNT(*) AS TotalRuns,
            COUNT(CASE WHEN Outcome = 'Passed' THEN 1 END) AS PassedRuns
        FROM dbo.InspectionRuns
        WHERE ExecutionTimestampUtc >= @FromUtc
          AND ExecutionTimestampUtc <= @ToUtc;

        -- 6. Scope breakdown for issues created in period (seeks IX_Issues_CreatedAtUtc)
        SELECT
            Scope AS Category,
            COUNT(*) AS [Count]
        FROM dbo.Issues
        WHERE CreatedAtUtc >= @FromUtc
          AND CreatedAtUtc <= @ToUtc
        GROUP BY Scope
        ORDER BY COUNT(*) DESC, Scope ASC;

        -- 7. Severity breakdown for issues created in period (seeks IX_Issues_CreatedAtUtc)
        SELECT
            CurrentSeverity AS Category,
            COUNT(*) AS [Count]
        FROM dbo.Issues
        WHERE CreatedAtUtc >= @FromUtc
          AND CreatedAtUtc <= @ToUtc
        GROUP BY CurrentSeverity
        ORDER BY CASE CurrentSeverity
            WHEN 'Critical' THEN 1
            WHEN 'High' THEN 2
            WHEN 'Medium' THEN 3
            WHEN 'Low' THEN 4
            ELSE 5
        END ASC;
        """;

    public GetReliabilityKpiHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ReliabilityKpiResponse> HandleAsync(
        GetReliabilityKpiQuery query,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("FromUtc", GetReliabilityKpiValidator.NormalizeToUtc(query.FromUtc), DbType.DateTime2);
        parameters.Add("ToUtc", GetReliabilityKpiValidator.NormalizeToUtc(query.ToUtc), DbType.DateTime2);

        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection && dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(GetReliabilityKpiSql, parameters, cancellationToken: cancellationToken));

        var resolvedStats = await multi.ReadSingleAsync<ResolvedStatsRaw>();
        var dismissedInPeriod = await multi.ReadSingleAsync<int>();
        var avgTimeToAcknowledgeHours = await multi.ReadSingleOrDefaultAsync<double?>();
        var reopenedIssuesCount = await multi.ReadSingleAsync<int>();
        var runStats = await multi.ReadSingleAsync<InspectionRunStatsRaw>();
        var scopeRows = (await multi.ReadAsync<BreakdownCountRaw>()).ToList();
        var severityRows = (await multi.ReadAsync<BreakdownCountRaw>()).ToList();

        var totalIssuesCreated = scopeRows.Sum(r => r.Count);
        var closedInPeriod = resolvedStats.ResolvedInPeriod + dismissedInPeriod;
        var resolutionRatePercentage = closedInPeriod == 0
            ? 0.0
            : Math.Round((double)resolvedStats.ResolvedInPeriod / closedInPeriod * 100.0, 1);

        var detectorPassRatePercentage = runStats.TotalRuns == 0
            ? 100.0
            : Math.Round((double)runStats.PassedRuns / runStats.TotalRuns * 100.0, 1);

        var scopeBreakdown = MapBreakdown(scopeRows, totalIssuesCreated);
        var severityBreakdown = MapBreakdown(severityRows, totalIssuesCreated);

        return new ReliabilityKpiResponse(
            totalIssuesCreated,
            resolutionRatePercentage,
            avgTimeToAcknowledgeHours,
            resolvedStats.AvgTimeToResolveHours,
            detectorPassRatePercentage,
            reopenedIssuesCount,
            scopeBreakdown,
            severityBreakdown);
    }

    private static IReadOnlyList<BreakdownItemDto> MapBreakdown(
        IReadOnlyList<BreakdownCountRaw> rows,
        int totalIssuesCreated)
    {
        if (totalIssuesCreated <= 0 || rows.Count == 0)
        {
            return Array.Empty<BreakdownItemDto>();
        }

        return rows
            .Select(r => new BreakdownItemDto(
                r.Category,
                r.Count,
                Math.Round((double)r.Count / totalIssuesCreated * 100.0, 1)))
            .ToList();
    }

    private sealed class ResolvedStatsRaw
    {
        public int ResolvedInPeriod { get; set; }
        public double? AvgTimeToResolveHours { get; set; }
    }

    private sealed class InspectionRunStatsRaw
    {
        public int TotalRuns { get; set; }
        public int PassedRuns { get; set; }
    }

    private sealed class BreakdownCountRaw
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
