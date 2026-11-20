using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Seeding;

public sealed class DevelopmentDemoDataSeeder
{
    private readonly IHostEnvironment _environment;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentDemoDataSeeder> _logger;

    // Deterministic Detector Identifiers
    public static readonly Guid BillingDetectorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    public static readonly Guid AdmissionDetectorId = Guid.Parse("e2c04068-8314-469b-8326-880bebbff71d");
    public static readonly Guid PrescriptionDetectorId = Guid.Parse("a5f8b912-3c4d-4e5f-6a7b-8c9d0e1f2a3b");
    public static readonly Guid ReplicationDetectorId = Guid.Parse("b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c");
    public static readonly Guid LaboratoryDetectorId = Guid.Parse("c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d");

    // Deterministic Seeded Issue Constants
    public const string ResolvedEntityReference = "INV-2026-RESOLVED-001";
    public const string DismissedEntityReference = "ADM-2026-DISMISSED-001";

    public static readonly Guid ResolvedIssueId = Guid.Parse("d0111111-1111-1111-1111-111111111101");
    public static readonly Guid ResolvedInspectionRunId = Guid.Parse("d0111111-1111-1111-1111-111111111102");
    public static readonly Guid ResolvedOccurrenceId = Guid.Parse("d0111111-1111-1111-1111-111111111103");
    public static readonly Guid ResolvedVerificationRecordId = Guid.Parse("d0111111-1111-1111-1111-111111111104");

    public static readonly Guid DismissedIssueId = Guid.Parse("d0222222-2222-2222-2222-222222222201");
    public static readonly Guid DismissedInspectionRunId = Guid.Parse("d0222222-2222-2222-2222-222222222202");
    public static readonly Guid DismissedOccurrenceId = Guid.Parse("d0222222-2222-2222-2222-222222222203");
    public static readonly Guid DismissedRecordId = Guid.Parse("d0222222-2222-2222-2222-222222222204");

