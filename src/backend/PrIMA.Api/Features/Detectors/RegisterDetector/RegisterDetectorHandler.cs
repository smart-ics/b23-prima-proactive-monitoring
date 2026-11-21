using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Features.Detectors.GetDetectorById;

namespace PrIMA.Api.Features.Detectors.RegisterDetector;

public sealed class RegisterDetectorHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IDetectorRepository _detectorRepository;

    public RegisterDetectorHandler(ISqlConnectionFactory connectionFactory, IDetectorRepository detectorRepository)
    {
        _connectionFactory = connectionFactory;
        _detectorRepository = detectorRepository;
    }

    public async Task<Result<DetectorDetailDto>> HandleAsync(RegisterDetectorRequest request, CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken);
            }
            else
            {
                conn.Open();
            }
        }

        using var tx = conn.BeginTransaction();

        var existing = await _detectorRepository.GetByNameAsync(request.Name, conn, tx, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<DetectorDetailDto>(DetectorErrors.DuplicateName);
        }

        if (!Enum.TryParse<MonitoringScope>(request.Scope, ignoreCase: true, out var scope))
        {
            return Result.Failure<DetectorDetailDto>(DomainError.Validation("Detectors.InvalidScope", $"Invalid monitoring scope '{request.Scope}'."));
        }

        var utcNow = DateTime.UtcNow;
        var detector = Detector.Create(
            request.Name,
            request.Description,
            scope,
            request.TargetModule,
            request.ExpectedCadenceSeconds,
            request.WebhookUrl,
            utcNow);

        await _detectorRepository.InsertAsync(detector, conn, tx, cancellationToken);

        tx.Commit();

        var responseDto = new DetectorDetailDto(
            detector.Id,
            detector.Name,
            detector.Description,
            detector.Scope.ToString(),
            detector.TargetModule,
            detector.ExpectedCadenceSeconds,
            detector.WebhookUrl,
            detector.AdminStatus.ToString(),
            detector.HealthStatus.ToString(),
            detector.LastRunAtUtc,
            detector.LastHealthyHeartbeatUtc,
            detector.CreatedAtUtc,
            new DetectorReliabilityStatsDto(null, null, 0, 100.0m),
            Array.Empty<RecentRunSummaryDto>(),
            Array.Empty<LinkedActiveIssueDto>());

        return Result.Success(responseDto);
    }
}
