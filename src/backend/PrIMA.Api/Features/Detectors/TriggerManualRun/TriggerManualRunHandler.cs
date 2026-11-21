using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Infrastructure.Webhooks;

namespace PrIMA.Api.Features.Detectors.TriggerManualRun;

public sealed class TriggerManualRunHandler
{
    private readonly IDetectorRepository _detectorRepository;
    private readonly IDetectorWebhookClient _webhookClient;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<TriggerManualRunHandler> _logger;

    public TriggerManualRunHandler(
        IDetectorRepository detectorRepository,
        IDetectorWebhookClient webhookClient,
        ISqlConnectionFactory connectionFactory,
        ILogger<TriggerManualRunHandler> logger)
    {
        _detectorRepository = detectorRepository ?? throw new ArgumentNullException(nameof(detectorRepository));
        _webhookClient = webhookClient ?? throw new ArgumentNullException(nameof(webhookClient));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<InspectionRunResultDto>> HandleAsync(Guid detectorId, CancellationToken cancellationToken = default)
    {
        Detector? detector;
        using (var readConn = _connectionFactory.CreateConnection())
        {
            if (readConn.State != ConnectionState.Open)
            {
                if (readConn is DbConnection dbConn)
                {
                    await dbConn.OpenAsync(cancellationToken);
                }
                else
                {
                    readConn.Open();
                }
            }

            detector = await _detectorRepository.GetByIdAsync(detectorId, readConn, tx: null, cancellationToken);
        }

        if (detector is null)
        {
            return Result<InspectionRunResultDto>.Failure(DetectorErrors.NotFound(detectorId));
        }

        if (detector.IsRetired)
        {
            return Result<InspectionRunResultDto>.Failure(DetectorErrors.Retired(detectorId));
        }

        if (detector.IsPaused)
        {
            return Result<InspectionRunResultDto>.Failure(DetectorErrors.Paused(detectorId));
        }

        if (string.IsNullOrWhiteSpace(detector.WebhookUrl) || !Uri.TryCreate(detector.WebhookUrl, UriKind.Absolute, out _))
        {
            return Result<InspectionRunResultDto>.Failure(DetectorErrors.NoWebhookConfigured(detectorId));
        }

        // Invoke external probe webhook synchronously without holding DB connection
        var webhookResult = await _webhookClient.ExecuteRunAsync(
            detector.Id,
            detector.WebhookUrl,
            "Manual",
            issueId: null,
            cancellationToken: cancellationToken);

        if (webhookResult.IsFailure)
        {
            _logger.LogWarning("Manual run for detector '{DetectorId}' failed: {Error}", detectorId, webhookResult.Error.Message);
            return Result<InspectionRunResultDto>.Failure(webhookResult.Error);
        }

        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var outcomeStr = webhookResult.Value.Outcome;
        var parsedOutcome = Enum.TryParse<InspectionOutcome>(outcomeStr, true, out var oc)
            ? oc
            : (webhookResult.Value.Findings.Count > 0 ? InspectionOutcome.FindingDiscovered : InspectionOutcome.Passed);

        var inspectionRun = new InspectionRun(
            runId,
            detector.Id,
            now,
            TriggerSource.Manual,
            parsedOutcome,
            webhookResult.Value.DurationMs,
            webhookResult.Value.FailureReason,
            now);

        using (var writeConn = _connectionFactory.CreateConnection())
        {
            if (writeConn.State != ConnectionState.Open)
            {
                if (writeConn is DbConnection dbConn)
                {
                    await dbConn.OpenAsync(cancellationToken);
                }
                else
                {
                    writeConn.Open();
                }
            }

            using var tx = writeConn.BeginTransaction();
            try
            {
                await _detectorRepository.RecordInspectionRunAsync(inspectionRun, writeConn, tx, cancellationToken);

                if (parsedOutcome is InspectionOutcome.Passed or InspectionOutcome.FindingDiscovered)
                {
                    await _detectorRepository.UpdateHealthAndHeartbeatAsync(
                        detector.Id,
                        DetectorHealth.Healthy,
                        lastRunUtc: now,
                        lastHealthyUtc: now,
                        writeConn,
                        tx,
                        cancellationToken);
                }
                else if (parsedOutcome == InspectionOutcome.ExecutionFailed)
                {
                    await _detectorRepository.UpdateHealthAndHeartbeatAsync(
                        detector.Id,
                        DetectorHealth.Failing,
                        lastRunUtc: now,
                        lastHealthyUtc: null,
                        writeConn,
                        tx,
                        cancellationToken);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        return Result<InspectionRunResultDto>.Success(new InspectionRunResultDto(
            RunId: runId,
            Outcome: parsedOutcome.ToString(),
            DurationMs: webhookResult.Value.DurationMs,
            FailureReason: webhookResult.Value.FailureReason,
            FindingsCount: webhookResult.Value.Findings.Count));
    }
}
