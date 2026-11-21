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
    public const string OpenEntityReference = "BILL-2026-OPEN-001";
    public const string AcknowledgedEntityReference = "RX-202611-042";
    public const string InvestigatingAwaitingVerificationEntityReference = "LIS-QUEUE-4412";
    public const string InvestigatingFailedVerificationEntityReference = "EMR-DB-REPL02";
    public const string InvestigatingCleanVerificationEntityReference = "SEP-202611-9982";
    public const string ResolvedEntityReference = "INV-2026-RESOLVED-001";
    public const string DismissedEntityReference = "ADM-2026-DISMISSED-001";

    // 1. Open Issue
    public static readonly Guid OpenIssueId = Guid.Parse("d0777777-7777-7777-7777-777777777701");
    public static readonly Guid OpenInspectionRunId = Guid.Parse("d0777777-7777-7777-7777-777777777702");
    public static readonly Guid OpenOccurrenceId = Guid.Parse("d0777777-7777-7777-7777-777777777703");

    // 2. Acknowledged Issue
    public static readonly Guid AcknowledgedIssueId = Guid.Parse("d0333333-3333-3333-3333-333333333301");
    public static readonly Guid AcknowledgedInspectionRunId = Guid.Parse("d0333333-3333-3333-3333-333333333302");
    public static readonly Guid AcknowledgedOccurrenceId = Guid.Parse("d0333333-3333-3333-3333-333333333303");
    public static readonly Guid AcknowledgedNoteId = Guid.Parse("d0333333-3333-3333-3333-333333333304");

    // 3. Investigating - Awaiting Verification Issue
    public static readonly Guid InvestigatingAwaitingVerificationIssueId = Guid.Parse("d0444444-4444-4444-4444-444444444401");
    public static readonly Guid InvestigatingAwaitingVerificationInspectionRunId = Guid.Parse("d0444444-4444-4444-4444-444444444402");
    public static readonly Guid InvestigatingAwaitingVerificationOccurrenceId = Guid.Parse("d0444444-4444-4444-4444-444444444403");
    public static readonly Guid InvestigatingAwaitingVerificationNoteId = Guid.Parse("d0444444-4444-4444-4444-444444444404");
    public static readonly Guid InvestigatingAwaitingVerificationRemediationId = Guid.Parse("d0444444-4444-4444-4444-444444444405");

    // 4. Investigating - Failed Verification Issue
    public static readonly Guid InvestigatingFailedVerificationIssueId = Guid.Parse("d0555555-5555-5555-5555-555555555501");
    public static readonly Guid InvestigatingFailedVerificationInspectionRunId = Guid.Parse("d0555555-5555-5555-5555-555555555502");
    public static readonly Guid InvestigatingFailedVerificationOccurrenceId = Guid.Parse("d0555555-5555-5555-5555-555555555503");
    public static readonly Guid InvestigatingFailedVerificationRemediationId = Guid.Parse("d0555555-5555-5555-5555-555555555504");
    public static readonly Guid InvestigatingFailedVerificationRecordId = Guid.Parse("d0555555-5555-5555-5555-555555555505");

    // 5. Investigating - Clean Verification Issue
    public static readonly Guid InvestigatingCleanVerificationIssueId = Guid.Parse("d0666666-6666-6666-6666-666666666601");
    public static readonly Guid InvestigatingCleanVerificationInspectionRunId = Guid.Parse("d0666666-6666-6666-6666-666666666602");
    public static readonly Guid InvestigatingCleanVerificationOccurrenceId = Guid.Parse("d0666666-6666-6666-6666-666666666603");
    public static readonly Guid InvestigatingCleanVerificationRemediationId = Guid.Parse("d0666666-6666-6666-6666-666666666604");
    public static readonly Guid InvestigatingCleanVerificationRecordId = Guid.Parse("d0666666-6666-6666-6666-666666666605");

    // 6. Resolved Issue
    public static readonly Guid ResolvedIssueId = Guid.Parse("d0111111-1111-1111-1111-111111111101");
    public static readonly Guid ResolvedInspectionRunId = Guid.Parse("d0111111-1111-1111-1111-111111111102");
    public static readonly Guid ResolvedOccurrenceId = Guid.Parse("d0111111-1111-1111-1111-111111111103");
    public static readonly Guid ResolvedRemediationId = Guid.Parse("d0111111-1111-1111-1111-111111111105");
    public static readonly Guid ResolvedVerificationRecordId = Guid.Parse("d0111111-1111-1111-1111-111111111104");

    // 7. Dismissed Issue
    public static readonly Guid DismissedIssueId = Guid.Parse("d0222222-2222-2222-2222-222222222201");
    public static readonly Guid DismissedInspectionRunId = Guid.Parse("d0222222-2222-2222-2222-222222222202");
    public static readonly Guid DismissedOccurrenceId = Guid.Parse("d0222222-2222-2222-2222-222222222203");
    public static readonly Guid DismissedRecordId = Guid.Parse("d0222222-2222-2222-2222-222222222204");

    // Reusable Idempotent SQL Statements
    private const string RunSql = """
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

    private const string IssueSql = """
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
                @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc, @AcknowledgedAtUtc, @ResolvedAtUtc, @DismissedAtUtc, @ReopenedAtUtc
            );
        END
        """;

    private const string OccurrenceSql = """
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

    private const string NoteSql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.InvestigationNotes WHERE Id = @Id)
        BEGIN
            INSERT INTO dbo.InvestigationNotes (
                Id, IssueId, OfficerName, NoteText, CreatedAtUtc
            )
            VALUES (
                @Id, @IssueId, @OfficerName, @NoteText, @CreatedAtUtc
            );
        END
        """;

    private const string RemediationSql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.RemediationRecords WHERE Id = @Id)
        BEGIN
            INSERT INTO dbo.RemediationRecords (
                Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
            )
            VALUES (
                @Id, @IssueId, @ActionCategory, @SummaryDescription, @PerformedBy, @TimestampUtc, @IsSuperseded
            );
        END
        """;

    private const string VerificationSql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.VerificationRecords WHERE Id = @Id)
        BEGIN
            INSERT INTO dbo.VerificationRecords (
                Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
            )
            VALUES (
                @Id, @IssueId, @VerificationMethod, @CleanPassed, @EvidenceNotes, @VerifiedBy, @TimestampUtc, @IsSuperseded
            );
        END
        """;

    private const string DismissalSql = """
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

    private const string ActivityLogSql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.IssueActivityLogs WHERE Id = @Id)
        BEGIN
            INSERT INTO dbo.IssueActivityLogs (
                Id, IssueId, ActivityType, ActorName, TimestampUtc, Summary, DetailsJson
            )
            VALUES (
                @Id, @IssueId, @ActivityType, @ActorName, @TimestampUtc, @Summary, @DetailsJson
            );
        END
        """;

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

            // 1. Seed Detectors Portfolio
            await SeedDetectorsAsync(connection, transaction, nowUtc);

            // 2. Seed Open Issue (Unassigned)
            await SeedOpenIssueAsync(connection, transaction, nowUtc);

            // 3. Seed Acknowledged Issue (Assigned to siti.rahma with notes and audit trail)
            await SeedAcknowledgedIssueAsync(connection, transaction, nowUtc);

            // 4. Seed Investigating Issue (Assigned to ahmad.budi, remediation recorded, awaiting verification)
            await SeedInvestigatingAwaitingVerificationIssueAsync(connection, transaction, nowUtc);

            // 5. Seed Investigating Issue (Failed verification CleanPassed = false)
            await SeedInvestigatingFailedVerificationIssueAsync(connection, transaction, nowUtc);

            // 6. Seed Investigating Issue (Clean verification CleanPassed = true, ready for resolution)
            await SeedInvestigatingCleanVerificationIssueAsync(connection, transaction, nowUtc);

            // 7. Seed Resolved Issue (With clean verification, remediation, and activity logs)
            await SeedResolvedIssueAsync(connection, transaction, nowUtc, startOfTodayWibUtc);

            // 8. Seed Dismissed Issue (With valid dismissal justification BR-PRIMA-006 and activity logs)
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

    private static async Task SeedOpenIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{BillingDetectorId:D}:{OpenEntityReference}";
        var discoveredAt = nowUtc.AddMinutes(-20);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = OpenInspectionRunId,
            DetectorId = BillingDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 180,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = OpenIssueId,
            CorrelationKey = correlationKey,
            Title = $"Unbalanced Patient Billing Ledger: Registration {OpenEntityReference}",
            DetectorId = BillingDetectorId,
            DetectorName = "Billing Journal Balance Checker",
            Scope = MonitoringScope.DataIntegrity.ToString(),
            BaseSeverity = IssueSeverity.Medium.ToString(),
            CurrentSeverity = IssueSeverity.Medium.ToString(),
            SeverityRank = (byte)SeverityRank.Medium,
            Status = IssueStatus.Open.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = (string?)null,
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = OpenOccurrenceId,
            IssueId = OpenIssueId,
            InspectionRunId = OpenInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Billing",
            SourceTable = "acc_patient_bills",
            RecordKeys = "registration_id=REG-2026-8819, bill_id=B-99120",
            DebitTotal = (decimal?)750000.00m,
            CreditTotal = (decimal?)700000.00m,
            VarianceDelta = (decimal?)50000.00m,
            TraceString = "Registration deposit credit does not match ledger billing balance."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0777777-7777-7777-7777-777777777711"),
            IssueId = OpenIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedAcknowledgedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{PrescriptionDetectorId:D}:{AcknowledgedEntityReference}";
        var discoveredAt = nowUtc.AddMinutes(-50);
        var assignedAt = nowUtc.AddMinutes(-42);
        var acknowledgedAt = nowUtc.AddMinutes(-40);
        var noteAt = nowUtc.AddMinutes(-35);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = AcknowledgedInspectionRunId,
            DetectorId = PrescriptionDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 240,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = AcknowledgedIssueId,
            CorrelationKey = correlationKey,
            Title = $"Inventory Ledger Mismatch: Amoxicillin 500mg Batch {AcknowledgedEntityReference}",
            DetectorId = PrescriptionDetectorId,
            DetectorName = "Prescription Stock Deductor",
            Scope = MonitoringScope.DataSync.ToString(),
            BaseSeverity = IssueSeverity.Medium.ToString(),
            CurrentSeverity = IssueSeverity.Medium.ToString(),
            SeverityRank = (byte)SeverityRank.Medium,
            Status = IssueStatus.Acknowledged.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "siti.rahma",
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)acknowledgedAt,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = AcknowledgedOccurrenceId,
            IssueId = AcknowledgedIssueId,
            InspectionRunId = AcknowledgedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Pharmacy",
            SourceTable = "pharmacy_dispense_log",
            RecordKeys = "item_code=MED-AMX-500, batch_no=RX-202611-042",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)50.00m,
            TraceString = "Dispense log indicates 450 units dispensed but inventory subledger recorded 400 units deducted."
        }, transaction);

        await connection.ExecuteAsync(NoteSql, new
        {
            Id = AcknowledgedNoteId,
            IssueId = AcknowledgedIssueId,
            OfficerName = "siti.rahma",
            NoteText = "Cross-checked floor inventory in Central Pharmacy. Stock physically counted at 400 units; investigating whether 50 units return voucher was omitted from subledger posting.",
            CreatedAtUtc = noteAt
        }, transaction);

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0333333-3333-3333-3333-333333333311"),
            IssueId = AcknowledgedIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0333333-3333-3333-3333-333333333312"),
            IssueId = AcknowledgedIssueId,
            ActivityType = "Assigned",
            ActorName = "edp.lead",
            TimestampUtc = assignedAt,
            Summary = "Issue assigned to siti.rahma by edp.lead.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0333333-3333-3333-3333-333333333313"),
            IssueId = AcknowledgedIssueId,
            ActivityType = "Acknowledged",
            ActorName = "siti.rahma",
            TimestampUtc = acknowledgedAt,
            Summary = "Issue acknowledged by siti.rahma.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0333333-3333-3333-3333-333333333314"),
            IssueId = AcknowledgedIssueId,
            ActivityType = "NoteAdded",
            ActorName = "siti.rahma",
            TimestampUtc = noteAt,
            Summary = "Investigation note added by siti.rahma.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedInvestigatingAwaitingVerificationIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{LaboratoryDetectorId:D}:{InvestigatingAwaitingVerificationEntityReference}";
        var discoveredAt = nowUtc.AddHours(-3);
        var takenAt = nowUtc.AddHours(-2.8);
        var noteAt = nowUtc.AddHours(-2.5);
        var remediationAt = nowUtc.AddHours(-2);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = InvestigatingAwaitingVerificationInspectionRunId,
            DetectorId = LaboratoryDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 310,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = InvestigatingAwaitingVerificationIssueId,
            CorrelationKey = correlationKey,
            Title = $"Order Queue Stalled: LIS Analyzer Interface Queue #{InvestigatingAwaitingVerificationEntityReference}",
            DetectorId = LaboratoryDetectorId,
            DetectorName = "Laboratory Order Sync Prober",
            Scope = MonitoringScope.DataSync.ToString(),
            BaseSeverity = IssueSeverity.High.ToString(),
            CurrentSeverity = IssueSeverity.High.ToString(),
            SeverityRank = (byte)SeverityRank.High,
            Status = IssueStatus.Investigating.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "ahmad.budi",
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)takenAt,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = InvestigatingAwaitingVerificationOccurrenceId,
            IssueId = InvestigatingAwaitingVerificationIssueId,
            InspectionRunId = InvestigatingAwaitingVerificationInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Laboratory",
            SourceTable = "lis_message_queue",
            RecordKeys = "queue_id=Q-4412, test_profile=CBC_PLATELET",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "HL7 ORU^R01 packet stuck in transmit state for > 15 minutes due to socket timeout on analyzer endpoint."
        }, transaction);

        await connection.ExecuteAsync(NoteSql, new
        {
            Id = InvestigatingAwaitingVerificationNoteId,
            IssueId = InvestigatingAwaitingVerificationIssueId,
            OfficerName = "ahmad.budi",
            NoteText = "Analyzer endpoint socket timed out. Recycled bridge daemon and inspected queue lockfiles.",
            CreatedAtUtc = noteAt
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = InvestigatingAwaitingVerificationRemediationId,
            IssueId = InvestigatingAwaitingVerificationIssueId,
            ActionCategory = "ServiceRestart",
            SummaryDescription = "Restarted LIS HL7 bridge daemon (sysmex-bridge.service) and purged corrupt transmit lockfile.",
            PerformedBy = "ahmad.budi",
            TimestampUtc = remediationAt,
            IsSuperseded = false
        }, transaction);

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0444444-4444-4444-4444-444444444411"),
            IssueId = InvestigatingAwaitingVerificationIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0444444-4444-4444-4444-444444444412"),
            IssueId = InvestigatingAwaitingVerificationIssueId,
            ActivityType = "Taken",
            ActorName = "ahmad.budi",
            TimestampUtc = takenAt,
            Summary = "Issue taken and investigation started by ahmad.budi.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0444444-4444-4444-4444-444444444413"),
            IssueId = InvestigatingAwaitingVerificationIssueId,
            ActivityType = "NoteAdded",
            ActorName = "ahmad.budi",
            TimestampUtc = noteAt,
            Summary = "Investigation note added by ahmad.budi.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0444444-4444-4444-4444-444444444414"),
            IssueId = InvestigatingAwaitingVerificationIssueId,
            ActivityType = "RemediationRecorded",
            ActorName = "ahmad.budi",
            TimestampUtc = remediationAt,
            Summary = "Remediation action recorded [ServiceRestart]: Restarted LIS HL7 bridge daemon (sysmex-bridge.service) and purged corrupt transmit lockfile.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedInvestigatingFailedVerificationIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{ReplicationDetectorId:D}:{InvestigatingFailedVerificationEntityReference}";
        var discoveredAt = nowUtc.AddHours(-4);
        var takenAt = nowUtc.AddHours(-3.8);
        var remediationAt = nowUtc.AddHours(-2.5);
        var verificationAt = nowUtc.AddHours(-2);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = InvestigatingFailedVerificationInspectionRunId,
            DetectorId = ReplicationDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 190,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = InvestigatingFailedVerificationIssueId,
            CorrelationKey = correlationKey,
            Title = $"Transactional Replication Latency Spike: Node {InvestigatingFailedVerificationEntityReference}",
            DetectorId = ReplicationDetectorId,
            DetectorName = "EMR Database Replication Monitor",
            Scope = MonitoringScope.Infrastructure.ToString(),
            BaseSeverity = IssueSeverity.Critical.ToString(),
            CurrentSeverity = IssueSeverity.Critical.ToString(),
            SeverityRank = (byte)SeverityRank.Critical,
            Status = IssueStatus.Investigating.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "siti.rahma",
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)takenAt,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = InvestigatingFailedVerificationOccurrenceId,
            IssueId = InvestigatingFailedVerificationIssueId,
            InspectionRunId = InvestigatingFailedVerificationInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Database",
            SourceTable = "sys.dm_hadr_database_replica_states",
            RecordKeys = "replica_node=EMR-DB-REPL02, database=HIS_CLINICAL",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Replication lag exceeded 480 seconds (threshold 60s). Log send queue size: 1,420 MB."
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = InvestigatingFailedVerificationRemediationId,
            IssueId = InvestigatingFailedVerificationIssueId,
            ActionCategory = "ConfigurationAdjustment",
            SummaryDescription = "Increased network buffer size and max worker threads on replication subscriber interface.",
            PerformedBy = "siti.rahma",
            TimestampUtc = remediationAt,
            IsSuperseded = false
        }, transaction);

        await connection.ExecuteAsync(VerificationSql, new
        {
            Id = InvestigatingFailedVerificationRecordId,
            IssueId = InvestigatingFailedVerificationIssueId,
            VerificationMethod = "ManualQueryConfirmed",
            CleanPassed = false,
            EvidenceNotes = "Checked DMV sys.dm_hadr_database_replica_states; redo queue latency still stands at 320 seconds. Anomaly still present.",
            VerifiedBy = "siti.rahma",
            TimestampUtc = verificationAt,
            IsSuperseded = false
        }, transaction);

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0555555-5555-5555-5555-555555555511"),
            IssueId = InvestigatingFailedVerificationIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0555555-5555-5555-5555-555555555512"),
            IssueId = InvestigatingFailedVerificationIssueId,
            ActivityType = "Taken",
            ActorName = "siti.rahma",
            TimestampUtc = takenAt,
            Summary = "Issue taken and investigation started by siti.rahma.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0555555-5555-5555-5555-555555555513"),
            IssueId = InvestigatingFailedVerificationIssueId,
            ActivityType = "RemediationRecorded",
            ActorName = "siti.rahma",
            TimestampUtc = remediationAt,
            Summary = "Remediation action recorded [ConfigurationAdjustment]: Increased network buffer size and max worker threads on replication subscriber interface.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0555555-5555-5555-5555-555555555514"),
            IssueId = InvestigatingFailedVerificationIssueId,
            ActivityType = "VerificationRecorded",
            ActorName = "siti.rahma",
            TimestampUtc = verificationAt,
            Summary = "Manual verification recorded: AnomalyStillPresent. Checked DMV sys.dm_hadr_database_replica_states; redo queue latency still stands at 320 seconds. Anomaly still present.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedInvestigatingCleanVerificationIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{BillingDetectorId:D}:{InvestigatingCleanVerificationEntityReference}";
        var discoveredAt = nowUtc.AddHours(-3.5);
        var takenAt = nowUtc.AddHours(-3);
        var remediationAt = nowUtc.AddHours(-2);
        var verificationAt = nowUtc.AddHours(-1.5);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = InvestigatingCleanVerificationInspectionRunId,
            DetectorId = BillingDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 280,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = InvestigatingCleanVerificationIssueId,
            CorrelationKey = correlationKey,
            Title = $"BPJS Claims Tariff Discrepancy: SEP-{InvestigatingCleanVerificationEntityReference}",
            DetectorId = BillingDetectorId,
            DetectorName = "Billing Journal Balance Checker",
            Scope = MonitoringScope.DataIntegrity.ToString(),
            BaseSeverity = IssueSeverity.High.ToString(),
            CurrentSeverity = IssueSeverity.High.ToString(),
            SeverityRank = (byte)SeverityRank.High,
            Status = IssueStatus.Investigating.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = "ahmad.budi",
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)takenAt,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = InvestigatingCleanVerificationOccurrenceId,
            IssueId = InvestigatingCleanVerificationIssueId,
            InspectionRunId = InvestigatingCleanVerificationInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Billing",
            SourceTable = "bpjs_claim_submissions",
            RecordKeys = "sep_number=SEP-202611-9982, invoice_id=INV-77621",
            DebitTotal = (decimal?)12500000.00m,
            CreditTotal = (decimal?)11000000.00m,
            VarianceDelta = (decimal?)1500000.00m,
            TraceString = "Tariff group INA-CBG code mismatch between surgical procedure log and final invoice billing line."
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = InvestigatingCleanVerificationRemediationId,
            IssueId = InvestigatingCleanVerificationIssueId,
            ActionCategory = "DataCorrection",
            SummaryDescription = "Re-grouped diagnosis code in casemix module per ICD-10 guide and generated updated SEP claim slip.",
            PerformedBy = "ahmad.budi",
            TimestampUtc = remediationAt,
            IsSuperseded = false
        }, transaction);

        await connection.ExecuteAsync(VerificationSql, new
        {
            Id = InvestigatingCleanVerificationRecordId,
            IssueId = InvestigatingCleanVerificationIssueId,
            VerificationMethod = "ManualQueryConfirmed",
            CleanPassed = true,
            EvidenceNotes = "BPJS VClaim validation endpoint returned HTTP 200 OK with zero tariff discrepancy. Tariff matches 12,500,000 IDR.",
            VerifiedBy = "ahmad.budi",
            TimestampUtc = verificationAt,
            IsSuperseded = false
        }, transaction);

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0666666-6666-6666-6666-666666666611"),
            IssueId = InvestigatingCleanVerificationIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0666666-6666-6666-6666-666666666612"),
            IssueId = InvestigatingCleanVerificationIssueId,
            ActivityType = "Taken",
            ActorName = "ahmad.budi",
            TimestampUtc = takenAt,
            Summary = "Issue taken and investigation started by ahmad.budi.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0666666-6666-6666-6666-666666666613"),
            IssueId = InvestigatingCleanVerificationIssueId,
            ActivityType = "RemediationRecorded",
            ActorName = "ahmad.budi",
            TimestampUtc = remediationAt,
            Summary = "Remediation action recorded [DataCorrection]: Re-grouped diagnosis code in casemix module per ICD-10 guide and generated updated SEP claim slip.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0666666-6666-6666-6666-666666666614"),
            IssueId = InvestigatingCleanVerificationIssueId,
            ActivityType = "VerificationRecorded",
            ActorName = "ahmad.budi",
            TimestampUtc = verificationAt,
            Summary = "Manual verification recorded: CleanPassed. BPJS VClaim validation endpoint returned HTTP 200 OK with zero tariff discrepancy. Tariff matches 12,500,000 IDR.",
            DetailsJson = (string?)null
        }, transaction);
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
        var discoveredAt = resolvedAtUtc.AddHours(-2);
        var takenAt = resolvedAtUtc.AddHours(-1.5);
        var remediationAt = resolvedAtUtc.AddMinutes(-30);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = ResolvedInspectionRunId,
            DetectorId = BillingDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 320,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        const string resolvedIssueSql = """
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

        await connection.ExecuteAsync(resolvedIssueSql, new
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
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)takenAt,
            ResolvedAtUtc = resolvedAtUtc
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = ResolvedOccurrenceId,
            IssueId = ResolvedIssueId,
            InspectionRunId = ResolvedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Billing",
            SourceTable = "acc_journal_entries",
            RecordKeys = "invoice_id=984321, batch_no=B20261120-1",
            DebitTotal = (decimal?)1500000.00m,
            CreditTotal = (decimal?)1450000.00m,
            VarianceDelta = (decimal?)50000.00m,
            TraceString = "Debit lines sum to 1,500,000 but credit lines sum to 1,450,000."
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = ResolvedRemediationId,
            IssueId = ResolvedIssueId,
            ActionCategory = "DataCorrection",
            SummaryDescription = "Adjusted tax withholding credit entry in acc_journal_entries to balance invoice journal.",
            PerformedBy = "ahmad.budi",
            TimestampUtc = remediationAt,
            IsSuperseded = false
        }, transaction);

        const string resolvedVerificationSql = """
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

        await connection.ExecuteAsync(resolvedVerificationSql, new
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

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0111111-1111-1111-1111-111111111111"),
            IssueId = ResolvedIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0111111-1111-1111-1111-111111111112"),
            IssueId = ResolvedIssueId,
            ActivityType = "Taken",
            ActorName = "ahmad.budi",
            TimestampUtc = takenAt,
            Summary = "Issue taken and investigation started by ahmad.budi.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0111111-1111-1111-1111-111111111113"),
            IssueId = ResolvedIssueId,
            ActivityType = "RemediationRecorded",
            ActorName = "ahmad.budi",
            TimestampUtc = remediationAt,
            Summary = "Remediation action recorded [DataCorrection]: Adjusted tax withholding credit entry in acc_journal_entries to balance invoice journal.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0111111-1111-1111-1111-111111111114"),
            IssueId = ResolvedIssueId,
            ActivityType = "VerificationRecorded",
            ActorName = "ahmad.budi",
            TimestampUtc = resolvedAtUtc,
            Summary = "Automated verification recorded: CleanPassed. Automated re-run verified debit and credit totals match with zero variance.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0111111-1111-1111-1111-111111111115"),
            IssueId = ResolvedIssueId,
            ActivityType = "Resolved",
            ActorName = "ahmad.budi",
            TimestampUtc = resolvedAtUtc,
            Summary = "Issue resolved after clean verification passed.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedDismissedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var dismissedCorrelationKey = $"{AdmissionDetectorId:D}:{DismissedEntityReference.ToUpperInvariant()}";
        var dismissedAtUtc = nowUtc.AddHours(-2);
        var discoveredAt = dismissedAtUtc.AddHours(-1);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = DismissedInspectionRunId,
            DetectorId = AdmissionDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 210,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
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
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)dismissedAtUtc,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DismissedOccurrenceId,
            IssueId = DismissedIssueId,
            InspectionRunId = DismissedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Inpatient",
            SourceTable = "bed_occupancies",
            RecordKeys = "bed_id=B-302, patient_id=P-8831",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Bed occupancy marked active despite discharge timestamp recorded."
        }, transaction);

        const string dismissalRationale = "Patient transfer delayed due to emergency medical protocol; manually cleared by nursing coordinator.";

        await connection.ExecuteAsync(DismissalSql, new
        {
            Id = DismissedRecordId,
            IssueId = DismissedIssueId,
            DismissalReason = "AcceptedOperationalException",
            JustificationRationale = dismissalRationale,
            DismissedBy = "siti.rahma",
            TimestampUtc = dismissedAtUtc,
            IsSuperseded = false
        }, transaction);

        // Activity Logs
        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0222222-2222-2222-2222-222222222211"),
            IssueId = DismissedIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0222222-2222-2222-2222-222222222212"),
            IssueId = DismissedIssueId,
            ActivityType = "Dismissed",
            ActorName = "siti.rahma",
            TimestampUtc = dismissedAtUtc,
            Summary = $"Issue dismissed (AcceptedOperationalException): {dismissalRationale}",
            DetailsJson = (string?)null
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
