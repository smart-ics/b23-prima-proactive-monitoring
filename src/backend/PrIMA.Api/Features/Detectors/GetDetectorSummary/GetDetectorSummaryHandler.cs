using Dapper;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.GetDetectorList;

namespace PrIMA.Api.Features.Detectors.GetDetectorSummary;

public sealed class GetDetectorSummaryHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetDetectorSummarySql = """
        SELECT
            COUNT(*) AS TotalCount,
            COUNT(CASE WHEN AdminStatus = 'Active' AND HealthStatus = 'Healthy' THEN 1 END) AS HealthyCount,
            COUNT(CASE WHEN AdminStatus = 'Active' AND HealthStatus = 'Failing' THEN 1 END) AS FailingCount,
            COUNT(CASE WHEN AdminStatus = 'Active' AND HealthStatus = 'Silent' THEN 1 END) AS SilentCount,
            COUNT(CASE WHEN AdminStatus = 'Paused' THEN 1 END) AS PausedCount
        FROM dbo.Detectors;
        """;

    public GetDetectorSummaryHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DetectorSummaryDto> HandleAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(GetDetectorSummarySql, cancellationToken: cancellationToken);
        var summary = await connection.QuerySingleAsync<DetectorSummaryDto>(command);
        return summary;
    }
}
