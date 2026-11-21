using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetDetectorList;
using PrIMA.Api.Features.Detectors.GetDetectorSummary;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Seeding;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DevelopmentDemoDataSeederTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly ISqlConnectionFactory _connectionFactory;

    public DevelopmentDemoDataSeederTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _connectionFactory = new DefaultSqlConnectionFactory(PrIMAApiFactory.TestConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "PrIMA.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class DefaultSqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;
        public DefaultSqlConnectionFactory(string connectionString) => _connectionString = connectionString;
        public IDbConnection CreateConnection() => new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
    }

    [Fact]
    public async Task SeedAsync_WhenSeedDemoDataIsFalse_DoesNotSeedAnyData()
    {
        // Arrange
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedDemoData"] = "false"
            })
            .Build();

        var seeder = new DevelopmentDemoDataSeeder(env, _connectionFactory, config, NullLogger<DevelopmentDemoDataSeeder>.Instance);

        // Act
        await seeder.SeedAsync();

        // Assert
        var detectorCount = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Detectors;");
        var issueCount = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Issues;");

        detectorCount.Should().Be(0);
        issueCount.Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsNotDevelopment_DoesNotSeedAnyData()
    {
        // Arrange
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedDemoData"] = "true"
            })
            .Build();

        var seeder = new DevelopmentDemoDataSeeder(env, _connectionFactory, config, NullLogger<DevelopmentDemoDataSeeder>.Instance);

        // Act
        await seeder.SeedAsync();

        // Assert
        var detectorCount = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Detectors;");
        var issueCount = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Issues;");

        detectorCount.Should().Be(0);
        issueCount.Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_WhenEnabled_IdempotentlySeedsAllStage2StatesAndAuditTrails()
    {
        // Arrange
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedDemoData"] = "true",
                ["OperationalTimeZone"] = "Asia/Jakarta"
            })
            .Build();

        var seeder = new DevelopmentDemoDataSeeder(env, _connectionFactory, config, NullLogger<DevelopmentDemoDataSeeder>.Instance);

        // Act: Run seed for the first time
        await seeder.SeedAsync();

        // Assert: 1. Detectors
        var detectors = (await _factory.QuerySqlAsync<Guid>("SELECT Id FROM dbo.Detectors")).ToList();
        detectors.Should().Contain(new[]
        {
            DevelopmentDemoDataSeeder.BillingDetectorId,
            DevelopmentDemoDataSeeder.AdmissionDetectorId,
            DevelopmentDemoDataSeeder.PrescriptionDetectorId,
            DevelopmentDemoDataSeeder.ReplicationDetectorId,
            DevelopmentDemoDataSeeder.LaboratoryDetectorId,
            DevelopmentDemoDataSeeder.RetiredDetectorId
        });

        // Stage 3 Detector Combinations Assertions
        // 1.1 Active + Healthy with configured WebhookUrl and high pass rate
        var billingDetector = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.BillingDetectorId });
        ((string)billingDetector!.AdminStatus).Should().Be("Active");
        ((string)billingDetector.HealthStatus).Should().Be("Healthy");
        ((string)billingDetector.WebhookUrl).Should().NotBeNullOrEmpty();

        // 1.2 Active + Failing with recent ExecutionFailed run and FAIL:{id} issue
        var repDetector = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.ReplicationDetectorId });
        ((string)repDetector!.AdminStatus).Should().Be("Active");
        ((string)repDetector.HealthStatus).Should().Be("Failing");

        var repFailIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.ReplicationFailingIssueId });
        ((object?)repFailIssue).Should().NotBeNull();
        ((string)repFailIssue!.CorrelationKey).Should().Be($"FAIL:{DevelopmentDemoDataSeeder.ReplicationDetectorId}");
        ((string)repFailIssue.Status).Should().Be("Open");

        // 1.3 Active + Silent with missed heartbeat beyond 2x cadence and SILENT:{id} issue
        var admDetector = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.AdmissionDetectorId });
        ((string)admDetector!.AdminStatus).Should().Be("Active");
        ((string)admDetector.HealthStatus).Should().Be("Silent");

        var admSilentIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.AdmissionSilentIssueId });
        ((object?)admSilentIssue).Should().NotBeNull();
        ((string)admSilentIssue!.CorrelationKey).Should().Be($"SILENT:{DevelopmentDemoDataSeeder.AdmissionDetectorId}");
        ((string)admSilentIssue.Status).Should().Be("Open");

        // 1.4 Paused detector
        var labDetector = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.LaboratoryDetectorId });
        ((string)labDetector!.AdminStatus).Should().Be("Paused");

        // 1.5 Retired detector with historical runs and active linked issue (DEC-009)
        var retDetector = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.RetiredDetectorId });
        ((string)retDetector!.AdminStatus).Should().Be("Retired");

        var retActiveIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.RetiredDetectorIssueId });
        ((object?)retActiveIssue).Should().NotBeNull();
        ((string)retActiveIssue!.Status).Should().Be("Investigating");
        ((Guid)retActiveIssue.DetectorId).Should().Be(DevelopmentDemoDataSeeder.RetiredDetectorId);

        // 1.6 Historical runs: at least 30 runs per detector covering Passed, FindingDiscovered, and ExecutionFailed outcomes
        foreach (var detId in new[]
        {
            DevelopmentDemoDataSeeder.BillingDetectorId,
            DevelopmentDemoDataSeeder.AdmissionDetectorId,
            DevelopmentDemoDataSeeder.PrescriptionDetectorId,
            DevelopmentDemoDataSeeder.ReplicationDetectorId,
            DevelopmentDemoDataSeeder.LaboratoryDetectorId,
            DevelopmentDemoDataSeeder.RetiredDetectorId
        })
        {
            var runCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
                "SELECT COUNT(1) FROM dbo.InspectionRuns WHERE DetectorId = @Id",
                new { Id = detId });
            runCount.Should().BeGreaterOrEqualTo(30);

            var outcomes = (await _factory.QuerySqlAsync<string>(
                "SELECT DISTINCT Outcome FROM dbo.InspectionRuns WHERE DetectorId = @Id",
                new { Id = detId })).ToList();
            outcomes.Should().Contain(new[] { "Passed", "FindingDiscovered", "ExecutionFailed" });
        }

        // 1.7 Escalated issue: EscalationLevel >= 1, CurrentSeverity High or Critical, with SeverityEscalated activity logs
        var escIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.EscalatedIssueId });
        ((object?)escIssue).Should().NotBeNull();
        ((int)escIssue!.EscalationLevel).Should().BeGreaterOrEqualTo(1);
        new[] { "High", "Critical" }.Should().Contain((string)escIssue.CurrentSeverity);

        var escLogs = (await _factory.QuerySqlAsync<string>(
            "SELECT ActivityType FROM dbo.IssueActivityLogs WHERE IssueId = @Id AND ActivityType = 'SeverityEscalated'",
            new { Id = DevelopmentDemoDataSeeder.EscalatedIssueId })).ToList();
        escLogs.Should().NotBeEmpty();

        // Assert: 2. Acknowledged issue assigned to siti.rahma with notes and activity logs
        var ackIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.AcknowledgedIssueId });
        ((object?)ackIssue).Should().NotBeNull();
        string ackStatus = (string)ackIssue!.Status;
        string ackAssignee = (string)ackIssue.CurrentAssignee;
        ackStatus.Should().Be("Acknowledged");
        ackAssignee.Should().Be("siti.rahma");

        var ackNotesCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.InvestigationNotes WHERE IssueId = @Id",
            new { Id = DevelopmentDemoDataSeeder.AcknowledgedIssueId });
        ackNotesCount.Should().BeGreaterOrEqualTo(1);

        var ackLogs = (await _factory.QuerySqlAsync<string>(
            "SELECT ActivityType FROM dbo.IssueActivityLogs WHERE IssueId = @Id ORDER BY TimestampUtc ASC",
            new { Id = DevelopmentDemoDataSeeder.AcknowledgedIssueId })).ToList();
        ackLogs.Should().Contain(new[] { "IssueDiscovered", "Assigned", "Acknowledged", "NoteAdded" });

        // Assert: 3. Investigating issue assigned to ahmad.budi, awaiting verification
        var awaitingIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingAwaitingVerificationIssueId });
        ((object?)awaitingIssue).Should().NotBeNull();
        string awaitingStatus = (string)awaitingIssue!.Status;
        string awaitingAssignee = (string)awaitingIssue.CurrentAssignee;
        awaitingStatus.Should().Be("Investigating");
        awaitingAssignee.Should().Be("ahmad.budi");

        var awaitingRemediation = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.RemediationRecords WHERE IssueId = @Id AND IsSuperseded = 0",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingAwaitingVerificationIssueId });
        ((object?)awaitingRemediation).Should().NotBeNull();
        string actionCat = (string)awaitingRemediation!.ActionCategory;
        actionCat.Should().Be("ServiceRestart");

        var awaitingVerificationsCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(
            "SELECT COUNT(1) FROM dbo.VerificationRecords WHERE IssueId = @Id",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingAwaitingVerificationIssueId });
        awaitingVerificationsCount.Should().Be(0);

        // Assert: 4. Investigating issue with failed verification (CleanPassed = false)
        var failedVerifIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingFailedVerificationIssueId });
        ((object?)failedVerifIssue).Should().NotBeNull();
        string failedStatus = (string)failedVerifIssue!.Status;
        failedStatus.Should().Be("Investigating");

        var failedVerifRecord = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @Id AND IsSuperseded = 0",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingFailedVerificationIssueId });
        ((object?)failedVerifRecord).Should().NotBeNull();
        bool failedCleanPassed = (bool)failedVerifRecord!.CleanPassed;
        failedCleanPassed.Should().BeFalse();

        // Assert: 5. Investigating issue with clean verification (CleanPassed = true), ready for resolution
        var cleanVerifIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingCleanVerificationIssueId });
        ((object?)cleanVerifIssue).Should().NotBeNull();
        string cleanStatus = (string)cleanVerifIssue!.Status;
        cleanStatus.Should().Be("Investigating");

        var cleanVerifRecord = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @Id AND IsSuperseded = 0",
            new { Id = DevelopmentDemoDataSeeder.InvestigatingCleanVerificationIssueId });
        ((object?)cleanVerifRecord).Should().NotBeNull();
        bool cleanPassed = (bool)cleanVerifRecord!.CleanPassed;
        cleanPassed.Should().BeTrue();

        // Assert: 6. Resolved issue with clean verification, remediation, and activity logs
        var resolvedIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.ResolvedIssueId });
        ((object?)resolvedIssue).Should().NotBeNull();
        string resStatus = (string)resolvedIssue!.Status;
        DateTime? resAt = (DateTime?)resolvedIssue.ResolvedAtUtc;
        resStatus.Should().Be("Resolved");
        resAt.Should().NotBeNull();

        var resRemediation = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.RemediationRecords WHERE IssueId = @Id",
            new { Id = DevelopmentDemoDataSeeder.ResolvedIssueId });
        ((object?)resRemediation).Should().NotBeNull();

        var resVerification = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @Id",
            new { Id = DevelopmentDemoDataSeeder.ResolvedIssueId });
        ((object?)resVerification).Should().NotBeNull();
        bool resCleanPassed = (bool)resVerification!.CleanPassed;
        resCleanPassed.Should().BeTrue();

        var resLogs = (await _factory.QuerySqlAsync<string>(
            "SELECT ActivityType FROM dbo.IssueActivityLogs WHERE IssueId = @Id ORDER BY TimestampUtc ASC",
            new { Id = DevelopmentDemoDataSeeder.ResolvedIssueId })).ToList();
        resLogs.Should().Contain(new[] { "IssueDiscovered", "Taken", "RemediationRecorded", "VerificationRecorded", "Resolved" });

        // Assert: 7. Dismissed issue with valid dismissal justification (BR-PRIMA-006 >= 20 chars) and activity logs
        var dismissedIssue = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.Issues WHERE Id = @Id",
            new { Id = DevelopmentDemoDataSeeder.DismissedIssueId });
        ((object?)dismissedIssue).Should().NotBeNull();
        string disStatus = (string)dismissedIssue!.Status;
        DateTime? disAt = (DateTime?)dismissedIssue.DismissedAtUtc;
        disStatus.Should().Be("Dismissed");
        disAt.Should().NotBeNull();

        var disRecord = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT * FROM dbo.DismissalRecords WHERE IssueId = @Id",
            new { Id = DevelopmentDemoDataSeeder.DismissedIssueId });
        ((object?)disRecord).Should().NotBeNull();
        string rationale = (string)disRecord!.JustificationRationale;
        rationale.Trim().Length.Should().BeGreaterOrEqualTo(20);

        var disLogs = (await _factory.QuerySqlAsync<string>(
            "SELECT ActivityType FROM dbo.IssueActivityLogs WHERE IssueId = @Id ORDER BY TimestampUtc ASC",
            new { Id = DevelopmentDemoDataSeeder.DismissedIssueId })).ToList();
        disLogs.Should().Contain(new[] { "IssueDiscovered", "Dismissed" });

        // Record initial counts
        var detectorsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Detectors;");
        var issuesCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Issues;");
        var runsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.InspectionRuns;");
        var occurrencesCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Occurrences;");
        var notesCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.InvestigationNotes;");
        var remediationsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.RemediationRecords;");
        var verificationsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.VerificationRecords;");
        var dismissalsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.DismissalRecords;");
        var logsCount1 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.IssueActivityLogs;");

        // Act: Run seed for the second time (Idempotency test)
        await seeder.SeedAsync();

        // Assert: Counts must remain identical
        var detectorsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Detectors;");
        var issuesCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Issues;");
        var runsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.InspectionRuns;");
        var occurrencesCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.Occurrences;");
        var notesCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.InvestigationNotes;");
        var remediationsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.RemediationRecords;");
        var verificationsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.VerificationRecords;");
        var dismissalsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.DismissalRecords;");
        var logsCount2 = await _factory.QuerySingleOrDefaultSqlAsync<int>("SELECT COUNT(1) FROM dbo.IssueActivityLogs;");

        detectorsCount2.Should().Be(detectorsCount1);
        issuesCount2.Should().Be(issuesCount1);
        runsCount2.Should().Be(runsCount1);
        occurrencesCount2.Should().Be(occurrencesCount1);
        notesCount2.Should().Be(notesCount1);
        remediationsCount2.Should().Be(remediationsCount1);
        verificationsCount2.Should().Be(verificationsCount1);
        dismissalsCount2.Should().Be(dismissalsCount1);
        logsCount2.Should().Be(logsCount1);

        // Verify composite query with GetIssueByIdHandler on seeded issues
        var handler = new GetIssueByIdHandler(_connectionFactory);
        var detailDto = await handler.HandleAsync(DevelopmentDemoDataSeeder.InvestigatingCleanVerificationIssueId);
        detailDto.Should().NotBeNull();
        detailDto!.Status.Should().Be("Investigating");
        detailDto.Occurrences.Should().NotBeEmpty();
        detailDto.Remediations.Should().NotBeEmpty();
        detailDto.Verifications.Should().NotBeEmpty();
        detailDto.ActivityLogs.Should().NotBeEmpty();

        // Verify Detector queries work cleanly against seeded data (S-03, S-04, S-06)
        var detectorListHandler = new GetDetectorListHandler(_connectionFactory);
        var detectorList = await detectorListHandler.HandleAsync(new GetDetectorListQuery());
        detectorList.Should().HaveCount(6);

        var summaryHandler = new GetDetectorSummaryHandler(_connectionFactory);
        var summary = await summaryHandler.HandleAsync();
        summary.TotalCount.Should().Be(6);
        summary.HealthyCount.Should().Be(2); // Billing, Prescription
        summary.FailingCount.Should().Be(1); // Replication
        summary.SilentCount.Should().Be(1);  // Admission
        summary.PausedCount.Should().Be(1);  // Laboratory

        var detectorDetailHandler = new GetDetectorByIdHandler(_connectionFactory);
        var billingDetail = await detectorDetailHandler.HandleAsync(DevelopmentDemoDataSeeder.BillingDetectorId);
        billingDetail.Should().NotBeNull();
        billingDetail!.ReliabilityStats.TotalRuns30d.Should().BeGreaterOrEqualTo(30);
        billingDetail.ReliabilityStats.PassRate30d.Should().BeGreaterThan(90.0m);
        billingDetail.RecentRuns.Should().HaveCount(10);

        var inspectionRunsHandler = new GetInspectionRunsHandler(_connectionFactory);
        var runsLog = await inspectionRunsHandler.HandleAsync(
            DevelopmentDemoDataSeeder.BillingDetectorId,
            new GetInspectionRunsQuery(
                StartDateUtc: DateTime.UtcNow.AddDays(-30),
                EndDateUtc: DateTime.UtcNow));
        runsLog.Should().NotBeNull();
        runsLog!.TotalCount.Should().BeGreaterOrEqualTo(30);
    }
}
