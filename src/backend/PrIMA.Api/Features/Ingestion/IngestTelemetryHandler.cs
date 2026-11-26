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
        var detector = await _detectorRepository.GetByIdAsync(request.DetectorId, transaction: null, cancellationToken: cancellationToken);
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
            var lockedDetector = await _detectorRepository.LockDetectorForUpdateAsync(detector.Id, connection, transaction, cancellationToken) ?? detector;
            if (lockedDetector.AdminStatus is AdminStatus.Paused or AdminStatus.Retired)
            {
                transaction.Rollback();
                return Result<IngestTelemetryResponse>.Failure(
                    DomainError.Conflict("Detectors.NotActive", "Detector is not active."));
            }

            var triggerSource = Enum.Parse<TriggerSource>(request.TriggerSource, true);
            var outcome = Enum.Parse<InspectionOutcome>(request.Outcome, true);

            var warnings = new List<string>();
            int? actualIntervalSeconds = null;
            string? cadenceDriftWarning = null;
            var updateCadence = false;

            var baselineLastRun = lockedDetector.LastRunAtUtc ?? detector.LastRunAtUtc;

            if (triggerSource == TriggerSource.Scheduled)
            {
                updateCadence = true;
                if (baselineLastRun.HasValue)
                {
                    var elapsedSeconds = (request.ExecutionTimestampUtc - baselineLastRun.Value).TotalSeconds;
                    var actualIntervalInt = (int)Math.Round(elapsedSeconds);
                    if (actualIntervalInt < 0) actualIntervalInt = 0;
                    actualIntervalSeconds = actualIntervalInt;

                    var expected = detector.ExpectedCadenceSeconds;
                    if (elapsedSeconds < expected * 0.5 || elapsedSeconds > expected * 1.5)
                    {
                        var deviation = Math.Round(((elapsedSeconds - expected) / (double)expected) * 100, 1);
                        var sign = deviation > 0 ? "+" : "";
                        cadenceDriftWarning = $"Execution cadence drifted by {sign}{deviation}% (actual: {actualIntervalInt}s, expected: {expected}s)";
                        warnings.Add(cadenceDriftWarning);
                    }
                    else
                    {
                        cadenceDriftWarning = null;
                    }
                }
            }

            var inspectionRun = InspectionRun.Create(
                detector.Id,
                request.ExecutionTimestampUtc,
                triggerSource,
                outcome,
                request.DurationMs,
                request.FailureReason);

            // Record InspectionRun
            await _detectorRepository.RecordInspectionRunAsync(inspectionRun, connection, transaction, cancellationToken);

            if (outcome == InspectionOutcome.ExecutionFailed)
            {
                // Updates detector HealthStatus = DetectorHealth.Failing via _detectorRepository.UpdateHealthAndHeartbeatAsync
                await _detectorRepository.UpdateHealthAndHeartbeatAsync(
                    detector.Id,
                    DetectorHealth.Failing,
                    lastRunUtc: request.ExecutionTimestampUtc,
                    lastHealthyUtc: null,
                    connection,
                    transaction,
                    lastActualIntervalSeconds: actualIntervalSeconds,
                    cadenceDriftWarning: cadenceDriftWarning,
                    updateCadence: updateCadence,
                    cancellationToken: cancellationToken);

                // Infrastructure anomaly correlation (BR-PRIMA-005):
                var failCorrelationKey = $"FAIL:{detector.Id}";
                var existingFailIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                    failCorrelationKey,
                    connection,
                    transaction,
                    cancellationToken);

                var isNew = existingFailIssue is null;
                var failureTrace = request.FailureReason ?? "Detector reported execution failure.";
                var evidence = new TelemetryEvidence(
                    module: string.IsNullOrWhiteSpace(detector.TargetModule) ? "System" : detector.TargetModule,
                    trace: failureTrace);

                var failResults = new List<FindingResultItem>();

                if (existingFailIssue is null)
                {
                    var correlationKey = CorrelationKey.FromCanonical(failCorrelationKey);
                    var title = $"Execution Failure: {detector.Name}";
                    if (title.Length > 250)
                    {
                        title = title[..250];
                    }

                    var newIssue = Issue.Create(
                        correlationKey,
                        title,
                        detector.Id,
                        detector.Name,
                        MonitoringScope.Infrastructure,
                        IssueSeverity.High,
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

                        failResults.Add(new FindingResultItem
                        {
                            CorrelationKey = failCorrelationKey,
                            IssueId = newIssue.Id,
                            Action = "Created",
                            OccurrenceId = newIssue.Occurrences[0].Id,
                            OccurrenceNumber = 1
                        });
                    }
                    catch (SqlException ex) when (ex.Number is 2601 or 2627)
                    {
                        // Concurrency race: lookup retry with lock
                        existingFailIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                            failCorrelationKey,
                            connection,
                            transaction,
                            cancellationToken);

                        if (existingFailIssue is null)
                        {
                            throw;
                        }

                        isNew = false;
                        var occurrence = existingFailIssue.RecordRecurringOccurrence(inspectionRun.Id, evidence, request.ExecutionTimestampUtc);
                        if (existingFailIssue.IsClosed)
                        {
                            await _issueRepository.ReopenIssueAsync(existingFailIssue, connection, transaction, cancellationToken);
                        }
                        else
                        {
                            await _issueRepository.UpdateRecurringIssueAsync(existingFailIssue, connection, transaction, cancellationToken);
                            if (existingFailIssue.Status == IssueStatus.Investigating)
                            {
                                await _issueRepository.SupersedeActiveVerificationsAsync(existingFailIssue.Id, connection, transaction, cancellationToken);
                            }
                        }

                        await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);
                        await _issueRepository.InsertActivityLogAsync(
                            connection,
                            transaction,
                            existingFailIssue.Id,
                            "RecurrenceObserved",
                            "System",
                            $"Detector execution failure recurred (Occurrence #{occurrence.OccurrenceNumber}).",
                            detailsJson: null,
                            cancellationToken: cancellationToken);

                        failResults.Add(new FindingResultItem
                        {
                            CorrelationKey = failCorrelationKey,
                            IssueId = existingFailIssue.Id,
                            Action = existingFailIssue.IsClosed ? "Reopened" : "Appended",
                            OccurrenceId = occurrence.Id,
                            OccurrenceNumber = occurrence.OccurrenceNumber
                        });
                    }
                }
                else
                {
                    var occurrence = existingFailIssue.RecordRecurringOccurrence(inspectionRun.Id, evidence, request.ExecutionTimestampUtc);
                    if (existingFailIssue.IsClosed)
                    {
                        await _issueRepository.ReopenIssueAsync(existingFailIssue, connection, transaction, cancellationToken);
                    }
                    else
                    {
                        await _issueRepository.UpdateRecurringIssueAsync(existingFailIssue, connection, transaction, cancellationToken);
                        if (existingFailIssue.Status == IssueStatus.Investigating)
                        {
                            await _issueRepository.SupersedeActiveVerificationsAsync(existingFailIssue.Id, connection, transaction, cancellationToken);
                        }
                    }

                    await _issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);
                    await _issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        existingFailIssue.Id,
                        "RecurrenceObserved",
                        "System",
                        $"Detector execution failure recurred (Occurrence #{occurrence.OccurrenceNumber}).",
                        detailsJson: null,
                        cancellationToken: cancellationToken);

                    failResults.Add(new FindingResultItem
                    {
                        CorrelationKey = failCorrelationKey,
                        IssueId = existingFailIssue.Id,
                        Action = existingFailIssue.IsClosed ? "Reopened" : "Appended",
                        OccurrenceId = occurrence.Id,
                        OccurrenceNumber = occurrence.OccurrenceNumber
                    });
                }

                transaction.Commit();

                var failResponse = new TelemetryIngestionResponseDto
                {
                    InspectionRunId = inspectionRun.Id,
                    DetectorId = detector.Id,
                    Outcome = request.Outcome,
                    ProcessedFindingsCount = 0,
                    TotalFindingsProcessed = 0,
                    NewIssuesCreated = isNew ? 1 : 0,
                    RecurrencesRecorded = isNew ? 0 : 1,
                    Results = failResults,
                    Warnings = warnings
                };

                return Result<IngestTelemetryResponse>.Success(failResponse);
            }

            // Outcome is Passed or FindingDiscovered
            var wasFailingOrSilent = (lockedDetector.HealthStatus is DetectorHealth.Silent or DetectorHealth.Failing)
                || (detector.HealthStatus is DetectorHealth.Silent or DetectorHealth.Failing);

            // Resets HealthStatus = DetectorHealth.Healthy, updates LastHealthyHeartbeatUtc = request.ExecutionTimestampUtc
            await _detectorRepository.UpdateHealthAndHeartbeatAsync(
                detector.Id,
                DetectorHealth.Healthy,
                lastRunUtc: request.ExecutionTimestampUtc,
                lastHealthyUtc: request.ExecutionTimestampUtc,
                connection,
                transaction,
                lastActualIntervalSeconds: actualIntervalSeconds,
                cadenceDriftWarning: cadenceDriftWarning,
                updateCadence: updateCadence,
                cancellationToken: cancellationToken);

            if (wasFailingOrSilent)
            {
                // DEC-004: Queries active issues matching SILENT:{detector.Id} or FAIL:{detector.Id}
                var silentKey = $"SILENT:{detector.Id}";
                var failKey = $"FAIL:{detector.Id}";

                var silentIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                    silentKey,
                    connection,
                    transaction,
                    cancellationToken);

                if (silentIssue is not null && silentIssue.IsActive)
                {
                    await _issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        silentIssue.Id,
                        "DetectorRecovered",
                        "System",
                        $"Detector resumed healthy reporting via run '{inspectionRun.Id}'. Issue remains open for EDP verification.",
                        detailsJson: null,
                        cancellationToken: cancellationToken);
                }

                var failIssue = await _issueRepository.FindByCorrelationKeyWithLockAsync(
                    failKey,
                    connection,
                    transaction,
                    cancellationToken);

                if (failIssue is not null && failIssue.IsActive)
                {
                    await _issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        failIssue.Id,
                        "DetectorRecovered",
                        "System",
                        $"Detector resumed healthy reporting via run '{inspectionRun.Id}'. Issue remains open for EDP verification.",
                        detailsJson: null,
                        cancellationToken: cancellationToken);
                }
            }

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

            var response = new TelemetryIngestionResponseDto
            {
                InspectionRunId = inspectionRun.Id,
                DetectorId = detector.Id,
                Outcome = request.Outcome,
                ProcessedFindingsCount = results.Count,
                TotalFindingsProcessed = results.Count,
                NewIssuesCreated = results.Count(r => r.Action == "Created"),
                RecurrencesRecorded = results.Count(r => r.Action is "Appended" or "Reopened"),
                Results = results,
                Warnings = warnings
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
