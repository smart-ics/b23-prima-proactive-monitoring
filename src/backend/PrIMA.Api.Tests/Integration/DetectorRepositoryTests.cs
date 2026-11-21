using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Infrastructure.Persistence;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorRepositoryTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly IDetectorRepository _repository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DetectorRepositoryTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        var scope = factory.Services.CreateScope();
        _repository = scope.ServiceProvider.GetRequiredService<IDetectorRepository>();
        _sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Detector CreateSampleDetector(string name = "Billing Anomaly Detector", int cadence = 300)
    {
        return new Detector(
            Guid.NewGuid(),
            name,
            "Monitors unposted billing transactions",
            MonitoringScope.DataIntegrity,
            "Billing",
            cadence,
            "https://hospital.local/probes/billing",
            AdminStatus.Active,
            DetectorHealth.Healthy,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(-1));
    }

    [Fact]
    public async Task InsertAsync_And_GetByIdAsync_PersistsAndLoadsDetectorAggregate()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        var loaded = await _repository.GetByIdAsync(detector.Id, conn);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(detector.Id);
        loaded.Name.Should().Be(detector.Name);
        loaded.Description.Should().Be(detector.Description);
        loaded.Scope.Should().Be(detector.Scope);
        loaded.TargetModule.Should().Be(detector.TargetModule);
        loaded.ExpectedCadenceSeconds.Should().Be(detector.ExpectedCadenceSeconds);
        loaded.WebhookUrl.Should().Be(detector.WebhookUrl);
        loaded.AdminStatus.Should().Be(AdminStatus.Active);
        loaded.HealthStatus.Should().Be(DetectorHealth.Healthy);
    }

    [Fact]
    public async Task GetByNameAsync_FindsDetectorByName_ReturnsNullWhenNotFound()
    {
        var detector = CreateSampleDetector("Pharmacy Cadence Probe");
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        var found = await _repository.GetByNameAsync("Pharmacy Cadence Probe", conn);
        var notFound = await _repository.GetByNameAsync("NonExistentProbe", conn);

        found.Should().NotBeNull();
        found!.Id.Should().Be(detector.Id);
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConfigurationAsync_UpdatesConfigurationFields()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        detector.UpdateConfiguration(
            "Updated Description for Pharmacy",
            MonitoringScope.ProcessIntegrity,
            "Pharmacy",
            600,
            "https://hospital.local/probes/pharmacy-v2");

        using var updateTx = conn.BeginTransaction();
        await _repository.UpdateConfigurationAsync(detector, conn, updateTx);
        updateTx.Commit();

        var loaded = await _repository.GetByIdAsync(detector.Id, conn);
        loaded.Should().NotBeNull();
        loaded!.Description.Should().Be("Updated Description for Pharmacy");
        loaded.Scope.Should().Be(MonitoringScope.ProcessIntegrity);
        loaded.TargetModule.Should().Be("Pharmacy");
        loaded.ExpectedCadenceSeconds.Should().Be(600);
        loaded.WebhookUrl.Should().Be("https://hospital.local/probes/pharmacy-v2");
    }

    [Fact]
    public async Task UpdateAdminStatusAsync_UpdatesAdminStatusInDatabase()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        using var pauseTx = conn.BeginTransaction();
        await _repository.UpdateAdminStatusAsync(detector.Id, AdminStatus.Paused, conn, pauseTx);
        pauseTx.Commit();

        var loaded = await _repository.GetByIdAsync(detector.Id, conn);
        loaded.Should().NotBeNull();
        loaded!.AdminStatus.Should().Be(AdminStatus.Paused);
    }

    [Fact]
    public async Task UpdateHealthAndHeartbeatAsync_UpdatesHealthAndTimestamps()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        var newRunUtc = DateTime.UtcNow;
        var newHealthyUtc = DateTime.UtcNow;

        using var healthTx = conn.BeginTransaction();
        await _repository.UpdateHealthAndHeartbeatAsync(detector.Id, DetectorHealth.Failing, newRunUtc, null, conn, healthTx);
        healthTx.Commit();

        var loadedAfterFailing = await _repository.GetByIdAsync(detector.Id, conn);
        loadedAfterFailing.Should().NotBeNull();
        loadedAfterFailing!.HealthStatus.Should().Be(DetectorHealth.Failing);
        loadedAfterFailing.LastRunAtUtc.Should().BeCloseTo(newRunUtc, TimeSpan.FromSeconds(1));
        // LastHealthyHeartbeatUtc preserved via COALESCE
        loadedAfterFailing.LastHealthyHeartbeatUtc.Should().BeCloseTo(detector.LastHealthyHeartbeatUtc!.Value, TimeSpan.FromSeconds(1));

        using var recoverTx = conn.BeginTransaction();
        await _repository.UpdateHealthAndHeartbeatAsync(detector.Id, DetectorHealth.Healthy, newRunUtc, newHealthyUtc, conn, recoverTx);
        recoverTx.Commit();

        var loadedAfterRecovery = await _repository.GetByIdAsync(detector.Id, conn);
        loadedAfterRecovery.Should().NotBeNull();
        loadedAfterRecovery!.HealthStatus.Should().Be(DetectorHealth.Healthy);
        loadedAfterRecovery.LastHealthyHeartbeatUtc.Should().BeCloseTo(newHealthyUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetActiveSilentCandidatesAsync_ReturnsOnlyEligibleSilentDetectors()
    {
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        var now = DateTime.UtcNow;

        // Detector 1: Active, last healthy 20 minutes ago (cutoff: 10 minutes ago) -> Candidate
        var silentCandidate = new Detector(
            Guid.NewGuid(), "Silent Candidate Detector", "Desc", MonitoringScope.DataIntegrity, "ModuleA", 300,
            null, AdminStatus.Active, DetectorHealth.Healthy, now.AddMinutes(-20), now.AddMinutes(-20), now.AddHours(-2));

        // Detector 2: Active, last healthy 2 minutes ago (cutoff: 10 minutes ago) -> Not candidate
        var healthyDetector = new Detector(
            Guid.NewGuid(), "Healthy Detector", "Desc", MonitoringScope.DataIntegrity, "ModuleB", 300,
            null, AdminStatus.Active, DetectorHealth.Healthy, now.AddMinutes(-2), now.AddMinutes(-2), now.AddHours(-2));

        // Detector 3: Paused, last healthy 30 minutes ago -> Not candidate (AdminStatus != Active)
        var pausedDetector = new Detector(
            Guid.NewGuid(), "Paused Detector", "Desc", MonitoringScope.DataIntegrity, "ModuleC", 300,
            null, AdminStatus.Paused, DetectorHealth.Healthy, now.AddMinutes(-30), now.AddMinutes(-30), now.AddHours(-2));

        using var tx = conn.BeginTransaction();
        await _repository.InsertAsync(silentCandidate, conn, tx);
        await _repository.InsertAsync(healthyDetector, conn, tx);
        await _repository.InsertAsync(pausedDetector, conn, tx);
        tx.Commit();

        var cutoffUtc = now.AddMinutes(-10);
        var candidates = await _repository.GetActiveSilentCandidatesAsync(cutoffUtc, conn);

        candidates.Should().ContainSingle(d => d.Id == silentCandidate.Id);
        candidates.Should().NotContain(d => d.Id == healthyDetector.Id);
        candidates.Should().NotContain(d => d.Id == pausedDetector.Id);
    }

    [Fact]
    public async Task LockDetectorForUpdateAsync_ExecutesUnderTransactionAndReturnsDetector()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);
        tx.Commit();

        using var lockTx = conn.BeginTransaction();
        var lockedDetector = await _repository.LockDetectorForUpdateAsync(detector.Id, conn, lockTx);
        lockTx.Commit();

        lockedDetector.Should().NotBeNull();
        lockedDetector!.Id.Should().Be(detector.Id);
        lockedDetector.Name.Should().Be(detector.Name);
    }

    [Fact]
    public async Task RecordInspectionRunAsync_InsertsInspectionRunWithFailureReason()
    {
        var detector = CreateSampleDetector();
        using var conn = _sqlConnectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();

        await _repository.InsertAsync(detector, conn, tx);

        var run = InspectionRun.Create(
            detector.Id,
            DateTime.UtcNow,
            TriggerSource.Manual,
            InspectionOutcome.ExecutionFailed,
            15420,
            "Timeout waiting for external SIMRS database probe response after 15000ms.");

        await _repository.RecordInspectionRunAsync(run, conn, tx);
        tx.Commit();

        const string sql = "SELECT Id, DetectorId, TriggerSource, Outcome, DurationMs, FailureReason FROM dbo.InspectionRuns WHERE Id = @Id;";
        var persistedRun = await conn.QuerySingleOrDefaultAsync<InspectionRunRow>(sql, new { run.Id });

        persistedRun.Should().NotBeNull();
        persistedRun!.DetectorId.Should().Be(detector.Id);
        persistedRun.TriggerSource.Should().Be("Manual");
        persistedRun.Outcome.Should().Be("ExecutionFailed");
        persistedRun.DurationMs.Should().Be(15420);
        persistedRun.FailureReason.Should().Be("Timeout waiting for external SIMRS database probe response after 15000ms.");
    }

    private sealed record InspectionRunRow(
        Guid Id,
        Guid DetectorId,
        string TriggerSource,
        string Outcome,
        int DurationMs,
        string? FailureReason);
}
