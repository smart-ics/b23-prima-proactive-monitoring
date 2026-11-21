using System.Data;
using System.Data.Common;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.GetDetectorById;

public sealed class GetDetectorByIdHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetDetectorByIdSql = """
        -- 1. Detector Metadata
        SELECT 
            d.Id,
            d.Name,
            d.Description,
            d.Scope,
            d.TargetModule,
            d.ExpectedCadenceSeconds,
            d.WebhookUrl,
            d.AdminStatus,
            d.HealthStatus,
            d.LastRunAtUtc,
            d.LastHealthyHeartbeatUtc,
            d.CreatedAtUtc
        FROM dbo.Detectors d
        WHERE d.Id = @DetectorId;

        -- 2. 30-Day Reliability Statistics
        SELECT 
            MAX(CASE WHEN Outcome = 'Passed' THEN ExecutionTimestampUtc END) AS LastSuccessfulRunAtUtc,
            MAX(CASE WHEN Outcome = 'ExecutionFailed' THEN ExecutionTimestampUtc END) AS LastFailureAtUtc,
            COUNT(CASE WHEN ExecutionTimestampUtc >= DATEADD(DAY, -30, SYSUTCDATETIME()) THEN 1 END) AS TotalRuns30d,
            CAST(ROUND(
                COUNT(CASE WHEN ExecutionTimestampUtc >= DATEADD(DAY, -30, SYSUTCDATETIME()) AND Outcome = 'Passed' THEN 1 END) * 100.0 
                / NULLIF(COUNT(CASE WHEN ExecutionTimestampUtc >= DATEADD(DAY, -30, SYSUTCDATETIME()) THEN 1 END), 0)
            , 1) AS DECIMAL(5,1)) AS PassRate30d
        FROM dbo.InspectionRuns
        WHERE DetectorId = @DetectorId;

        -- 3. Recent 10 Inspection Runs
        SELECT TOP 10
            r.Id,
            r.ExecutionTimestampUtc,
            r.TriggerSource,
            r.Outcome,
            r.DurationMs,
            r.FailureReason,
            COUNT(o.Id) AS FindingsCount
        FROM dbo.InspectionRuns r
        LEFT JOIN dbo.Occurrences o ON o.InspectionRunId = r.Id
        WHERE r.DetectorId = @DetectorId
        GROUP BY r.Id, r.ExecutionTimestampUtc, r.TriggerSource, r.Outcome, r.DurationMs, r.FailureReason
        ORDER BY r.ExecutionTimestampUtc DESC;

        -- 4. Active Linked Issues
        SELECT 
            i.Id,
            i.CorrelationKey,
            i.Title,
            i.CurrentSeverity,
            i.SeverityRank,
            i.Status,
            i.EscalationLevel,
            i.OccurrenceCount,
            i.LastOccurrenceAtUtc
        FROM dbo.Issues i
        WHERE i.DetectorId = @DetectorId
          AND i.Status NOT IN ('Resolved', 'Dismissed')
        ORDER BY i.SeverityRank DESC, i.LastOccurrenceAtUtc DESC;
        """;

    public GetDetectorByIdHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DetectorDetailDto?> HandleAsync(Guid detectorId, CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn is DbConnection dbConnection && dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(GetDetectorByIdSql, new { DetectorId = detectorId }, cancellationToken: cancellationToken));

        var detector = await multi.ReadSingleOrDefaultAsync<DetectorMetadataRaw>();
        if (detector is null)
        {
            return null;
        }

        var statsRaw = await multi.ReadSingleOrDefaultAsync<StatsRaw>();
        var recentRuns = (await multi.ReadAsync<RecentRunSummaryDto>()).ToList();
        var activeIssues = (await multi.ReadAsync<LinkedActiveIssueDto>()).ToList();

        var reliabilityStats = new DetectorReliabilityStatsDto(
            statsRaw?.LastSuccessfulRunAtUtc,
            statsRaw?.LastFailureAtUtc,
            statsRaw?.TotalRuns30d ?? 0,
            statsRaw?.PassRate30d ?? 100.0m);

        return new DetectorDetailDto(
            detector.Id,
            detector.Name,
            detector.Description,
            detector.Scope,
            detector.TargetModule,
            detector.ExpectedCadenceSeconds,
            detector.WebhookUrl,
            detector.AdminStatus,
            detector.HealthStatus,
            detector.LastRunAtUtc,
            detector.LastHealthyHeartbeatUtc,
            detector.CreatedAtUtc,
            reliabilityStats,
            recentRuns,
            activeIssues);
    }

    private sealed class DetectorMetadataRaw
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string TargetModule { get; set; } = string.Empty;
        public int ExpectedCadenceSeconds { get; set; }
        public string? WebhookUrl { get; set; }
        public string AdminStatus { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = string.Empty;
        public DateTime? LastRunAtUtc { get; set; }
        public DateTime? LastHealthyHeartbeatUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class StatsRaw
    {
        public DateTime? LastSuccessfulRunAtUtc { get; set; }
        public DateTime? LastFailureAtUtc { get; set; }
        public int TotalRuns30d { get; set; }
        public decimal? PassRate30d { get; set; }
    }
}
