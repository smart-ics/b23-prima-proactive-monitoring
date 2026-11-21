using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.UpdateDetector;

public sealed class UpdateDetectorHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IDetectorRepository _detectorRepository;

    public UpdateDetectorHandler(ISqlConnectionFactory connectionFactory, IDetectorRepository detectorRepository)
    {
        _connectionFactory = connectionFactory;
        _detectorRepository = detectorRepository;
    }

    public async Task<Result> HandleAsync(Guid id, UpdateDetectorRequest request, CancellationToken cancellationToken = default)
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

        var detector = await _detectorRepository.GetByIdAsync(id, conn, tx, cancellationToken);
        if (detector is null)
        {
            return Result.Failure(DetectorErrors.NotFound(id));
        }

        if (detector.IsRetired)
        {
            return Result.Failure(DetectorErrors.CannotModifyRetiredDetector);
        }

        if (!Enum.TryParse<MonitoringScope>(request.Scope, ignoreCase: true, out var scope))
        {
            return Result.Failure(DomainError.Validation("Detectors.InvalidScope", $"Invalid monitoring scope '{request.Scope}'."));
        }

        var updateResult = detector.UpdateConfiguration(
            request.Description,
            scope,
            request.TargetModule,
            request.ExpectedCadenceSeconds,
            request.WebhookUrl);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _detectorRepository.UpdateConfigurationAsync(detector, conn, tx, cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
