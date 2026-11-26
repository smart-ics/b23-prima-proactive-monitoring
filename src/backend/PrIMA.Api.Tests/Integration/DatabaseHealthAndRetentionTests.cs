using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DatabaseHealthAndRetentionTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DatabaseHealthAndRetentionTests(PrIMAApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private MonitoringSupervisorWorker CreateWorker(int retentionDays = 90)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supervisor:PollingIntervalSeconds"] = "60",
                ["Supervisor:UnacknowledgedSlaTimeoutMinutes"] = "30",
                ["Retention:InspectionRunsDays"] = retentionDays.ToString()
            })
            .Build();

        return new MonitoringSupervisorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<MonitoringSupervisorWorker>.Instance);
    }

    [Fact]
    public async Task HealthEndpoints_WhenDatabaseConnected_Returns200OkWithConnectedStatus()
    {
        var client = _factory.CreateClient();

        // 1. GET /health
        var healthResp = await client.GetAsync("/health");
        healthResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var healthBody = await healthResp.Content.ReadFromJsonAsync<HealthResponseDto>(JsonOptions);
        healthBody.Should().NotBeNull();
        healthBody!.Status.Should().Be("Healthy");
        healthBody.Database.Should().Be("Connected");
        healthBody.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // 2. GET /api/health
        var apiHealthResp = await client.GetAsync("/api/health");
        apiHealthResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiHealthBody = await apiHealthResp.Content.ReadFromJsonAsync<HealthResponseDto>(JsonOptions);
        apiHealthBody.Should().NotBeNull();
        apiHealthBody!.Status.Should().Be("Healthy");
        apiHealthBody.Database.Should().Be("Connected");
        apiHealthBody.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task HealthEndpoints_WhenDatabaseDisconnected_Returns503ServiceUnavailable()
    {
        var brokenFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISqlConnectionFactory>(
                    new SqlConnectionFactory("Server=127.0.0.1,65534;Database=NonExistent;Connection Timeout=1;TrustServerCertificate=True;"));
            });
        });

        var client = brokenFactory.CreateClient();

        var apiHealthResp = await client.GetAsync("/api/health");
        apiHealthResp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await apiHealthResp.Content.ReadFromJsonAsync<HealthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be("Unhealthy");
        body.Database.Should().Be("Disconnected");
        body.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RetentionPruning_PrunesCleanPassedRunsOlderThanCutoff_AndPreservesOthers()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var detectorId = Guid.NewGuid();

        var run1OldPassedCleanId = Guid.NewGuid();
        var run2OldPassedCleanId = Guid.NewGuid();
        var run3RecentPassedCleanId = Guid.NewGuid();
        var run4VeryRecentPassedId = Guid.NewGuid();
        var run5OldPassedLinkedId = Guid.NewGuid();
        var run6OldFindingId = Guid.NewGuid();
        var run7OldFailedId = Guid.NewGuid();

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    AdminStatus, HealthStatus, CreatedAtUtc
                ) VALUES (
                    @Id, 'Retention-Test-Detector', 'Test', 'DataIntegrity', 'Billing', 60,
                    'Paused', 'Healthy', @CreatedAtUtc
                );
                """, new { Id = detectorId, CreatedAtUtc = now.AddDays(-150) });

            // 1. Clean Passed run, 95 days old (should be pruned)
            await InsertInspectionRunAsync(conn, run1OldPassedCleanId, detectorId, now.AddDays(-95), "Passed");

            // 2. Clean Passed run, 120 days old (should be pruned)
            await InsertInspectionRunAsync(conn, run2OldPassedCleanId, detectorId, now.AddDays(-120), "Passed");

            // 3. Clean Passed run, 80 days old (within retention, preserve)
            await InsertInspectionRunAsync(conn, run3RecentPassedCleanId, detectorId, now.AddDays(-80), "Passed");

            // 4. Clean Passed run, 2 days old (recent, preserve)
            await InsertInspectionRunAsync(conn, run4VeryRecentPassedId, detectorId, now.AddDays(-2), "Passed");

            // 5. Passed run, 100 days old, BUT linked to an Occurrence (preserve per BR-PRIMA-009 / FK)
            await InsertInspectionRunAsync(conn, run5OldPassedLinkedId, detectorId, now.AddDays(-100), "Passed");
            var issueId = Guid.NewGuid();
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Issues (
                    Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                    BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                    OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
                ) VALUES (
                    @Id, @Key, 'Old Passed With Occurrence', @DetectorId, 'Retention-Test-Detector', 'DataIntegrity',
                    'Low', 'Low', 1, 'Closed', 0, 1, @Now, @Now
                );
                """, new { Id = issueId, Key = $"RETENTION:{Guid.NewGuid():N}", DetectorId = detectorId, Now = now.AddDays(-100) });
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Occurrences (
                    Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc, Module, TraceString
                ) VALUES (
                    @Id, @IssueId, @InspectionRunId, 1, @Now, 'Billing', 'Occurrence evidence'
                );
                """, new { Id = Guid.NewGuid(), IssueId = issueId, InspectionRunId = run5OldPassedLinkedId, Now = now.AddDays(-100) });

            // 6. FindingDiscovered run, 105 days old (preserve per BR-PRIMA-009)
            await InsertInspectionRunAsync(conn, run6OldFindingId, detectorId, now.AddDays(-105), "FindingDiscovered");

            // 7. ExecutionFailed run, 110 days old (preserve per BR-PRIMA-009)
            await InsertInspectionRunAsync(conn, run7OldFailedId, detectorId, now.AddDays(-110), "ExecutionFailed");
        }

        var worker = CreateWorker(retentionDays: 90);

        // Act
        var deletedCount = await worker.ExecuteRetentionPruningAsync(simulatedUtcNow: now);

        // Assert
        deletedCount.Should().Be(2);

        using (var conn = _factory.CreateDbConnection())
        {
            var remainingRuns = (await conn.QueryAsync<Guid>(
                "SELECT Id FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId;",
                new { DetectorId = detectorId })).ToHashSet();

            remainingRuns.Should().NotContain(run1OldPassedCleanId);
            remainingRuns.Should().NotContain(run2OldPassedCleanId);

            remainingRuns.Should().Contain(run3RecentPassedCleanId);
            remainingRuns.Should().Contain(run4VeryRecentPassedId);
            remainingRuns.Should().Contain(run5OldPassedLinkedId);
            remainingRuns.Should().Contain(run6OldFindingId);
            remainingRuns.Should().Contain(run7OldFailedId);
        }
    }

    [Fact]
    public async Task RetentionPruning_BatchLoop_DeletesAcrossMultiple1000ChunksUntilZero()
    {
        // Arrange: Insert 2,200 old passed runs (more than 2 batches of 1,000)
        var now = DateTime.UtcNow;
        var detectorId = Guid.NewGuid();
        const int totalOldRuns = 2200;

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    AdminStatus, HealthStatus, CreatedAtUtc
                ) VALUES (
                    @Id, 'Retention-Chunk-Detector', 'Test', 'DataIntegrity', 'Billing', 60,
                    'Paused', 'Healthy', @CreatedAtUtc
                );
                """, new { Id = detectorId, CreatedAtUtc = now.AddDays(-200) });

            // Fast multi-row inserts in 4 chunks of 550 to avoid Dapper parameter limit and roundtrips
            const int chunkSize = 550;
            for (var chunk = 0; chunk < 4; chunk++)
            {
                var values = string.Join(",", Enumerable.Range(1, chunkSize).Select(i =>
                {
                    var offset = (chunk * chunkSize) + i;
                    var runTime = now.AddDays(-100).AddMinutes(-offset).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    return $"('{Guid.NewGuid()}', '{detectorId}', '{runTime}', 'Scheduled', 'Passed', 50, '{runTime}')";
                }));

                await conn.ExecuteAsync($"INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc) VALUES {values};");
            }
        }

        var worker = CreateWorker(retentionDays: 90);

        // Act
        var deletedCount = await worker.ExecuteRetentionPruningAsync(simulatedUtcNow: now);

        // Assert: All 2,200 runs deleted across 3 iterations (1000 + 1000 + 200)
        deletedCount.Should().Be(totalOldRuns);

        using (var conn = _factory.CreateDbConnection())
        {
            var remainingCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId;",
                new { DetectorId = detectorId });
            remainingCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task SupervisionCycle_EvaluatesRetentionPruningDaily()
    {
        // Arrange
        var t0 = DateTime.UtcNow.AddDays(-5);
        var detectorId = Guid.NewGuid();

        using (var conn = _factory.CreateDbConnection())
        {
            await conn.ExecuteAsync("""
                INSERT INTO dbo.Detectors (
                    Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                    AdminStatus, HealthStatus, CreatedAtUtc, LastHealthyHeartbeatUtc
                ) VALUES (
                    @Id, 'Retention-Cycle-Detector', 'Test', 'DataIntegrity', 'Billing', 60,
                    'Paused', 'Healthy', @T0, @T0
                );
                """, new { Id = detectorId, T0 = t0 });

            // Run 1: 95 days old relative to t0
            await InsertInspectionRunAsync(conn, Guid.NewGuid(), detectorId, t0.AddDays(-95), "Passed");
        }

        var worker = CreateWorker(retentionDays: 90);

        // Cycle 1 at t0 -> executes retention pruning (deletes 1 run)
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: t0);

        using (var conn = _factory.CreateDbConnection())
        {
            var countAfterCycle1 = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.InspectionRuns WHERE Outcome = 'Passed';");
            countAfterCycle1.Should().Be(0);

            // Insert another old run that would be eligible
            await InsertInspectionRunAsync(conn, Guid.NewGuid(), detectorId, t0.AddDays(-95), "Passed");
        }

        // Cycle 2 at t0 + 2 hours -> within 24h interval, retention pruning is NOT executed
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: t0.AddHours(2));

        using (var conn = _factory.CreateDbConnection())
        {
            var countAfterCycle2 = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.InspectionRuns WHERE Outcome = 'Passed';");
            countAfterCycle2.Should().Be(1, "within 24 hours retention pruning should not run again");
        }

        // Cycle 3 at t0 + 25 hours -> 25h passed, retention pruning runs and deletes the old run
        await worker.ExecuteSupervisionCycleAsync(simulatedUtcNow: t0.AddHours(25));

        using (var conn = _factory.CreateDbConnection())
        {
            var countAfterCycle3 = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.InspectionRuns WHERE Outcome = 'Passed';");
            countAfterCycle3.Should().Be(0, "after 24 hours elapsed retention pruning should run again");
        }
    }

    private static async Task InsertInspectionRunAsync(IDbConnection conn, Guid id, Guid detectorId, DateTime timestampUtc, string outcome)
    {
        await conn.ExecuteAsync("""
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc
            ) VALUES (
                @Id, @DetectorId, @TimestampUtc, 'Scheduled', @Outcome, 42, @TimestampUtc
            );
            """, new { Id = id, DetectorId = detectorId, TimestampUtc = timestampUtc, Outcome = outcome });
    }

    private sealed record HealthResponseDto(
        string Status,
        string Database,
        DateTime? TimestampUtc,
        string? Error);
}
