using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Ingestion;

public sealed class IngestTelemetryHandler
{
    private readonly IDetectorRepository _detectorRepository;
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _connectionFactory;

    public IngestTelemetryHandler(
        IDetectorRepository detectorRepository,
        IIssueRepository issueRepository,
        ISqlConnectionFactory connectionFactory)
    {
        _detectorRepository = detectorRepository ?? throw new ArgumentNullException(nameof(detectorRepository));
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result<IngestTelemetryResponse>> HandleAsync(
        IngestTelemetryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Guardrail 1: Detector existence
        var detector = await _detectorRepository.GetByIdAsync(request.DetectorId, conn: null, tx: null, cancellationToken: cancellationToken);
        if (detector is null)
        {
            return Result<IngestTelemetryResponse>.Failure(DetectorErrors.NotFound(request.DetectorId));
        }

        // Guardrail 2: Detector active status
        if (detector.AdminStatus is AdminStatus.Paused or AdminStatus.Retired)
        {
            return Result<IngestTelemetryResponse>.Failure(
                DomainError.Conflict("Detectors.NotActive", "Detector is not active."));
        }

        // Transaction boundary: ReadCommitted
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            if (connection is DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            var triggerSource = Enum.Parse<TriggerSource>(request.TriggerSource, true);
            var outcome = Enum.Parse<InspectionOutcome>(request.Outcome, true);

            var inspectionRun = InspectionRun.Create(
                detector.Id,
                request.ExecutionTimestampUtc,
                triggerSource,
                outcome,
                request.DurationMs,
                request.FailureReason);

            // Record InspectionRun
            await _detectorRepository.InsertInspectionRunAsync(inspectionRun, connection, transaction, cancellationToken);

            // Update detector liveness
            await _detectorRepository.UpdateLivenessAsync(
                detector.Id,
                request.ExecutionTimestampUtc,
                DetectorHealth.Healthy,
                connection,
                transaction,
                cancellationToken);

            var results = new List<FindingResultItem>();

            if (request.Findings is { Count: > 0 })
            {
                foreach (var finding in request.Findings)
                {
                    var correlationKey = CorrelationKey.Create(detector.Id, finding.TargetEntityReference);
                    var evidence = new TelemetryEvidence(
                        finding.Evidence.Module,
                        finding.Evidence.SourceTable,
                        finding.Evidence.RecordKeys,
                        finding.Evidence.DebitTotal,
                        finding.Evidence.CreditTotal,
                        finding.Evidence.VarianceDelta,
                        finding.Evidence.Trace);

                    var existingIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                        correlationKey.Value,
                        connection,
                        transaction,
                        cancellationToken);

                    if (existingIssue is null)
                    {
                        var baseSeverity = Enum.Parse<IssueSeverity>(finding.BaseSeverity, true);
                        var newIssue = Issue.Create(
                            correlationKey,
                            finding.Title,
                            detector.Id,
                            detector.Name,
                            detector.Scope,
                            baseSeverity,
                            inspectionRun.Id,
                            evidence,
                            request.ExecutionTimestampUtc);

                        try
                        {
                            await _issueRepository.InsertIssueAsync(newIssue, connection, transaction, cancellationToken);
                            await _issueRepository.InsertOccurrenceAsync(newIssue.Occurrences[0], connection, transaction, cancellationToken);

                            await _issueRepository.InsertActivityLogAsync(
                                connection,
                                transaction,
                                newIssue.Id,
                                "Created",
                                "System",
                                $"Initial anomaly discovered: {newIssue.Title} ({newIssue.BaseSeverity}).",
                                detailsJson: null,
                                cancellationToken: cancellationToken);

                            results.Add(new FindingResultItem
                            {
                                CorrelationKey = correlationKey.Value,
                                IssueId = newIssue.Id,
                                Action = "Created",
                                OccurrenceId = newIssue.Occurrences[0].Id,
                                OccurrenceNumber = 1
                            });
                        }
                        catch (SqlException ex) when (ex.Number is 2601 or 2627)
                        {
                            // Concurrency race: lookup retry with update lock
                            existingIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                                correlationKey.Value,
                                connection,
                                transaction,
                                cancellationToken);

                            if (existingIssue is null)
                            {
                                throw;
                            }

                            await ProcessExistingIssueAsync(
                                existingIssue,
                                inspectionRun.Id,
                                evidence,
                                request.ExecutionTimestampUtc,
                                correlationKey.Value,
                                connection,
                                transaction,
                                results,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        await ProcessExistingIssueAsync(
                            existingIssue,
                            inspectionRun.Id,
                            evidence,
                            request.ExecutionTimestampUtc,
                            correlationKey.Value,
                            connection,
                            transaction,
                            results,
                            cancellationToken);
                    }
                }
            }

            transaction.Commit();

            var response = new IngestTelemetryResponse
            {
                InspectionRunId = inspectionRun.Id,
                DetectorId = detector.Id,
                Outcome = request.Outcome,
                ProcessedFindingsCount = results.Count,
                Results = results
            };

            return Result<IngestTelemetryResponse>.Success(response);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task ProcessExistingIssueAsync(
        Issue issue,
        Guid inspectionRunId,
        TelemetryEvidence evidence,
        DateTime executionTimestampUtc,
        string correlationKey,
        IDbConnection connection,
        IDbTransaction transaction,
        List<FindingResultItem> results,
        CancellationToken cancellationToken)
    {
        var isClosed = issue.IsClosed;
        var occurrence = issue.RecordRecurringOccurrence(inspectionRunId, evidence, executionTimestampUtc);

        if (isClosed)
        {
            // BR-PRIMA-002: Reopen closed issue and supersede prior governance rows
            await _issueRepository.ReopenIssueAsync(issue, connection, transaction, cancellationToken);
            await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);

            await _issueRepository.InsertActivityLogAsync(
                connection,
                transaction,
                issue.Id,
                "Reopened",
                "System",
                $"Anomaly recurred on closed issue. Issue reopened (Occurrence #{occurrence.OccurrenceNumber}).",
                detailsJson: null,
                cancellationToken: cancellationToken);

            results.Add(new FindingResultItem
            {
                CorrelationKey = correlationKey,
                IssueId = issue.Id,
                Action = "Reopened",
                OccurrenceId = occurrence.Id,
                OccurrenceNumber = occurrence.OccurrenceNumber
            });
        }
        else
        {
            // Active issue: update occurrence count and last occurrence timestamp
            await _issueRepository.UpdateRecurringIssueAsync(issue, connection, transaction, cancellationToken);
            await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);

            if (issue.Status == IssueStatus.Investigating)
            {
                // GAP-005 & OQ-005: Recurrence on active investigating issue supersedes active verification
                await _issueRepository.SupersedeActiveVerificationsAsync(issue.Id, connection, transaction, cancellationToken);

                await _issueRepository.InsertActivityLogAsync(
                    connection,
                    transaction,
                    issue.Id,
                    "VerificationInvalidated",
                    "System",
                    $"Anomaly recurred during investigation (Occurrence #{occurrence.OccurrenceNumber}). Active verifications superseded.",
                    detailsJson: null,
                    cancellationToken: cancellationToken);
            }

            results.Add(new FindingResultItem
            {
                CorrelationKey = correlationKey,
                IssueId = issue.Id,
                Action = "Appended",
                OccurrenceId = occurrence.Id,
                OccurrenceNumber = occurrence.OccurrenceNumber
            });
        }
    }
}
