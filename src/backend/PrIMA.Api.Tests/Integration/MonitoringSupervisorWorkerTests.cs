using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class MonitoringSupervisorWorkerTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public MonitoringSupervisorWorkerTests(PrIMAApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private MonitoringSupervisorWorker CreateWorker(int pollingIntervalSeconds = 60, int slaTimeoutMinutes = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supervisor:PollingIntervalSeconds"] = pollingIntervalSeconds.ToString(),
                ["Supervisor:UnacknowledgedSlaTimeoutMinutes"] = slaTimeoutMinutes.ToString()
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        return new MonitoringSupervisorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            config,
            loggerFactory.CreateLogger<MonitoringSupervisorWorker>());
    }

    private sealed class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        public XunitLoggerProvider(ITestOutputHelper output) => _output = output;
        public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);
        public void Dispose() { }
    }

    private sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _category;
        public XunitLogger(ITestOutputHelper output, string category) { _output = output; _category = category; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try { _output.WriteLine($"[{logLevel}] {_category}: {formatter(state, exception)} {exception}"); } catch { }
        }
    }

    [Fact]
    public async Task SilenceSupervision_WhenDetectorMissesCadence_TransitionsToSilentAndSynthesizesIssue()
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var cadenceSeconds = 60;
        // Last heartbeat was 150 seconds ago (> 2x cadence: 120s)
        var lastHeartbeat = now.AddSeconds(-150);

        using (var conn = _factory.CreateDbConnection())
        {
            const string insertDetectorSql = """
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
                ) VALUES (
                    @Id, @Name, @Desc, 'DataIntegrity', 'Billing', @Cadence,
                    'Active', 'Healthy', @LastHeartbeat, @LastHeartbeat, @CreatedAt
                );
                """;

            await conn.ExecuteAsync(insertDetectorSql, new
            {
                Id = detectorId,
                Name = $"Silent-Detector-{detectorId:N}",
                Desc = "Test silent detector",
                Cadence = cadenceSeconds,
                LastHeartbeat = lastHeartbeat,
                CreatedAt = now.AddMinutes(-10)
            });
        }

        var worker = CreateWorker();

        // Act
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        // Assert
        using (var conn = _factory.CreateDbConnection())
        {
            // Verify detector health transitioned to Silent
            var health = await conn.QuerySingleAsync<string>(
                "SELECT HealthStatus FROM dbo.Detectors WHERE Id = @Id;", new { Id = detectorId });
            health.Should().Be("Silent");

            // Verify synthetic issue created
            var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key;",
                new { Key = $"SILENT:{detectorId}" });

            ((object?)issue).Should().NotBeNull();
            ((string)issue!.Scope).Should().Be("Infrastructure");
            ((string)issue.BaseSeverity).Should().Be("High");
            ((string)issue.CurrentSeverity).Should().Be("High");
            ((int)issue.OccurrenceCount).Should().Be(1);

            // Verify Occurrence #1
            var occurrence = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId AND OccurrenceNumber = 1;",
                new { IssueId = (Guid)issue.Id });

            ((object?)occurrence).Should().NotBeNull();
            ((string)occurrence!.Module).Should().Be("Billing");

            // Verify Activity Log "Created"
            var log = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'Created';",
                new { IssueId = (Guid)issue.Id });

            ((object?)log).Should().NotBeNull();
            ((string)log!.ActorName).Should().Be("System");
        }
    }

    [Fact]
    public async Task SilenceSupervision_WhenSilenceProlonged_AppendsCadenceMultipliedOccurrences()
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var baseline = DateTime.UtcNow.AddMinutes(-10);
        var cadenceSeconds = 60;

        using (var conn = _factory.CreateDbConnection())
        {
            const string insertDetectorSql = """
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc
                ) VALUES (
                    @Id, @Name, @Desc, 'DataIntegrity', 'Pharmacy', @Cadence,
                    'Active', 'Healthy', @LastHeartbeat, @LastHeartbeat, @CreatedAt
                );
                """;

            await conn.ExecuteAsync(insertDetectorSql, new
            {
                Id = detectorId,
                Name = $"Prolonged-Silence-Detector-{detectorId:N}",
                Desc = "Test prolonged silence",
                Cadence = cadenceSeconds,
                LastHeartbeat = baseline,
                CreatedAt = baseline
            });
        }

        var worker = CreateWorker();

        // Tick 1: at baseline + 130 seconds (2.16x cadence -> 2x window breached -> creates issue Occurrence #1)
        var tick1 = baseline.AddSeconds(130);
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: tick1);

        // Tick 2: at baseline + 190 seconds (3.16x cadence -> 3x window breached -> multiples = 3 >= 3 and 3 > 2 -> appends Occurrence #2)
        var tick2 = baseline.AddSeconds(190);
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: tick2);

        // Assert
        using (var conn = _factory.CreateDbConnection())
        {
            var issue = await conn.QuerySingleAsync<dynamic>(
                "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key;",
                new { Key = $"SILENT:{detectorId}" });

            ((int)issue.OccurrenceCount).Should().Be(2);

            var occurrences = (await conn.QueryAsync<dynamic>(
                "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber;",
                new { IssueId = (Guid)issue.Id })).ToList();

            occurrences.Should().HaveCount(2);
            ((int)occurrences[1].OccurrenceNumber).Should().Be(2);

            var recurLog = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'RecurrenceObserved';",
                new { IssueId = (Guid)issue.Id });

            ((object?)recurLog).Should().NotBeNull();
            ((string)recurLog!.Summary).Should().Contain("3x");
        }
    }

    [Fact]
    public async Task DynamicSeverityEscalation_WhenSlaBreached_EscalatesSeverityAndLogsActivity()
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var createdAt = now.AddMinutes(-35); // 35 minutes ago, SLA is 30m

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Detectors (Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds, AdminStatus, HealthStatus, CreatedAtUtc)
                VALUES (@Id, @Name, 'Desc', 'DataIntegrity', 'Billing', 60, 'Active', 'Healthy', @Now);
                """, new { Id = detectorId, Name = $"Detector-{detectorId:N}", Now = now });

            await conn.ExecuteAsync("""
                INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
                VALUES (@RunId, @DetectorId, @CreatedAt, 'Scheduled', 'FindingDiscovered', 10, @CreatedAt);
                """, new { RunId = runId, DetectorId = detectorId, CreatedAt = createdAt });

            await conn.ExecuteAsync("""
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                    OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
                ) VALUES (
                    @Id, @Key, 'Unack Issue', @DetectorId, 'Detector', 'DataIntegrity',
                    'Low', 'Low', 1, 'Open', 0,
                    1, @CreatedAt, @CreatedAt
                );
                """, new { Id = issueId, Key = $"UNACK:{issueId}", DetectorId = detectorId, CreatedAt = createdAt });

            var beforeCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Issues WHERE Id = @Id;", new { Id = issueId });
            _output.WriteLine($"BEFORE WORKER: issue exists = {beforeCount}, issueId = {issueId}");
        }

        var worker = CreateWorker(slaTimeoutMinutes: 30);

        // Act
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        // Assert
        using (var conn = _factory.CreateDbConnection())
        {
            var issue = await conn.QuerySingleAsync<dynamic>(
                "SELECT CurrentSeverity, SeverityRank, EscalationLevel FROM dbo.Issues WHERE Id = @Id;",
                new { Id = issueId });

            ((string)issue.CurrentSeverity).Should().Be("Medium"); // Low -> Medium
            ((byte)issue.SeverityRank).Should().Be(2);
            ((int)issue.EscalationLevel).Should().Be(1);

            var log = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'SeverityEscalated';",
                new { IssueId = issueId });

            ((object?)log).Should().NotBeNull();
            ((string)log!.Summary).Should().Contain("unacknowledged");
        }
    }

    [Fact]
    public async Task DynamicSeverityEscalation_WhenOccurrenceThresholdReached_EscalatesSeverityOnce()
    {
        // Arrange
        var detectorId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Detectors (Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds, AdminStatus, HealthStatus, CreatedAtUtc)
                VALUES (@Id, @Name, 'Desc', 'DataIntegrity', 'Billing', 60, 'Active', 'Healthy', @Now);
                """, new { Id = detectorId, Name = $"Detector-{detectorId:N}", Now = now });

            await conn.ExecuteAsync("""
                INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
                VALUES (@RunId, @DetectorId, @Now, 'Scheduled', 'FindingDiscovered', 10, @Now);
                """, new { RunId = runId, DetectorId = detectorId, Now = now });

            await conn.ExecuteAsync("""
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                    OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc, AcknowledgedAtUtc
                ) VALUES (
                    @Id, @Key, 'Recurring Issue', @DetectorId, 'Detector', 'DataIntegrity',
                    'Medium', 'Medium', 2, 'Acknowledged', 0,
                    5, @Now, @Now, @Now
                );
                """, new { Id = issueId, Key = $"RECUR:{issueId}", DetectorId = detectorId, Now = now });
        }

        var worker = CreateWorker();

        // Act - First pass: should escalate Medium -> High
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        using (var conn = _factory.CreateDbConnection())
        {
            var issue = await conn.QuerySingleAsync<dynamic>(
                "SELECT CurrentSeverity, SeverityRank, EscalationLevel FROM dbo.Issues WHERE Id = @Id;",
                new { Id = issueId });

            ((string)issue.CurrentSeverity).Should().Be("High");
            ((int)issue.EscalationLevel).Should().Be(1);
        }

        // Act - Second pass: should NOT escalate again (idempotent recurrence check)
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: now);

        using (var conn = _factory.CreateDbConnection())
        {
            var issue = await conn.QuerySingleAsync<dynamic>(
                "SELECT CurrentSeverity, SeverityRank, EscalationLevel FROM dbo.Issues WHERE Id = @Id;",
                new { Id = issueId });

            ((string)issue.CurrentSeverity).Should().Be("High");
            ((int)issue.EscalationLevel).Should().Be(1);

            var logs = (await conn.QueryAsync<dynamic>(
                "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'SeverityEscalated';",
                new { IssueId = issueId })).ToList();

            logs.Should().HaveCount(1);
        }
    }
}