    public DevelopmentDemoDataSeeder(
        IHostEnvironment environment,
        ISqlConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<DevelopmentDemoDataSeeder> logger)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() || !_configuration.GetValue<bool>("SeedDemoData"))
        {
            _logger.LogInformation("Development demo data seeding skipped (Environment: {Env}, SeedDemoData: {SeedFlag}).",
                _environment.EnvironmentName, _configuration.GetValue<bool>("SeedDemoData"));
            return;
        }

        _logger.LogInformation("Starting development demo data seeding...");

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
            var nowUtc = DateTime.UtcNow;
            var operationalTimeZone = GetOperationalTimeZone();
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, operationalTimeZone);
            var localStartOfToday = localNow.Date;
            var startOfTodayWibUtc = TimeZoneInfo.ConvertTimeToUtc(localStartOfToday, operationalTimeZone);

            // 1. Seed Detectors
            await SeedDetectorsAsync(connection, transaction, nowUtc);

            // 2. Seed Resolved Issue with VerificationRecord
            await SeedResolvedIssueAsync(connection, transaction, nowUtc, startOfTodayWibUtc);

            // 3. Seed Dismissed Issue with DismissalRecord
            await SeedDismissedIssueAsync(connection, transaction, nowUtc);

            transaction.Commit();
            _logger.LogInformation("Development demo data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to seed development demo data.");
            throw;
        }
    }

    private static async Task SeedDetectorsAsync(IDbConnection connection, IDbTransaction transaction, DateTime nowUtc)
    {
        const string detectorSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Detectors WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
                )
                VALUES (
                    @Id, @Name, @Description, @Scope, @TargetModule, @ExpectedCadenceSeconds,
                    @WebhookUrl, @AdminStatus, @HealthStatus, @LastRunAtUtc, @LastHealthyHeartbeatUtc, @CreatedAtUtc
                );
            END
            """;

        var detectors = new[]
        {
            new
            {
                Id = BillingDetectorId,
                Name = "Billing Journal Balance Checker",
                Description = "Audits general ledger journal balancing across debit and credit transactions.",
                Scope = MonitoringScope.DataIntegrity.ToString(),
                TargetModule = "Billing",
                ExpectedCadenceSeconds = 300,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Active.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc,
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc,
                CreatedAtUtc = nowUtc.AddDays(-30)
            },
            new
            {
                Id = AdmissionDetectorId,
                Name = "Patient Admission Discharge Sync",
                Description = "Cross-checks admission bed states against discharge summaries to detect stale occupancies.",
                Scope = MonitoringScope.ProcessIntegrity.ToString(),
                TargetModule = "Inpatient",
                ExpectedCadenceSeconds = 600,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Active.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc,
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc,
                CreatedAtUtc = nowUtc.AddDays(-30)
            },
            new
            {
                Id = PrescriptionDetectorId,
                Name = "Prescription Stock Deductor",
                Description = "Verifies pharmacy dispensing events against inventory ledger deduction journals.",
                Scope = MonitoringScope.DataSync.ToString(),
                TargetModule = "Pharmacy",
                ExpectedCadenceSeconds = 300,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Active.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc,
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc,
                CreatedAtUtc = nowUtc.AddDays(-30)
            },
            new
            {
                Id = ReplicationDetectorId,
                Name = "EMR Database Replication Monitor",
                Description = "Monitors transactional replication latency and transaction log backlog across primary and replica.",
                Scope = MonitoringScope.Infrastructure.ToString(),
                TargetModule = "Database",
                ExpectedCadenceSeconds = 60,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Active.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc,
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc,
                CreatedAtUtc = nowUtc.AddDays(-30)
            },
            new
            {
                Id = LaboratoryDetectorId,
                Name = "Laboratory Order Sync Prober",
                Description = "Probes HL7/LIS order transmission queues between HIS and Laboratory analyzer bridge.",
                Scope = MonitoringScope.DataSync.ToString(),
                TargetModule = "Laboratory",
                ExpectedCadenceSeconds = 180,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Paused.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc.AddHours(-12),
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc.AddHours(-12),
                CreatedAtUtc = nowUtc.AddDays(-30)
            }
        };

        foreach (var detector in detectors)
        {
            await connection.ExecuteAsync(detectorSql, detector, transaction);
        }
    }

    private static async Task SeedResolvedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc,
        DateTime startOfTodayWibUtc)
    {
        var resolvedCorrelationKey = $"{BillingDetectorId:D}:{ResolvedEntityReference.ToUpperInvariant()}";
        // Ensure resolvedAtUtc is set to today WIB (within the current operational day)
        var resolvedAtUtc = nowUtc > startOfTodayWibUtc ? nowUtc : startOfTodayWibUtc.AddMinutes(30);

        const string runSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.InspectionRuns WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.InspectionRuns (
                    Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc
                )
                VALUES (
                    @Id, @DetectorId, @ExecutionTimestampUtc, @TriggerSource, @Outcome, @DurationMs, @FailureReason, @CreatedAtUtc
                );
            END
            """;

        await connection.ExecuteAsync(runSql, new
        {
            Id = ResolvedInspectionRunId,
            DetectorId = BillingDetectorId,
            ExecutionTimestampUtc = resolvedAtUtc.AddHours(-2),
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 320,
            FailureReason = (string?)null,
            CreatedAtUtc = resolvedAtUtc.AddHours(-2)
        }, transaction);

        const string issueSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Issues WHERE Id = @Id OR CorrelationKey = @CorrelationKey)
            BEGIN
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                    CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
                )
                VALUES (
                    @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope,
                    @BaseSeverity, @CurrentSeverity, @SeverityRank, @Status, @EscalationLevel, @OccurrenceCount,
                    @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc, @AcknowledgedAtUtc, @ResolvedAtUtc, NULL, NULL
                );
            END
            ELSE
            BEGIN
                -- Idempotently update ResolvedAtUtc for today WIB if issue remains in Resolved state
                UPDATE dbo.Issues
                SET ResolvedAtUtc = @ResolvedAtUtc
                WHERE (Id = @Id OR CorrelationKey = @CorrelationKey) AND Status = 'Resolved';
            END
            """;

        await connection.ExecuteAsync(issueSql, new
        {
            Id = ResolvedIssueId,
            CorrelationKey = resolvedCorrelationKey,
            Title = $"Unbalanced Journal: Billing Invoice {ResolvedEntityReference}",
            DetectorId = BillingDetectorId,
            DetectorName = "Billing Journal Balance Checker",
            Scope = MonitoringScope.DataIntegrity.ToString(),
            BaseSeverity = IssueSeverity.High.ToString(),
            CurrentSeverity = IssueSeverity.High.ToString(),
            SeverityRank = (byte)SeverityRank.High,
            Status = IssueStatus.Resolved.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "ahmad.budi",
            CreatedAtUtc = resolvedAtUtc.AddHours(-2),
            LastOccurrenceAtUtc = resolvedAtUtc.AddHours(-2),
            AcknowledgedAtUtc = (DateTime?)resolvedAtUtc.AddHours(-1.5),
            ResolvedAtUtc = resolvedAtUtc
        }, transaction);

        const string occurrenceSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Occurrences WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Occurrences (
                    Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc,
                    Module, SourceTable, RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString
                )
                VALUES (
                    @Id, @IssueId, @InspectionRunId, @OccurrenceNumber, @TimestampUtc,
                    @Module, @SourceTable, @RecordKeys, @DebitTotal, @CreditTotal, @VarianceDelta, @TraceString
                );
            END
            """;

        await connection.ExecuteAsync(occurrenceSql, new
        {
            Id = ResolvedOccurrenceId,
            IssueId = ResolvedIssueId,
            InspectionRunId = ResolvedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = resolvedAtUtc.AddHours(-2),
            Module = "Billing",
            SourceTable = "acc_journal_entries",
            RecordKeys = "invoice_id=984321, batch_no=B20261120-1",
            DebitTotal = (decimal?)1500000.00m,
            CreditTotal = (decimal?)1450000.00m,
            VarianceDelta = (decimal?)50000.00m,
            TraceString = "Debit lines sum to 1,500,000 but credit lines sum to 1,450,000."
        }, transaction);

        const string verificationSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.VerificationRecords WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.VerificationRecords (
                    Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
                )
                VALUES (
                    @Id, @IssueId, @VerificationMethod, @CleanPassed, @EvidenceNotes, @VerifiedBy, @TimestampUtc, @IsSuperseded
                );
            END
            ELSE
            BEGIN
                -- Keep VerificationRecord timestamp synced with ResolvedAtUtc when not superseded
                UPDATE dbo.VerificationRecords
                SET TimestampUtc = @TimestampUtc
                WHERE Id = @Id AND IsSuperseded = 0;
            END
            """;

        await connection.ExecuteAsync(verificationSql, new
        {
            Id = ResolvedVerificationRecordId,
            IssueId = ResolvedIssueId,
            VerificationMethod = "AutomatedReRun",
            CleanPassed = true,
            EvidenceNotes = "Automated re-run verified debit and credit totals match with zero variance.",
            VerifiedBy = "ahmad.budi",
            TimestampUtc = resolvedAtUtc,
            IsSuperseded = false
        }, transaction);
    }

    private static async Task SeedDismissedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var dismissedCorrelationKey = $"{AdmissionDetectorId:D}:{DismissedEntityReference.ToUpperInvariant()}";
        var dismissedAtUtc = nowUtc.AddHours(-2);

        const string runSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.InspectionRuns WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.InspectionRuns (
                    Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc
                )
                VALUES (
                    @Id, @DetectorId, @ExecutionTimestampUtc, @TriggerSource, @Outcome, @DurationMs, @FailureReason, @CreatedAtUtc
                );
            END
            """;

        await connection.ExecuteAsync(runSql, new
        {
            Id = DismissedInspectionRunId,
            DetectorId = AdmissionDetectorId,
            ExecutionTimestampUtc = dismissedAtUtc.AddHours(-1),
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 210,
            FailureReason = (string?)null,
            CreatedAtUtc = dismissedAtUtc.AddHours(-1)
        }, transaction);

        const string issueSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Issues WHERE Id = @Id OR CorrelationKey = @CorrelationKey)
            BEGIN
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                    CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
                )
                VALUES (
                    @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope,
                    @BaseSeverity, @CurrentSeverity, @SeverityRank, @Status, @EscalationLevel, @OccurrenceCount,
                    @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc, NULL, NULL, @DismissedAtUtc, NULL
                );
            END
            """;

        await connection.ExecuteAsync(issueSql, new
        {
            Id = DismissedIssueId,
            CorrelationKey = dismissedCorrelationKey,
            Title = $"Stale Bed Occupancy: Admission {DismissedEntityReference}",
            DetectorId = AdmissionDetectorId,
            DetectorName = "Patient Admission Discharge Sync",
            Scope = MonitoringScope.ProcessIntegrity.ToString(),
            BaseSeverity = IssueSeverity.Medium.ToString(),
            CurrentSeverity = IssueSeverity.Medium.ToString(),
            SeverityRank = (byte)SeverityRank.Medium,
            Status = IssueStatus.Dismissed.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "siti.rahma",
            CreatedAtUtc = dismissedAtUtc.AddHours(-1),
            LastOccurrenceAtUtc = dismissedAtUtc.AddHours(-1),
            DismissedAtUtc = dismissedAtUtc
        }, transaction);

        const string occurrenceSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Occurrences WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Occurrences (
                    Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc,
                    Module, SourceTable, RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString
                )
                VALUES (
                    @Id, @IssueId, @InspectionRunId, @OccurrenceNumber, @TimestampUtc,
                    @Module, @SourceTable, @RecordKeys, @DebitTotal, @CreditTotal, @VarianceDelta, @TraceString
                );
            END
            """;

        await connection.ExecuteAsync(occurrenceSql, new
        {
            Id = DismissedOccurrenceId,
            IssueId = DismissedIssueId,
            InspectionRunId = DismissedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = dismissedAtUtc.AddHours(-1),
            Module = "Inpatient",
            SourceTable = "bed_occupancies",
            RecordKeys = "bed_id=B-302, patient_id=P-8831",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Bed occupancy marked active despite discharge timestamp recorded."
        }, transaction);

        const string dismissalSql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.DismissalRecords WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.DismissalRecords (
                    Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded
                )
                VALUES (
                    @Id, @IssueId, @DismissalReason, @JustificationRationale, @DismissedBy, @TimestampUtc, @IsSuperseded
                );
            END
            """;

        await connection.ExecuteAsync(dismissalSql, new
        {
            Id = DismissedRecordId,
            IssueId = DismissedIssueId,
            DismissalReason = "AcceptedOperationalException",
            JustificationRationale = "Patient transfer delayed due to emergency medical protocol; manually cleared by nursing coordinator.",
            DismissedBy = "siti.rahma",
            TimestampUtc = dismissedAtUtc,
            IsSuperseded = false
        }, transaction);
    }

    private TimeZoneInfo GetOperationalTimeZone()
    {
        var timeZoneId = _configuration["OperationalTimeZone"] ?? "Asia/Jakarta";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("Asia/Jakarta", TimeSpan.FromHours(7), "WIB", "WIB");
            }
        }
    }
}
