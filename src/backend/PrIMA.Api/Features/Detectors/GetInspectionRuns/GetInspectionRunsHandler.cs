using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.GetInspectionRuns;

public sealed class GetInspectionRunsHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public GetInspectionRunsHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<InspectionRunLogResponse?> HandleAsync(
        Guid detectorId,
        GetInspectionRunsQuery query,
        CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn is DbConnection dbConnection && dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }

        // 1. Verify detector exists
        const string checkDetectorSql = "SELECT 1 FROM dbo.Detectors WHERE Id = @DetectorId;";
        var exists = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(checkDetectorSql, new { DetectorId = detectorId }, cancellationToken: cancellationToken));

        if (exists is null)
        {
            return null;
        }

        // 2. Prepare filters and pagination
        var startDate = query.StartDateUtc ?? DateTime.UtcNow.AddDays(-7);
        var endDate = query.EndDateUtc;

        string? outcomeFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Outcome) &&
            !string.Equals(query.Outcome, "All", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = query.Outcome.Trim();
            if (string.Equals(trimmed, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                outcomeFilter = "ExecutionFailed";
            }
            else if (string.Equals(trimmed, "Finding", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trimmed, "Finding Discovered", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trimmed, "FindingDiscovered", StringComparison.OrdinalIgnoreCase))
            {
                outcomeFilter = "FindingDiscovered";
            }
            else
            {
                outcomeFilter = trimmed;
            }
        }

        string? triggerSourceFilter = null;
        if (!string.IsNullOrWhiteSpace(query.TriggerSource) &&
            !string.Equals(query.TriggerSource, "All", StringComparison.OrdinalIgnoreCase))
        {
            triggerSourceFilter = query.TriggerSource.Trim();
        }

        var limit = Math.Clamp(query.Limit ?? 50, 1, 500);
        var offset = Math.Max(0, query.Offset ?? 0);

        var whereClauses = new List<string>
        {
            "r.DetectorId = @DetectorId",
            "r.ExecutionTimestampUtc >= @StartDateUtc"
        };

        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("DetectorId", detectorId);
        dynamicParams.Add("StartDateUtc", startDate);

        if (endDate.HasValue)
        {
            whereClauses.Add("r.ExecutionTimestampUtc <= @EndDateUtc");
            dynamicParams.Add("EndDateUtc", endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(outcomeFilter))
        {
            whereClauses.Add("r.Outcome = @Outcome");
            dynamicParams.Add("Outcome", outcomeFilter);
        }

        if (!string.IsNullOrWhiteSpace(triggerSourceFilter))
        {
            whereClauses.Add("r.TriggerSource = @TriggerSource");
            dynamicParams.Add("TriggerSource", triggerSourceFilter);
        }

        var whereSql = string.Join(" AND ", whereClauses);

        dynamicParams.Add("Offset", offset);
        dynamicParams.Add("Limit", limit);

        var countAndRunsSql = $"""
            SELECT COUNT(*) 
            FROM dbo.InspectionRuns r
            WHERE {whereSql};

            SELECT 
                r.Id,
                r.DetectorId,
                r.ExecutionTimestampUtc,
                r.TriggerSource,
                r.Outcome,
                r.DurationMs,
                r.FailureReason
            FROM dbo.InspectionRuns r
            WHERE {whereSql}
            ORDER BY r.ExecutionTimestampUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;
            """;

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(countAndRunsSql, dynamicParams, cancellationToken: cancellationToken));

        var totalCount = await multi.ReadSingleAsync<int>();
        var runs = (await multi.ReadAsync<InspectionRunRowRaw>()).ToList();

        if (runs.Count == 0)
        {
            return new InspectionRunLogResponse(totalCount, Array.Empty<InspectionRunDetailDto>());
        }

        var runIds = runs.Select(r => r.Id).ToArray();
        const string occurrencesSql = """
            SELECT o.InspectionRunId, o.IssueId
            FROM dbo.Occurrences o
            WHERE o.InspectionRunId IN @RunIds;
            """;

        var occurrences = (await conn.QueryAsync<OccurrenceRunMappingRaw>(
            new CommandDefinition(occurrencesSql, new { RunIds = runIds }, cancellationToken: cancellationToken)
        )).ToList();

        var occurrencesByRun = occurrences
            .GroupBy(o => o.InspectionRunId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Count = g.Count(),
                    IssueIds = g.Select(x => x.IssueId).Distinct().ToList()
                });

        var runDtos = runs.Select(r =>
        {
            var hasOccurrences = occurrencesByRun.TryGetValue(r.Id, out var occ);
            var findingsCount = hasOccurrences ? occ!.Count : 0;
            var issueIds = hasOccurrences ? (IReadOnlyList<Guid>)occ!.IssueIds : Array.Empty<Guid>();

            return new InspectionRunDetailDto(
                r.Id,
                r.DetectorId,
                r.ExecutionTimestampUtc,
                r.TriggerSource,
                r.Outcome,
                r.DurationMs,
                r.FailureReason,
                findingsCount,
                issueIds);
        }).ToList();

        return new InspectionRunLogResponse(totalCount, runDtos);
    }

    private sealed class InspectionRunRowRaw
    {
        public Guid Id { get; set; }
        public Guid DetectorId { get; set; }
        public DateTime ExecutionTimestampUtc { get; set; }
        public string TriggerSource { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public string? FailureReason { get; set; }
    }

    private sealed class OccurrenceRunMappingRaw
    {
        public Guid InspectionRunId { get; set; }
        public Guid IssueId { get; set; }
    }
}
