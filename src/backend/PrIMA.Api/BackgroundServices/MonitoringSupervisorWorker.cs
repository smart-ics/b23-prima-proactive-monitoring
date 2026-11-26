using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.BackgroundServices;

public sealed class MonitoringSupervisorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MonitoringSupervisorWorker> _logger;
    private DateTime? _lastRetentionPruningUtc;

    public MonitoringSupervisorWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MonitoringSupervisorWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingIntervalSeconds = _configuration.GetValue<int>("Supervisor:PollingIntervalSeconds", 60);
        if (pollingIntervalSeconds <= 0)
        {
            pollingIntervalSeconds = 60;
        }

        _logger.LogInformation("MonitoringSupervisorWorker starting with polling interval of {Interval}s.", pollingIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExecuteSupervisionCycleAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during MonitoringSupervisorWorker cycle.");
            }
        }

        _logger.LogInformation("MonitoringSupervisorWorker is stopping.");
    }

    public async Task ExecuteSupervisionCycleAsync(DateTime? simulatedUtcNow = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var detectorRepository = scope.ServiceProvider.GetRequiredService<IDetectorRepository>();
        var issueRepository = scope.ServiceProvider.GetRequiredService<IIssueRepository>();

        var slaTimeoutMinutes = _configuration.GetValue<int>("Supervisor:UnacknowledgedSlaTimeoutMinutes", 30);
        if (slaTimeoutMinutes <= 0)
        {
            slaTimeoutMinutes = 30;
        }
        var slaTimeout = TimeSpan.FromMinutes(slaTimeoutMinutes);

        var utcNow = simulatedUtcNow ?? DateTime.UtcNow;

        // Phase 1: Silence Supervision (BR-PRIMA-010, BR-PRIMA-005, DEC-003, DEC-005)
        await ExecuteSilenceSupervisionPhaseAsync(connectionFactory, detectorRepository, issueRepository, utcNow, cancellationToken);

        // Phase 2: Dynamic Severity Escalation (BR-PRIMA-003, DEC-006, DEC-007)
        await ExecuteSeverityEscalationPhaseAsync(connectionFactory, issueRepository, slaTimeout, utcNow, cancellationToken);

        // Phase 3: Automated 90-Day Retention Pruning (DEC-009, BR-PRIMA-009)
        var retentionInterval = TimeSpan.FromHours(24);
        if (_lastRetentionPruningUtc is null || (utcNow - _lastRetentionPruningUtc.Value) >= retentionInterval)
        {
            await ExecuteRetentionPruningPhaseAsync(connectionFactory, utcNow, cancellationToken);
            _lastRetentionPruningUtc = utcNow;
        }
    }

    public async Task<int> ExecuteRetentionPruningAsync(DateTime? simulatedUtcNow = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var utcNow = simulatedUtcNow ?? DateTime.UtcNow;

        var deleted = await ExecuteRetentionPruningPhaseAsync(connectionFactory, utcNow, cancellationToken);
        _lastRetentionPruningUtc = utcNow;
        return deleted;
    }

    private async Task<int> ExecuteRetentionPruningPhaseAsync(
        ISqlConnectionFactory connectionFactory,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var retentionDays = _configuration.GetValue<int>("Retention:InspectionRunsDays", 90);
        if (retentionDays <= 0)
        {
            retentionDays = 90;
        }

        var cutoffUtc = utcNow.AddDays(-retentionDays);
        var totalDeleted = 0;

        try
        {
            using var connection = connectionFactory.CreateConnection();
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

            const string deleteBatchSql = """
                DELETE TOP (1000) ir
                FROM dbo.InspectionRuns ir
                WHERE ir.Outcome = 'Passed'
                  AND ir.ExecutionTimestampUtc < @CutoffUtc
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.Occurrences o WHERE o.InspectionRunId = ir.Id
                  );
                """;

            while (!cancellationToken.IsCancellationRequested)
            {
                var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                    deleteBatchSql,
                    new { CutoffUtc = cutoffUtc },
                    cancellationToken: cancellationToken));

                totalDeleted += rowsAffected;

                if (rowsAffected == 0)
                {
                    break;
                }

                _logger.LogInformation("Retention pruning batch completed: deleted {BatchCount} clean passed runs (total: {TotalCount}).", rowsAffected, totalDeleted);
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation("Automated retention pruning finished. Total clean passed runs deleted: {TotalDeleted} older than {CutoffUtc}.", totalDeleted, cutoffUtc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute automated retention pruning cycle.");
        }

        return totalDeleted;
    }

    private async Task ExecuteSilenceSupervisionPhaseAsync(
        ISqlConnectionFactory connectionFactory,
        IDetectorRepository detectorRepository,
        IIssueRepository issueRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
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

        // Query active detectors where COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < DATEADD(SECOND, -(ExpectedCadenceSeconds * 2.0), @UtcNow)
        const string candidateSql = """
            SELECT d.Id
            FROM dbo.Detectors d WITH (NOLOCK)
            WHERE d.AdminStatus = 'Active'
              AND COALESCE(d.LastHealthyHeartbeatUtc, d.CreatedAtUtc) < DATEADD(SECOND, -(d.ExpectedCadenceSeconds * 2.0), @UtcNow);
            """;

        var candidateIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            candidateSql,
            new { UtcNow = utcNow },
            cancellationToken: cancellationToken))).ToList();

        foreach (var detectorId in candidateIds)
        {
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            try
            {
                var lockedDetector = await detectorRepository.LockDetectorForUpdateAsync(detectorId, connection, transaction, cancellationToken);
                if (lockedDetector is null || !lockedDetector.IsActive)
                {
                    transaction.Commit();
                    continue;
                }

                // Re-evaluate silence condition under lock to avoid race conditions with incoming recovery (RISK-003)
                if (!lockedDetector.IsSilenceThresholdExceeded(utcNow))
                {
                    transaction.Commit();
                    continue;
                }

                lockedDetector.MarkSilent(utcNow);
                await detectorRepository.UpdateHealthAndHeartbeatAsync(
                    lockedDetector.Id,
                    DetectorHealth.Silent,
                    lockedDetector.LastRunAtUtc,
                    lockedDetector.LastHealthyHeartbeatUtc,
                    connection,
                    transaction,
                    cancellationToken);

                var correlationKey = $"SILENT:{lockedDetector.Id}";
                var existingIssue = await issueRepository.FindByCorrelationKeyWithLockAsync(correlationKey, connection, transaction, cancellationToken);

                if (existingIssue is null)
                {
                    var traceString = $"Detector '{lockedDetector.Name}' missed heartbeat. Cadence: {lockedDetector.ExpectedCadenceSeconds}s. Threshold: {lockedDetector.ExpectedCadenceSeconds * 2}s.";
                    var run = InspectionRun.Create(
                        lockedDetector.Id,
                        utcNow,
                        TriggerSource.Scheduled,
                        InspectionOutcome.ExecutionFailed,
                        durationMs: 0,
                        failureReason: traceString,
                        createdAtUtc: utcNow);
                    await detectorRepository.RecordInspectionRunAsync(run, connection, transaction, cancellationToken);

                    var evidence = new TelemetryEvidence(
                        module: lockedDetector.TargetModule,
                        trace: traceString);

                    var newIssue = Issue.Create(
                        CorrelationKey.FromCanonical(correlationKey),
                        $"Silent Detector: {lockedDetector.Name} has missed cadence",
                        lockedDetector.Id,
                        lockedDetector.Name,
                        MonitoringScope.Infrastructure,
                        IssueSeverity.High,
                        run.Id,
                        evidence,
                        utcNow);

                    await issueRepository.InsertIssueAsync(newIssue, connection, transaction, cancellationToken);
                    await issueRepository.InsertOccurrenceAsync(newIssue.Occurrences[0], connection, transaction, cancellationToken);

                    await issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        newIssue.Id,
                        activityType: "Created",
                        actorName: "System",
                        summary: "Infrastructure issue auto-synthesized due to missed cadence.",
                        detailsJson: null,
                        cancellationToken: cancellationToken);
                }
                else if (existingIssue.IsActive)
                {
                    var baseline = lockedDetector.LastHealthyHeartbeatUtc ?? lockedDetector.CreatedAtUtc;
                    var elapsedSeconds = (utcNow - baseline).TotalSeconds;
                    var multiples = (int)(elapsedSeconds / lockedDetector.ExpectedCadenceSeconds);

                    if (multiples >= 3 && multiples > (existingIssue.OccurrenceCount + 1))
                    {
                        var run = InspectionRun.Create(
                            lockedDetector.Id,
                            utcNow,
                            TriggerSource.Scheduled,
                            InspectionOutcome.ExecutionFailed,
                            durationMs: 0,
                            failureReason: $"Prolonged silence breached {multiples}x cadence window.",
                            createdAtUtc: utcNow);
                        await detectorRepository.RecordInspectionRunAsync(run, connection, transaction, cancellationToken);

                        var recurrenceEvidence = new TelemetryEvidence(
                            module: lockedDetector.TargetModule,
                            trace: $"Detector '{lockedDetector.Name}' missed heartbeat. Cadence: {lockedDetector.ExpectedCadenceSeconds}s. Threshold: {lockedDetector.ExpectedCadenceSeconds * 2}s.");

                        var occurrence = Occurrence.Create(
                            existingIssue.Id,
                            run.Id,
                            existingIssue.OccurrenceCount + 1,
                            recurrenceEvidence,
                            utcNow);

                        await issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);

                        existingIssue.RecordRecurringOccurrence(run.Id, recurrenceEvidence, utcNow);
                        await issueRepository.UpdateRecurringIssueAsync(existingIssue, connection, transaction, cancellationToken);

                        await issueRepository.InsertActivityLogAsync(
                            connection,
                            transaction,
                            existingIssue.Id,
                            activityType: "RecurrenceObserved",
                            actorName: "System",
                            summary: $"Prolonged silence breached {multiples}x cadence window.",
                            detailsJson: null,
                            cancellationToken: cancellationToken);
                    }
                }
                else if (existingIssue.IsClosed)
                {
                    // Reopen closed issue upon silence recurrence per BR-PRIMA-002
                    var run = InspectionRun.Create(
                        lockedDetector.Id,
                        utcNow,
                        TriggerSource.Scheduled,
                        InspectionOutcome.ExecutionFailed,
                        durationMs: 0,
                        failureReason: $"Detector '{lockedDetector.Name}' missed heartbeat after prior resolution.",
                        createdAtUtc: utcNow);
                    await detectorRepository.RecordInspectionRunAsync(run, connection, transaction, cancellationToken);

                    var evidence = new TelemetryEvidence(
                        module: lockedDetector.TargetModule,
                        trace: $"Detector '{lockedDetector.Name}' missed heartbeat. Cadence: {lockedDetector.ExpectedCadenceSeconds}s. Threshold: {lockedDetector.ExpectedCadenceSeconds * 2}s.");

                    var occurrence = existingIssue.RecordRecurringOccurrence(run.Id, evidence, utcNow);
                    await issueRepository.ReopenIssueAsync(existingIssue, connection, transaction, cancellationToken);
                    await issueRepository.InsertOccurrenceAsync(occurrence, connection, transaction, cancellationToken);

                    await issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        existingIssue.Id,
                        activityType: "Reopened",
                        actorName: "System",
                        summary: $"Silence anomaly recurred on closed issue. Issue reopened (Occurrence #{occurrence.OccurrenceNumber}).",
                        detailsJson: null,
                        cancellationToken: cancellationToken);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to supervise silence for detector {DetectorId}.", detectorId);
            }
        }
    }

    private async Task ExecuteSeverityEscalationPhaseAsync(
        ISqlConnectionFactory connectionFactory,
        IIssueRepository issueRepository,
        TimeSpan slaTimeout,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
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

        // 1. SLA unacknowledged escalation (BR-PRIMA-003, DEC-006, DEC-007)
        const string slaIssuesSql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Status IN ('Open', 'Reopened')
              AND AcknowledgedAtUtc IS NULL
              AND CurrentSeverity != 'Critical';
            """;

        var slaRows = (await connection.QueryAsync<IssueRow>(new CommandDefinition(slaIssuesSql, cancellationToken: cancellationToken))).ToList();
        foreach (var row in slaRows)
        {
            var issue = MapToIssue(row);
            if (issue.CheckSlaEscalation(slaTimeout, utcNow, out var reason))
            {
                using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    issue.EscalateSeverity(reason, utcNow);

                    const string updateSql = """
                        UPDATE dbo.Issues
                        SET CurrentSeverity = @CurrentSeverity,
                            SeverityRank = @SeverityRank,
                            EscalationLevel = @EscalationLevel
                        WHERE Id = @Id;
                        """;

                    await connection.ExecuteAsync(new CommandDefinition(
                        updateSql,
                        new
                        {
                            Id = issue.Id,
                            CurrentSeverity = issue.CurrentSeverity.ToString(),
                            issue.SeverityRank,
                            issue.EscalationLevel
                        },
                        transaction,
                        cancellationToken: cancellationToken));

                    await issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        issue.Id,
                        activityType: "SeverityEscalated",
                        actorName: "System",
                        summary: reason,
                        detailsJson: null,
                        cancellationToken: cancellationToken);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Failed to apply SLA escalation for issue {IssueId}.", issue.Id);
                }
            }
        }

        // 2. Recurrence threshold escalation (BR-PRIMA-003, DEC-006, DEC-007)
        const string recurrenceIssuesSql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Status IN ('Open', 'Acknowledged', 'Investigating', 'Reopened')
              AND OccurrenceCount >= 5
              AND CurrentSeverity != 'Critical';
            """;

        var recurRows = (await connection.QueryAsync<IssueRow>(new CommandDefinition(recurrenceIssuesSql, cancellationToken: cancellationToken))).ToList();
        foreach (var row in recurRows)
        {
            // Verify if this issue has already received a recurrence escalation
            const string checkRecurrenceLogSql = """
                SELECT COUNT(1)
                FROM dbo.IssueActivityLogs
                WHERE IssueId = @IssueId
                  AND ActivityType = 'SeverityEscalated'
                  AND (Summary LIKE '%recurred%' OR Summary LIKE '%Recurrence%');
                """;

            var alreadyEscalatedForRecurrence = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(checkRecurrenceLogSql, new { IssueId = row.Id }, cancellationToken: cancellationToken));

            if (alreadyEscalatedForRecurrence > 0)
            {
                continue;
            }

            var issue = MapToIssue(row);
            if (issue.CheckRecurrenceEscalation(utcNow, out var reason))
            {
                using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    issue.EscalateSeverity(reason, utcNow);

                    const string updateSql = """
                        UPDATE dbo.Issues
                        SET CurrentSeverity = @CurrentSeverity,
                            SeverityRank = @SeverityRank,
                            EscalationLevel = @EscalationLevel
                        WHERE Id = @Id;
                        """;

                    await connection.ExecuteAsync(new CommandDefinition(
                        updateSql,
                        new
                        {
                            Id = issue.Id,
                            CurrentSeverity = issue.CurrentSeverity.ToString(),
                            issue.SeverityRank,
                            issue.EscalationLevel
                        },
                        transaction,
                        cancellationToken: cancellationToken));

                    await issueRepository.InsertActivityLogAsync(
                        connection,
                        transaction,
                        issue.Id,
                        activityType: "SeverityEscalated",
                        actorName: "System",
                        summary: reason,
                        detailsJson: null,
                        cancellationToken: cancellationToken);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Failed to apply recurrence escalation for issue {IssueId}.", issue.Id);
                }
            }
        }
    }

    private static Issue MapToIssue(IssueRow row)
    {
        return new Issue(
            row.Id,
            CorrelationKey.FromCanonical(row.CorrelationKey),
            row.Title,
            row.DetectorId,
            row.DetectorName,
            Enum.Parse<MonitoringScope>(row.Scope, ignoreCase: true),
            Enum.Parse<IssueSeverity>(row.BaseSeverity, ignoreCase: true),
            Enum.Parse<IssueSeverity>(row.CurrentSeverity, ignoreCase: true),
            row.SeverityRank,
            Enum.Parse<IssueStatus>(row.Status, ignoreCase: true),
            row.EscalationLevel,
            row.OccurrenceCount,
            row.CurrentAssignee,
            row.CreatedAtUtc,
            row.LastOccurrenceAtUtc,
            row.AcknowledgedAtUtc,
            row.ResolvedAtUtc,
            row.DismissedAtUtc,
            row.ReopenedAtUtc);
    }

    private sealed record IssueRow(
        Guid Id,
        string CorrelationKey,
        string Title,
        Guid DetectorId,
        string DetectorName,
        string Scope,
        string BaseSeverity,
        string CurrentSeverity,
        byte SeverityRank,
        string Status,
        int EscalationLevel,
        int OccurrenceCount,
        string? CurrentAssignee,
        DateTime CreatedAtUtc,
        DateTime LastOccurrenceAtUtc,
        DateTime? AcknowledgedAtUtc,
        DateTime? ResolvedAtUtc,
        DateTime? DismissedAtUtc,
        DateTime? ReopenedAtUtc);
}
