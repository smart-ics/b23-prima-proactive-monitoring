using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Infrastructure.Webhooks;

namespace PrIMA.Api.Features.Issues.TriggerVerificationRun;

public sealed class TriggerVerificationRunHandler
{
    private readonly IIssueRepository _issueRepository;
    private readonly IDetectorRepository _detectorRepository;
    private readonly IDetectorWebhookClient _webhookClient;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<TriggerVerificationRunHandler> _logger;

    public TriggerVerificationRunHandler(
        IIssueRepository issueRepository,
        IDetectorRepository detectorRepository,
        IDetectorWebhookClient webhookClient,
        ISqlConnectionFactory connectionFactory,
        ILogger<TriggerVerificationRunHandler> logger)
    {
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _detectorRepository = detectorRepository ?? throw new ArgumentNullException(nameof(detectorRepository));
        _webhookClient = webhookClient ?? throw new ArgumentNullException(nameof(webhookClient));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<VerificationResultDto>> HandleAsync(
        Guid issueId,
        CurrentOfficer officer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(officer);

        Issue? issue;
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

            issue = await _issueRepository.GetIssueWithGovernanceByIdAsync(issueId, readConn, tx: null, cancellationToken);
            if (issue is null)
            {
                return Result<VerificationResultDto>.Failure(IssueErrors.NotFound(issueId));
            }

            if (issue.Status != IssueStatus.Investigating)
            {
                return Result<VerificationResultDto>.Failure(IssueErrors.ActionRequiresStatus(IssueStatus.Investigating));
            }

            if (issue.LatestRemediation is null)
            {
                return Result<VerificationResultDto>.Failure(IssueErrors.RemediationRequiredBeforeVerification());
            }

            detector = await _detectorRepository.GetByIdAsync(issue.DetectorId, readConn, tx: null, cancellationToken);
        }

        if (detector is null || string.IsNullOrWhiteSpace(detector.WebhookUrl) || !Uri.TryCreate(detector.WebhookUrl, UriKind.Absolute, out _))
        {
            return Result<VerificationResultDto>.Failure(DetectorErrors.NoWebhookConfigured(issue.DetectorId));
        }

        // Invoke external probe webhook synchronously without holding DB connection
        var webhookResult = await _webhookClient.ExecuteRunAsync(
            detector.Id,
            detector.WebhookUrl,
            "AutomatedReRun",
            issue.Id,
            cancellationToken);

        if (webhookResult.IsFailure)
        {
            _logger.LogWarning("Automated verification run for issue '{IssueId}' failed: {Error}", issueId, webhookResult.Error.Message);
            return Result<VerificationResultDto>.Failure(webhookResult.Error);
        }

        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var isPassed = string.Equals(webhookResult.Value.Outcome, "Passed", StringComparison.OrdinalIgnoreCase);
        var cleanPassed = isPassed && webhookResult.Value.Findings.Count == 0;

        var evidenceNotes = cleanPassed
            ? $"Automated verification run '{runId}' completed with zero findings."
            : (string.Equals(webhookResult.Value.Outcome, "ExecutionFailed", StringComparison.OrdinalIgnoreCase)
                ? $"Automated verification run '{runId}' failed execution: {webhookResult.Value.FailureReason ?? "Detector reported execution failure."}"
                : $"Automated verification run '{runId}' discovered {webhookResult.Value.Findings.Count} finding(s). Verification failed.");

        var parsedOutcome = Enum.TryParse<InspectionOutcome>(webhookResult.Value.Outcome, true, out var oc)
            ? oc
            : (webhookResult.Value.Findings.Count > 0 ? InspectionOutcome.FindingDiscovered : InspectionOutcome.Passed);

        var inspectionRun = new InspectionRun(
            runId,
            detector.Id,
            now,
            TriggerSource.AutomatedReRun,
            parsedOutcome,
            webhookResult.Value.DurationMs,
            webhookResult.Value.FailureReason,
            now);

        var verificationRecord = new VerificationRecord(
            id: Guid.NewGuid(),
            issueId: issue.Id,
            verificationMethod: "AutomatedReRun",
            cleanPassed: cleanPassed,
            evidenceNotes: evidenceNotes,
            verifiedBy: officer.FullName,
            timestampUtc: now,
            isSuperseded: false);

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
                var currentIssue = await _issueRepository.GetIssueWithGovernanceByIdAsync(issueId, writeConn, tx, cancellationToken);
                if (currentIssue is null)
                {
                    return Result<VerificationResultDto>.Failure(IssueErrors.NotFound(issueId));
                }

                var domainResult = currentIssue.RecordVerification(
                    method: "AutomatedReRun",
                    cleanPassed: cleanPassed,
                    evidenceNotes: evidenceNotes,
                    verifiedBy: officer.FullName,
                    timestampUtc: now);

                if (domainResult.IsFailure)
                {
                    return Result<VerificationResultDto>.Failure(domainResult.Error);
                }

                await _issueRepository.SupersedeActiveVerificationsAsync(currentIssue.Id, writeConn, tx, cancellationToken);
                await _issueRepository.RecordVerificationAsync(verificationRecord, writeConn, tx, cancellationToken);
                await _detectorRepository.RecordInspectionRunAsync(inspectionRun, writeConn, tx, cancellationToken);

                var outcomeDesc = cleanPassed ? "CleanPassed" : "Failed";
                var summary = $"Automated verification recorded: {outcomeDesc}. {evidenceNotes}";

                await _issueRepository.InsertActivityLogAsync(
                    writeConn,
                    tx,
                    currentIssue.Id,
                    activityType: "VerificationRecorded",
                    actorName: officer.FullName,
                    summary: summary,
                    detailsJson: null,
                    cancellationToken: cancellationToken);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        return Result<VerificationResultDto>.Success(new VerificationResultDto(
            VerificationId: verificationRecord.Id,
            IssueId: issue.Id,
            RunId: runId,
            VerificationMethod: "AutomatedReRun",
            CleanPassed: cleanPassed,
            EvidenceNotes: evidenceNotes,
            VerifiedBy: officer.FullName,
            TimestampUtc: now,
            GateUnlocked: cleanPassed));
    }
}
