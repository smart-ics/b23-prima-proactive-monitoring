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
    public static readonly Guid RetiredDetectorId = Guid.Parse("f8c1e245-6f7a-7b8c-9d0e-1f2a3b4c5d6e");

    // Failing Infrastructure Issue & Run
    public static readonly Guid ReplicationFailingIssueId = Guid.Parse("d0555555-5555-5555-5555-555555555591");
    public static readonly Guid ReplicationFailingRunId = Guid.Parse("d0555555-5555-5555-5555-555555555592");
    public static readonly Guid ReplicationFailingOccurrenceId = Guid.Parse("d0555555-5555-5555-5555-555555555593");

    // Silent Infrastructure Issue & Run
    public static readonly Guid AdmissionSilentIssueId = Guid.Parse("d0222222-2222-2222-2222-222222222291");
    public static readonly Guid AdmissionSilentRunId = Guid.Parse("d0222222-2222-2222-2222-222222222292");
    public static readonly Guid AdmissionSilentOccurrenceId = Guid.Parse("d0222222-2222-2222-2222-222222222293");

    // Retired Detector Active Issue (DEC-009)
    public static readonly Guid RetiredDetectorIssueId = Guid.Parse("d0888888-8888-8888-8888-888888888801");
    public static readonly Guid RetiredDetectorRunId = Guid.Parse("d0888888-8888-8888-8888-888888888802");
    public static readonly Guid RetiredDetectorOccurrenceId = Guid.Parse("d0888888-8888-8888-8888-888888888803");

    // Escalated Issue (EscalationLevel = 2, Severity = Critical)
    public static readonly Guid EscalatedIssueId = Guid.Parse("d0999999-9999-9999-9999-999999999901");
    public static readonly Guid EscalatedInspectionRunId = Guid.Parse("d0999999-9999-9999-9999-999999999902");
    public static readonly Guid EscalatedOccurrenceId = Guid.Parse("d0999999-9999-9999-9999-999999999903");

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

    // 8. Stage 4 Historical Reporting Cohorts (Resolved Today/Last Week, Dismissed Last Week, Reopened, Open)
    public static readonly Guid ResolvedTodayProcessIssueId = Guid.Parse("d0a10001-0000-0000-0000-000000000001");
    public static readonly Guid ResolvedLastWeekPharmacyIssueId = Guid.Parse("d0a10002-0000-0000-0000-000000000001");
    public static readonly Guid ResolvedLastWeekInfraIssueId = Guid.Parse("d0a10003-0000-0000-0000-000000000001");
    public static readonly Guid DismissedLastWeekIssueId = Guid.Parse("d0a20001-0000-0000-0000-000000000001");
    public static readonly Guid ReopenedTodayIssueId = Guid.Parse("d0a30001-0000-0000-0000-000000000001");
    public static readonly Guid ReopenedLastWeekIssueId = Guid.Parse("d0a30002-0000-0000-0000-000000000001");
    public static readonly Guid OpenHistoricalLabIssueId = Guid.Parse("d0a40001-0000-0000-0000-000000000001");

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

            // 1. Seed Detectors Portfolio (Active+Healthy, Active+Silent, Active+Failing, Paused, Retired)
            await SeedDetectorsAsync(connection, transaction, nowUtc);

            // 2. Seed Historical Inspection Runs (at least 32 runs per detector covering Passed, FindingDiscovered, ExecutionFailed)
            await SeedHistoricalRunsAsync(connection, transaction, nowUtc);

            // 3. Seed Failing Detector Infrastructure Issue (FAIL:{id})
            await SeedFailingDetectorInfrastructureIssueAsync(connection, transaction, nowUtc);

            // 4. Seed Silent Detector Infrastructure Issue (SILENT:{id})
            await SeedSilentDetectorInfrastructureIssueAsync(connection, transaction, nowUtc);

            // 5. Seed Retired Detector Active Issue (DEC-009)
            await SeedRetiredDetectorActiveIssueAsync(connection, transaction, nowUtc);

            // 6. Seed Escalated Issue (EscalationLevel = 2, Severity = Critical, SeverityEscalated activity logs)
            await SeedEscalatedIssueAsync(connection, transaction, nowUtc);

            // 7. Seed Open Issue (Unassigned)
            await SeedOpenIssueAsync(connection, transaction, nowUtc);

            // 8. Seed Acknowledged Issue (Assigned to siti.rahma with notes and audit trail)
            await SeedAcknowledgedIssueAsync(connection, transaction, nowUtc);

            // 9. Seed Investigating Issue (Assigned to ahmad.budi, remediation recorded, awaiting verification)
            await SeedInvestigatingAwaitingVerificationIssueAsync(connection, transaction, nowUtc);

            // 10. Seed Investigating Issue (Failed verification CleanPassed = false)
            await SeedInvestigatingFailedVerificationIssueAsync(connection, transaction, nowUtc);

            // 11. Seed Investigating Issue (Clean verification CleanPassed = true, ready for resolution)
            await SeedInvestigatingCleanVerificationIssueAsync(connection, transaction, nowUtc);

            // 12. Seed Resolved Issue (With clean verification, remediation, and activity logs)
            await SeedResolvedIssueAsync(connection, transaction, nowUtc, startOfTodayWibUtc);

            // 13. Seed Dismissed Issue (With valid dismissal justification BR-PRIMA-006 and activity logs)
            await SeedDismissedIssueAsync(connection, transaction, nowUtc);

            // 14. Seed Today WIB Inspection Runs & Historical Multi-Cohort Issues (Stage 4 Analytics)
            await SeedTodayInspectionRunsAsync(connection, transaction, nowUtc, startOfTodayWibUtc);
            await SeedHistoricalCohortIssuesAsync(connection, transaction, nowUtc, startOfTodayWibUtc);

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

    public static Guid CreateDeterministicRunId(Guid detectorId, int runIndex)
    {
        var bytes = detectorId.ToByteArray();
        bytes[0] = 0xEE;
        bytes[14] = (byte)((runIndex >> 8) & 0xFF);
        bytes[15] = (byte)(runIndex & 0xFF);
        return new Guid(bytes);
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
            ELSE
            BEGIN
                UPDATE dbo.Detectors
                SET Name = @Name,
                    Description = @Description,
                    Scope = @Scope,
                    TargetModule = @TargetModule,
                    ExpectedCadenceSeconds = @ExpectedCadenceSeconds,
                    WebhookUrl = @WebhookUrl,
                    AdminStatus = @AdminStatus,
                    HealthStatus = @HealthStatus,
                    LastRunAtUtc = @LastRunAtUtc,
                    LastHealthyHeartbeatUtc = @LastHealthyHeartbeatUtc
                WHERE Id = @Id;
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
                WebhookUrl = (string?)"https://probes.internal.hospital/api/detectors/billing",
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
                HealthStatus = DetectorHealth.Silent.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc.AddMinutes(-40),
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc.AddMinutes(-40),
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
                WebhookUrl = (string?)"https://probes.internal.hospital/api/detectors/replication",
                AdminStatus = AdminStatus.Active.ToString(),
                HealthStatus = DetectorHealth.Failing.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc.AddMinutes(-5),
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc.AddMinutes(-15),
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
                WebhookUrl = (string?)"https://probes.internal.hospital/api/detectors/laboratory",
                AdminStatus = AdminStatus.Paused.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc.AddHours(-12),
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc.AddHours(-12),
                CreatedAtUtc = nowUtc.AddDays(-30)
            },
            new
            {
                Id = RetiredDetectorId,
                Name = "Legacy Bed Tracking Bridge",
                Description = "Retired HL7 v2 ADT bed sensor feed superseded by integrated FHIR Encounter service.",
                Scope = MonitoringScope.ProcessIntegrity.ToString(),
                TargetModule = "Inpatient",
                ExpectedCadenceSeconds = 300,
                WebhookUrl = (string?)null,
                AdminStatus = AdminStatus.Retired.ToString(),
                HealthStatus = DetectorHealth.Healthy.ToString(),
                LastRunAtUtc = (DateTime?)nowUtc.AddDays(-5),
                LastHealthyHeartbeatUtc = (DateTime?)nowUtc.AddDays(-5),
                CreatedAtUtc = nowUtc.AddDays(-60)
            }
        };

        foreach (var detector in detectors)
        {
            await connection.ExecuteAsync(detectorSql, detector, transaction);
        }
    }

    private static async Task SeedHistoricalRunsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var detectors = new[]
        {
            (Id: BillingDetectorId, Module: "Billing", CadenceSeconds: 300, IsRetired: false),
            (Id: AdmissionDetectorId, Module: "Inpatient", CadenceSeconds: 600, IsRetired: false),
            (Id: PrescriptionDetectorId, Module: "Pharmacy", CadenceSeconds: 300, IsRetired: false),
            (Id: ReplicationDetectorId, Module: "Database", CadenceSeconds: 60, IsRetired: false),
            (Id: LaboratoryDetectorId, Module: "Laboratory", CadenceSeconds: 180, IsRetired: false),
            (Id: RetiredDetectorId, Module: "Inpatient", CadenceSeconds: 300, IsRetired: true)
        };

        foreach (var detector in detectors)
        {
            int totalRuns = (detector.Id == BillingDetectorId) ? 65 : 32;
            for (int i = 1; i <= totalRuns; i++)
            {
                var runId = CreateDeterministicRunId(detector.Id, i);

                DateTime runTime;
                if (detector.IsRetired)
                {
                    runTime = nowUtc.AddDays(-28).AddHours(i * 17);
                }
                else if (detector.Id == BillingDetectorId)
                {
                    runTime = nowUtc.AddDays(-29).AddHours(i * 10.5);
                }
                else
                {
                    runTime = nowUtc.AddDays(-29).AddHours(i * 15.5);
                }

                InspectionOutcome outcome;
                int durationMs;
                string? failureReason = null;

                if (detector.Id == BillingDetectorId)
                {
                    // High pass rate (> 90%) covering Passed, FindingDiscovered (from issues), and ExecutionFailed
                    if (i == 10)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 0;
                        failureReason = "Temporary socket timeout connecting to billing ledger database.";
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 120 + (i * 7) % 95;
                    }
                }
                else if (detector.Id == ReplicationDetectorId)
                {
                    // Active Failing detector: recent runs fail
                    if (i >= 27)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 30000;
                        failureReason = "Replication subscriber socket timeout on primary cluster node EMR-DB-PRIMARY.";
                    }
                    else if (i % 6 == 0)
                    {
                        outcome = InspectionOutcome.FindingDiscovered;
                        durationMs = 240;
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 95 + (i * 5) % 60;
                    }
                }
                else if (detector.Id == AdmissionDetectorId)
                {
                    if (i == 8 || i == 20)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 0;
                        failureReason = "HL7 interface parser exception on malformed ADT^A03 packet.";
                    }
                    else if (i % 5 == 0)
                    {
                        outcome = InspectionOutcome.FindingDiscovered;
                        durationMs = 310;
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 140 + (i * 8) % 80;
                    }
                }
                else if (detector.Id == LaboratoryDetectorId)
                {
                    if (i == 12)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 0;
                        failureReason = "Sysmex analyzer bridge returned HTTP 503 Service Unavailable.";
                    }
                    else if (i % 7 == 0)
                    {
                        outcome = InspectionOutcome.FindingDiscovered;
                        durationMs = 420;
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 180 + (i * 6) % 90;
                    }
                }
                else if (detector.Id == RetiredDetectorId)
                {
                    if (i >= 30)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 0;
                        failureReason = "Legacy ADT feed discontinued by hospital infrastructure modernization.";
                    }
                    else if (i % 5 == 0)
                    {
                        outcome = InspectionOutcome.FindingDiscovered;
                        durationMs = 290;
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 150 + (i * 9) % 70;
                    }
                }
                else
                {
                    // PrescriptionDetectorId
                    if (i == 15)
                    {
                        outcome = InspectionOutcome.ExecutionFailed;
                        durationMs = 0;
                        failureReason = "Pharmacy dispense log locked by nightly maintenance batch.";
                    }
                    else if (i % 6 == 0)
                    {
                        outcome = InspectionOutcome.FindingDiscovered;
                        durationMs = 330;
                    }
                    else
                    {
                        outcome = InspectionOutcome.Passed;
                        durationMs = 110 + (i * 7) % 75;
                    }
                }

                await connection.ExecuteAsync(RunSql, new
                {
                    Id = runId,
                    DetectorId = detector.Id,
                    ExecutionTimestampUtc = runTime,
                    TriggerSource = (i % 8 == 0) ? TriggerSource.Manual.ToString() : TriggerSource.Scheduled.ToString(),
                    Outcome = outcome.ToString(),
                    DurationMs = durationMs,
                    FailureReason = failureReason,
                    CreatedAtUtc = runTime
                }, transaction);
            }
        }
    }

    private static async Task SeedFailingDetectorInfrastructureIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var discoveredAt = nowUtc.AddMinutes(-5);
        var correlationKey = $"FAIL:{ReplicationDetectorId:D}";

        await connection.ExecuteAsync(RunSql, new
        {
            Id = ReplicationFailingRunId,
            DetectorId = ReplicationDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.ExecutionFailed.ToString(),
            DurationMs = 30000,
            FailureReason = "Replication subscriber socket timeout on primary cluster node EMR-DB-PRIMARY.",
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = ReplicationFailingIssueId,
            CorrelationKey = correlationKey,
            Title = "Infrastructure Failure: EMR Database Replication Monitor execution failed",
            DetectorId = ReplicationDetectorId,
            DetectorName = "EMR Database Replication Monitor",
            Scope = MonitoringScope.Infrastructure.ToString(),
            BaseSeverity = IssueSeverity.High.ToString(),
            CurrentSeverity = IssueSeverity.High.ToString(),
            SeverityRank = (byte)SeverityRank.High,
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
            Id = ReplicationFailingOccurrenceId,
            IssueId = ReplicationFailingIssueId,
            InspectionRunId = ReplicationFailingRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Database",
            SourceTable = "sys.dm_hadr_database_replica_states",
            RecordKeys = $"detector_id={ReplicationDetectorId}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Replication subscriber socket timeout on primary cluster node EMR-DB-PRIMARY."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0555555-5555-5555-5555-555555555594"),
            IssueId = ReplicationFailingIssueId,
            ActivityType = "Created",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Initial anomaly discovered: Infrastructure Failure: EMR Database Replication Monitor execution failed (High).",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedSilentDetectorInfrastructureIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var discoveredAt = nowUtc.AddMinutes(-20);
        var correlationKey = $"SILENT:{AdmissionDetectorId:D}";

        await connection.ExecuteAsync(RunSql, new
        {
            Id = AdmissionSilentRunId,
            DetectorId = AdmissionDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.ExecutionFailed.ToString(),
            DurationMs = 0,
            FailureReason = "Detector 'Patient Admission Discharge Sync' missed heartbeat. Cadence: 600s. Threshold: 1200s.",
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = AdmissionSilentIssueId,
            CorrelationKey = correlationKey,
            Title = "Silent Detector: Patient Admission Discharge Sync has missed cadence",
            DetectorId = AdmissionDetectorId,
            DetectorName = "Patient Admission Discharge Sync",
            Scope = MonitoringScope.Infrastructure.ToString(),
            BaseSeverity = IssueSeverity.High.ToString(),
            CurrentSeverity = IssueSeverity.High.ToString(),
            SeverityRank = (byte)SeverityRank.High,
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
            Id = AdmissionSilentOccurrenceId,
            IssueId = AdmissionSilentIssueId,
            InspectionRunId = AdmissionSilentRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Inpatient",
            SourceTable = "Detectors",
            RecordKeys = $"detector_id={AdmissionDetectorId}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Detector 'Patient Admission Discharge Sync' missed heartbeat. Cadence: 600s. Threshold: 1200s."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0222222-2222-2222-2222-222222222294"),
            IssueId = AdmissionSilentIssueId,
            ActivityType = "Created",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Infrastructure issue auto-synthesized due to missed cadence.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedRetiredDetectorActiveIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var discoveredAt = nowUtc.AddDays(-6);
        var acknowledgedAt = nowUtc.AddDays(-5).AddHours(1);
        var correlationKey = $"{RetiredDetectorId:D}:LEGACY-ADT-4091";

        await connection.ExecuteAsync(RunSql, new
        {
            Id = RetiredDetectorRunId,
            DetectorId = RetiredDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 280,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = RetiredDetectorIssueId,
            CorrelationKey = correlationKey,
            Title = "Unmatched Discharge Encounter: Room 402 Bed B",
            DetectorId = RetiredDetectorId,
            DetectorName = "Legacy Bed Tracking Bridge",
            Scope = MonitoringScope.ProcessIntegrity.ToString(),
            BaseSeverity = IssueSeverity.Medium.ToString(),
            CurrentSeverity = IssueSeverity.Medium.ToString(),
            SeverityRank = (byte)SeverityRank.Medium,
            Status = IssueStatus.Investigating.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 2,
            CurrentAssignee = "siti.rahma",
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = nowUtc.AddDays(-5),
            AcknowledgedAtUtc = (DateTime?)acknowledgedAt,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = RetiredDetectorOccurrenceId,
            IssueId = RetiredDetectorIssueId,
            InspectionRunId = RetiredDetectorRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Inpatient",
            SourceTable = "bed_adt_events",
            RecordKeys = "bed_id=B402-B, encounter_id=ENC-2026-9041",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Discharge event recorded in legacy ADT stream with missing HIS Encounter close timestamp."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0888888-8888-8888-8888-888888888811"),
            IssueId = RetiredDetectorIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0888888-8888-8888-8888-888888888812"),
            IssueId = RetiredDetectorIssueId,
            ActivityType = "Taken",
            ActorName = "siti.rahma",
            TimestampUtc = acknowledgedAt,
            Summary = "Issue taken and investigation started by siti.rahma.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedEscalatedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc)
    {
        var correlationKey = $"{PrescriptionDetectorId:D}:RX-ESCALATED-099";
        var discoveredAt = nowUtc.AddHours(-18);
        var firstEscalationAt = nowUtc.AddHours(-14);
        var secondEscalationAt = nowUtc.AddHours(-6);
        var lastOccurrenceAt = nowUtc.AddMinutes(-30);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = EscalatedInspectionRunId,
            DetectorId = PrescriptionDetectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 350,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = EscalatedIssueId,
            CorrelationKey = correlationKey,
            Title = "Narcotic Dispense Discrepancy: Fentanyl 50mcg Batch RX-ESCALATED-099",
            DetectorId = PrescriptionDetectorId,
            DetectorName = "Prescription Stock Deductor",
            Scope = MonitoringScope.DataSync.ToString(),
            BaseSeverity = IssueSeverity.Medium.ToString(),
            CurrentSeverity = IssueSeverity.Critical.ToString(),
            SeverityRank = (byte)SeverityRank.Critical,
            Status = IssueStatus.Open.ToString(),
            EscalationLevel = 2,
            OccurrenceCount = 6,
            CurrentAssignee = (string?)null,
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = lastOccurrenceAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = EscalatedOccurrenceId,
            IssueId = EscalatedIssueId,
            InspectionRunId = EscalatedInspectionRunId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = "Pharmacy",
            SourceTable = "pharmacy_dispense_log",
            RecordKeys = "item_code=MED-FNT-50, batch_no=RX-ESCALATED-099",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)12.00m,
            TraceString = "Controlled substance dispense journal has 12 ampoules discrepancy against automated cabinet inventory log."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0999999-9999-9999-9999-999999999911"),
            IssueId = EscalatedIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0999999-9999-9999-9999-999999999912"),
            IssueId = EscalatedIssueId,
            ActivityType = "SeverityEscalated",
            ActorName = "System",
            TimestampUtc = firstEscalationAt,
            Summary = "SLA breach: Issue unacknowledged for > 4h at level 0. Severity stepped to High.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = Guid.Parse("d0999999-9999-9999-9999-999999999913"),
            IssueId = EscalatedIssueId,
            ActivityType = "SeverityEscalated",
            ActorName = "System",
            TimestampUtc = secondEscalationAt,
            Summary = "Recurrence breach: Issue reached 5 occurrences. Severity stepped to Critical.",
            DetailsJson = (string?)null
        }, transaction);
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

    private static async Task SeedTodayInspectionRunsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc,
        DateTime startOfTodayWibUtc)
    {
        var anchorTodayUtc = nowUtc > startOfTodayWibUtc.AddMinutes(30)
            ? nowUtc.AddMinutes(-5)
            : startOfTodayWibUtc.AddMinutes(30);

        var todayRuns = new[]
        {
            (RunIndex: 901, DetectorId: BillingDetectorId, MinutesOffset: -20, Outcome: InspectionOutcome.Passed, DurationMs: 135, Failure: (string?)null),
            (RunIndex: 902, DetectorId: BillingDetectorId, MinutesOffset: -15, Outcome: InspectionOutcome.Passed, DurationMs: 142, Failure: (string?)null),
            (RunIndex: 903, DetectorId: BillingDetectorId, MinutesOffset: -10, Outcome: InspectionOutcome.Passed, DurationMs: 128, Failure: (string?)null),
            (RunIndex: 904, DetectorId: BillingDetectorId, MinutesOffset: -5, Outcome: InspectionOutcome.Passed, DurationMs: 131, Failure: (string?)null),
            (RunIndex: 905, DetectorId: PrescriptionDetectorId, MinutesOffset: -18, Outcome: InspectionOutcome.Passed, DurationMs: 155, Failure: (string?)null),
            (RunIndex: 906, DetectorId: PrescriptionDetectorId, MinutesOffset: -12, Outcome: InspectionOutcome.Passed, DurationMs: 162, Failure: (string?)null),
            (RunIndex: 907, DetectorId: PrescriptionDetectorId, MinutesOffset: -6, Outcome: InspectionOutcome.ExecutionFailed, DurationMs: 0, Failure: "Transient pharmacy stock sync timeout during peak dispense window.")
        };

        foreach (var r in todayRuns)
        {
            var runTime = anchorTodayUtc.AddMinutes(r.MinutesOffset);
            if (runTime < startOfTodayWibUtc)
            {
                runTime = startOfTodayWibUtc.AddMinutes(Math.Abs(r.MinutesOffset));
            }

            await connection.ExecuteAsync(RunSql, new
            {
                Id = CreateDeterministicRunId(r.DetectorId, r.RunIndex),
                DetectorId = r.DetectorId,
                ExecutionTimestampUtc = runTime,
                TriggerSource = TriggerSource.Scheduled.ToString(),
                Outcome = r.Outcome.ToString(),
                DurationMs = r.DurationMs,
                FailureReason = r.Failure,
                CreatedAtUtc = runTime
            }, transaction);
        }
    }

    private static async Task SeedHistoricalCohortIssuesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        DateTime nowUtc,
        DateTime startOfTodayWibUtc)
    {
        var todayCohortUtc = nowUtc > startOfTodayWibUtc ? nowUtc : startOfTodayWibUtc.AddMinutes(30);

        // 1. Resolved Today (ProcessIntegrity / Laboratory / Medium)
        await SeedCohortResolvedIssueAsync(
            connection,
            transaction,
            issueId: ResolvedTodayProcessIssueId,
            entityRef: "LIS-2026-RESOLVED-TODAY",
            title: "Delayed Lab Result Dispatch: LIS-2026-RESOLVED-TODAY",
            detectorId: LaboratoryDetectorId,
            detectorName: "Laboratory Result Dispatch Monitor",
            scope: MonitoringScope.ProcessIntegrity,
            severity: IssueSeverity.Medium,
            severityRank: SeverityRank.Medium,
            assignee: "siti.rahma",
            discoveredAt: todayCohortUtc.AddHours(-3),
            acknowledgedAt: todayCohortUtc.AddHours(-2.5),
            resolvedAt: todayCohortUtc.AddMinutes(-10),
            module: "Laboratory",
            sourceTable: "lis_dispatch_queue",
            remediationSummary: "Flushed stalled HL7 ORU^R01 outbound queue and verified delivery.",
            keepResolvedAtSyncedToToday: true);

        // 2. Resolved Last Week (DataIntegrity / Prescription / Medium)
        await SeedCohortResolvedIssueAsync(
            connection,
            transaction,
            issueId: ResolvedLastWeekPharmacyIssueId,
            entityRef: "RX-2026-RESOLVED-LW1",
            title: "Negative Stock Ledger Drift: RX-2026-RESOLVED-LW1",
            detectorId: PrescriptionDetectorId,
            detectorName: "Prescription Dispense Stock Checker",
            scope: MonitoringScope.DataIntegrity,
            severity: IssueSeverity.Medium,
            severityRank: SeverityRank.Medium,
            assignee: "ahmad.budi",
            discoveredAt: nowUtc.AddDays(-7).AddHours(-4),
            acknowledgedAt: nowUtc.AddDays(-7).AddHours(-3),
            resolvedAt: nowUtc.AddDays(-7),
            module: "Pharmacy",
            sourceTable: "phm_stock_ledger",
            remediationSummary: "Reconciled dispense batch reversal and restored non-negative stock balance.",
            keepResolvedAtSyncedToToday: false);

        // 3. Resolved Last Week (Infrastructure / Replication / Critical)
        await SeedCohortResolvedIssueAsync(
            connection,
            transaction,
            issueId: ResolvedLastWeekInfraIssueId,
            entityRef: "REPL-2026-RESOLVED-LW2",
            title: "Replication Lag Spike Above 120s: REPL-2026-RESOLVED-LW2",
            detectorId: ReplicationDetectorId,
            detectorName: "EMR Database Replication Lag Monitor",
            scope: MonitoringScope.Infrastructure,
            severity: IssueSeverity.Critical,
            severityRank: SeverityRank.Critical,
            assignee: "ahmad.budi",
            discoveredAt: nowUtc.AddDays(-10).AddHours(-5),
            acknowledgedAt: nowUtc.AddDays(-10).AddHours(-4.5),
            resolvedAt: nowUtc.AddDays(-10),
            module: "Database",
            sourceTable: "sys.dm_hadr_database_replica_states",
            remediationSummary: "Cleared long-running reporting transaction blocking redo queue on secondary.",
            keepResolvedAtSyncedToToday: false);

        // 4. Dismissed Last Week (DataIntegrity / Billing / Low)
        await SeedCohortDismissedIssueAsync(
            connection,
            transaction,
            issueId: DismissedLastWeekIssueId,
            entityRef: "BILL-2026-DISMISSED-LW1",
            title: "Rounding Penny Variance on BPJS Claim: BILL-2026-DISMISSED-LW1",
            detectorId: BillingDetectorId,
            detectorName: "Billing Journal Balance Checker",
            scope: MonitoringScope.DataIntegrity,
            severity: IssueSeverity.Low,
            severityRank: SeverityRank.Low,
            assignee: "siti.rahma",
            discoveredAt: nowUtc.AddDays(-9).AddHours(-2),
            dismissedAt: nowUtc.AddDays(-9),
            module: "Billing",
            sourceTable: "acc_journal_entries",
            rationale: "Verified 1 IDR rounding difference is an expected BPJS tariff ceiling adjustment.");

        // 5. Reopened Today (DataIntegrity / Billing / High)
        await SeedCohortReopenedIssueAsync(
            connection,
            transaction,
            issueId: ReopenedTodayIssueId,
            entityRef: "BILL-2026-REOPENED-TODAY",
            title: "Recurring Journal Imbalance: BILL-2026-REOPENED-TODAY",
            detectorId: BillingDetectorId,
            detectorName: "Billing Journal Balance Checker",
            scope: MonitoringScope.DataIntegrity,
            severity: IssueSeverity.High,
            severityRank: SeverityRank.High,
            discoveredAt: nowUtc.AddDays(-3),
            reopenedAt: todayCohortUtc.AddMinutes(-15),
            module: "Billing",
            sourceTable: "acc_journal_entries");

        // 6. Reopened Last Week (ProcessIntegrity / Laboratory / Medium)
        await SeedCohortReopenedIssueAsync(
            connection,
            transaction,
            issueId: ReopenedLastWeekIssueId,
            entityRef: "LIS-2026-REOPENED-LW1",
            title: "Recurring Specimen Barcode Mismatch: LIS-2026-REOPENED-LW1",
            detectorId: LaboratoryDetectorId,
            detectorName: "Laboratory Result Dispatch Monitor",
            scope: MonitoringScope.ProcessIntegrity,
            severity: IssueSeverity.Medium,
            severityRank: SeverityRank.Medium,
            discoveredAt: nowUtc.AddDays(-12),
            reopenedAt: nowUtc.AddDays(-6),
            module: "Laboratory",
            sourceTable: "lis_specimen_orders");

        // 7. Open Historical Issue (ProcessIntegrity / Laboratory / Low)
        var openCreatedAt = nowUtc.AddDays(-5);
        var openRunId = DeriveCohortGuid(OpenHistoricalLabIssueId, 0x02);
        await connection.ExecuteAsync(RunSql, new
        {
            Id = openRunId,
            DetectorId = LaboratoryDetectorId,
            ExecutionTimestampUtc = openCreatedAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 275,
            FailureReason = (string?)null,
            CreatedAtUtc = openCreatedAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = OpenHistoricalLabIssueId,
            CorrelationKey = $"{LaboratoryDetectorId:D}:LIS-2026-OPEN-HIST1",
            Title = "Minor Specimen Turnaround Delay: LIS-2026-OPEN-HIST1",
            DetectorId = LaboratoryDetectorId,
            DetectorName = "Laboratory Result Dispatch Monitor",
            Scope = MonitoringScope.ProcessIntegrity.ToString(),
            BaseSeverity = IssueSeverity.Low.ToString(),
            CurrentSeverity = IssueSeverity.Low.ToString(),
            SeverityRank = (byte)SeverityRank.Low,
            Status = IssueStatus.Open.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = (string?)null,
            CreatedAtUtc = openCreatedAt,
            LastOccurrenceAtUtc = openCreatedAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DeriveCohortGuid(OpenHistoricalLabIssueId, 0x03),
            IssueId = OpenHistoricalLabIssueId,
            InspectionRunId = openRunId,
            OccurrenceNumber = 1,
            TimestampUtc = openCreatedAt,
            Module = "Laboratory",
            SourceTable = "lis_dispatch_queue",
            RecordKeys = "specimen_id=SP-2026-4401",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = "Specimen result dispatch exceeded 15-minute SLA threshold."
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = DeriveCohortGuid(OpenHistoricalLabIssueId, 0x11),
            IssueId = OpenHistoricalLabIssueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = openCreatedAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedCohortResolvedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid issueId,
        string entityRef,
        string title,
        Guid detectorId,
        string detectorName,
        MonitoringScope scope,
        IssueSeverity severity,
        byte severityRank,
        string assignee,
        DateTime discoveredAt,
        DateTime acknowledgedAt,
        DateTime resolvedAt,
        string module,
        string sourceTable,
        string remediationSummary,
        bool keepResolvedAtSyncedToToday)
    {
        var runId = DeriveCohortGuid(issueId, 0x02);
        var correlationKey = $"{detectorId:D}:{entityRef.ToUpperInvariant()}";

        await connection.ExecuteAsync(RunSql, new
        {
            Id = runId,
            DetectorId = detectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 260,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        var sql = keepResolvedAtSyncedToToday
            ? """
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
                  UPDATE dbo.Issues
                  SET ResolvedAtUtc = @ResolvedAtUtc
                  WHERE (Id = @Id OR CorrelationKey = @CorrelationKey) AND Status = 'Resolved';
              END
              """
            : IssueSql;

        await connection.ExecuteAsync(sql, new
        {
            Id = issueId,
            CorrelationKey = correlationKey,
            Title = title,
            DetectorId = detectorId,
            DetectorName = detectorName,
            Scope = scope.ToString(),
            BaseSeverity = severity.ToString(),
            CurrentSeverity = severity.ToString(),
            SeverityRank = (byte)severityRank,
            Status = IssueStatus.Resolved.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = assignee,
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)acknowledgedAt,
            ResolvedAtUtc = (DateTime?)resolvedAt,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x03),
            IssueId = issueId,
            InspectionRunId = runId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = module,
            SourceTable = sourceTable,
            RecordKeys = $"ref={entityRef}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = $"Historical cohort finding detected for {entityRef}."
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x04),
            IssueId = issueId,
            ActionCategory = "DataCorrection",
            SummaryDescription = remediationSummary,
            PerformedBy = assignee,
            TimestampUtc = resolvedAt.AddMinutes(-20),
            IsSuperseded = false
        }, transaction);

        await connection.ExecuteAsync(VerificationSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x05),
            IssueId = issueId,
            VerificationMethod = "AutomatedReRun",
            CleanPassed = true,
            EvidenceNotes = "Automated re-run confirmed zero remaining anomaly.",
            VerifiedBy = assignee,
            TimestampUtc = resolvedAt,
            IsSuperseded = false
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x11),
            IssueId = issueId,
            ActivityType = "IssueDiscovered",
            ActorName = "System",
            TimestampUtc = discoveredAt,
            Summary = "Issue discovered by automated inspection run.",
            DetailsJson = (string?)null
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x12),
            IssueId = issueId,
            ActivityType = "Resolved",
            ActorName = assignee,
            TimestampUtc = resolvedAt,
            Summary = "Issue resolved after clean verification passed.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedCohortDismissedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid issueId,
        string entityRef,
        string title,
        Guid detectorId,
        string detectorName,
        MonitoringScope scope,
        IssueSeverity severity,
        byte severityRank,
        string assignee,
        DateTime discoveredAt,
        DateTime dismissedAt,
        string module,
        string sourceTable,
        string rationale)
    {
        var runId = DeriveCohortGuid(issueId, 0x02);
        await connection.ExecuteAsync(RunSql, new
        {
            Id = runId,
            DetectorId = detectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 190,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = issueId,
            CorrelationKey = $"{detectorId:D}:{entityRef.ToUpperInvariant()}",
            Title = title,
            DetectorId = detectorId,
            DetectorName = detectorName,
            Scope = scope.ToString(),
            BaseSeverity = severity.ToString(),
            CurrentSeverity = severity.ToString(),
            SeverityRank = (byte)severityRank,
            Status = IssueStatus.Dismissed.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 1,
            CurrentAssignee = assignee,
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = discoveredAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)dismissedAt,
            ReopenedAtUtc = (DateTime?)null
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x03),
            IssueId = issueId,
            InspectionRunId = runId,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = module,
            SourceTable = sourceTable,
            RecordKeys = $"ref={entityRef}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = $"Historical dismissed finding for {entityRef}."
        }, transaction);

        await connection.ExecuteAsync(DismissalSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x04),
            IssueId = issueId,
            DismissalReason = "AcceptedOperationalException",
            JustificationRationale = rationale,
            DismissedBy = assignee,
            TimestampUtc = dismissedAt,
            IsSuperseded = false
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x11),
            IssueId = issueId,
            ActivityType = "Dismissed",
            ActorName = assignee,
            TimestampUtc = dismissedAt,
            Summary = $"Issue dismissed (AcceptedOperationalException): {rationale}",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static async Task SeedCohortReopenedIssueAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid issueId,
        string entityRef,
        string title,
        Guid detectorId,
        string detectorName,
        MonitoringScope scope,
        IssueSeverity severity,
        byte severityRank,
        DateTime discoveredAt,
        DateTime reopenedAt,
        string module,
        string sourceTable)
    {
        var run1Id = DeriveCohortGuid(issueId, 0x02);
        var run2Id = DeriveCohortGuid(issueId, 0x06);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = run1Id,
            DetectorId = detectorId,
            ExecutionTimestampUtc = discoveredAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 240,
            FailureReason = (string?)null,
            CreatedAtUtc = discoveredAt
        }, transaction);

        await connection.ExecuteAsync(RunSql, new
        {
            Id = run2Id,
            DetectorId = detectorId,
            ExecutionTimestampUtc = reopenedAt,
            TriggerSource = TriggerSource.Scheduled.ToString(),
            Outcome = InspectionOutcome.FindingDiscovered.ToString(),
            DurationMs = 250,
            FailureReason = (string?)null,
            CreatedAtUtc = reopenedAt
        }, transaction);

        await connection.ExecuteAsync(IssueSql, new
        {
            Id = issueId,
            CorrelationKey = $"{detectorId:D}:{entityRef.ToUpperInvariant()}",
            Title = title,
            DetectorId = detectorId,
            DetectorName = detectorName,
            Scope = scope.ToString(),
            BaseSeverity = severity.ToString(),
            CurrentSeverity = severity.ToString(),
            SeverityRank = (byte)severityRank,
            Status = IssueStatus.Reopened.ToString(),
            EscalationLevel = 0,
            OccurrenceCount = 2,
            CurrentAssignee = (string?)null,
            CreatedAtUtc = discoveredAt,
            LastOccurrenceAtUtc = reopenedAt,
            AcknowledgedAtUtc = (DateTime?)null,
            ResolvedAtUtc = (DateTime?)null,
            DismissedAtUtc = (DateTime?)null,
            ReopenedAtUtc = (DateTime?)reopenedAt
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x03),
            IssueId = issueId,
            InspectionRunId = run1Id,
            OccurrenceNumber = 1,
            TimestampUtc = discoveredAt,
            Module = module,
            SourceTable = sourceTable,
            RecordKeys = $"ref={entityRef}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = $"Initial occurrence for {entityRef}."
        }, transaction);

        await connection.ExecuteAsync(OccurrenceSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x07),
            IssueId = issueId,
            InspectionRunId = run2Id,
            OccurrenceNumber = 2,
            TimestampUtc = reopenedAt,
            Module = module,
            SourceTable = sourceTable,
            RecordKeys = $"ref={entityRef}",
            DebitTotal = (decimal?)null,
            CreditTotal = (decimal?)null,
            VarianceDelta = (decimal?)null,
            TraceString = $"Recurring occurrence triggering Reopened transition for {entityRef}."
        }, transaction);

        await connection.ExecuteAsync(RemediationSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x04),
            IssueId = issueId,
            ActionCategory = "DataCorrection",
            SummaryDescription = "Initial remediation applied prior to recurrence.",
            PerformedBy = "ahmad.budi",
            TimestampUtc = discoveredAt.AddHours(2),
            IsSuperseded = true
        }, transaction);

        await connection.ExecuteAsync(VerificationSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x05),
            IssueId = issueId,
            VerificationMethod = "AutomatedReRun",
            CleanPassed = true,
            EvidenceNotes = "Prior verification superseded when issue recurred.",
            VerifiedBy = "ahmad.budi",
            TimestampUtc = discoveredAt.AddHours(3),
            IsSuperseded = true
        }, transaction);

        await connection.ExecuteAsync(ActivityLogSql, new
        {
            Id = DeriveCohortGuid(issueId, 0x11),
            IssueId = issueId,
            ActivityType = "Reopened",
            ActorName = "System",
            TimestampUtc = reopenedAt,
            Summary = "Issue automatically reopened after recurring finding was detected.",
            DetailsJson = (string?)null
        }, transaction);
    }

    private static Guid DeriveCohortGuid(Guid baseId, byte suffixByte)
    {
        var bytes = baseId.ToByteArray();
        bytes[15] = suffixByte;
        return new Guid(bytes);
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
