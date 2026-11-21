using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.ToggleDetectorStatus;

public sealed class ToggleDetectorStatusHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IDetectorRepository _detectorRepository;

    public ToggleDetectorStatusHandler(ISqlConnectionFactory connectionFactory, IDetectorRepository detectorRepository)
    {
        _connectionFactory = connectionFactory;
        _detectorRepository = detectorRepository;
    }

    public async Task<Result> HandleAsync(Guid id, ToggleDetectorStatusRequest request, CancellationToken cancellationToken = default)
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

        var normalizedAction = request.Action.Trim().ToLowerInvariant();
        var domainResult = normalizedAction switch
        {
            "pause" => detector.Pause(),
            "resume" => detector.Resume(),
            "retire" => detector.Retire(),
            _ => Result.Failure(DomainError.Validation("Detectors.InvalidAction", $"Action '{request.Action}' is invalid. Allowed actions: 'Pause', 'Resume', 'Retire'."))
        };

        if (domainResult.IsFailure)
        {
            return domainResult;
        }

        await _detectorRepository.UpdateAdminStatusAsync(detector.Id, detector.AdminStatus, conn, tx, cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
