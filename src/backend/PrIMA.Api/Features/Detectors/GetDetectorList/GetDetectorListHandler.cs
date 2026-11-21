using Dapper;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.GetDetectorList;

public sealed class GetDetectorListHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetDetectorListSql = """
        SELECT 
            d.Id, d.Name, d.Description, d.Scope, d.TargetModule, d.ExpectedCadenceSeconds,
            d.WebhookUrl, d.AdminStatus, d.HealthStatus, d.LastRunAtUtc, d.LastHealthyHeartbeatUtc,
            ISNULL(r.PassRate30d, 100.0) AS PassRate30d,
            ISNULL(r.TotalRuns30d, 0) AS TotalRuns30d,
            ISNULL(i.ActiveIssuesCount, 0) AS ActiveIssuesCount
        FROM dbo.Detectors d
        LEFT JOIN (
            SELECT 
                DetectorId,
                COUNT(*) AS TotalRuns30d,
                CAST(ROUND(COUNT(CASE WHEN Outcome = 'Passed' THEN 1 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS DECIMAL(5,1)) AS PassRate30d
            FROM dbo.InspectionRuns
            WHERE ExecutionTimestampUtc >= DATEADD(DAY, -30, SYSUTCDATETIME())
            GROUP BY DetectorId
        ) r ON r.DetectorId = d.Id
        LEFT JOIN (
            SELECT 
                DetectorId,
                COUNT(*) AS ActiveIssuesCount
            FROM dbo.Issues
            WHERE Status NOT IN ('Resolved', 'Dismissed')
            GROUP BY DetectorId
        ) i ON i.DetectorId = d.Id
        WHERE (@Scope IS NULL OR d.Scope = @Scope)
          AND (@AdminStatus IS NULL OR d.AdminStatus = @AdminStatus)
          AND (@HealthStatus IS NULL OR d.HealthStatus = @HealthStatus)
          AND (@Search IS NULL OR (
               d.Name LIKE '%' + @Search + '%' OR
               d.Description LIKE '%' + @Search + '%' OR
               d.TargetModule LIKE '%' + @Search + '%'
          ))
        ORDER BY 
            CASE d.HealthStatus 
                WHEN 'Failing' THEN 1 
                WHEN 'Silent' THEN 2 
                WHEN 'Healthy' THEN 3 
                ELSE 4 
            END ASC,
            d.Name ASC;
        """;

    public GetDetectorListHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<List<DetectorListItemDto>> HandleAsync(
        GetDetectorListQuery query,
        CancellationToken cancellationToken = default)
    {
        string? scopeParam = null;
        if (!string.IsNullOrWhiteSpace(query.Scope))
        {
            var trimmed = query.Scope.Trim();
            if (!string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
            {
                scopeParam = Enum.TryParse<MonitoringScope>(trimmed, ignoreCase: true, out var parsedScope)
                    ? parsedScope.ToString()
                    : trimmed;
            }
        }

        string? adminStatusParam = null;
        if (!string.IsNullOrWhiteSpace(query.AdminStatus))
        {
            var trimmed = query.AdminStatus.Trim();
            if (!string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
            {
                adminStatusParam = Enum.TryParse<AdminStatus>(trimmed, ignoreCase: true, out var parsedAdminStatus)
                    ? parsedAdminStatus.ToString()
                    : trimmed;
            }
        }

        string? healthStatusParam = null;
        if (!string.IsNullOrWhiteSpace(query.HealthStatus))
        {
            var trimmed = query.HealthStatus.Trim();
            if (!string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
            {
                healthStatusParam = Enum.TryParse<DetectorHealth>(trimmed, ignoreCase: true, out var parsedHealthStatus)
                    ? parsedHealthStatus.ToString()
                    : trimmed;
            }
        }

        string? searchParam = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            GetDetectorListSql,
            new
            {
                Scope = scopeParam,
                AdminStatus = adminStatusParam,
                HealthStatus = healthStatusParam,
                Search = searchParam
            },
            cancellationToken: cancellationToken);

        var detectors = await connection.QueryAsync<DetectorListItemDto>(command);
        return detectors.AsList();
    }
}
