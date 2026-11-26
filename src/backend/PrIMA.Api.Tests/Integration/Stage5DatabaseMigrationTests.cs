using Dapper;
using FluentAssertions;
using PrIMA.Api.Migrations;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class Stage5DatabaseMigrationTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;

    public Stage5DatabaseMigrationTests(PrIMAApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        // Ensure web application and migrations have run
        _ = _factory.CreateClient();
        MigrationRunner.Run(PrIMAApiFactory.TestConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void MigrationRunner_Run_ExecutesIdempotentlyWithoutError()
    {
        // Act & Assert - running migration again should succeed idempotently
        var act = () => MigrationRunner.Run(PrIMAApiFactory.TestConnectionString);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Migration005_CreatesDetectorEnrollmentTokensTableAndIndexes()
    {
        using var conn = _factory.CreateDbConnection();

        // Verify table exists
        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.tables WHERE name = 'DetectorEnrollmentTokens' AND schema_id = SCHEMA_ID('dbo')");
        tableExists.Should().Be(1);

        // Verify required columns
        var columns = (await conn.QueryAsync<string>(
            """
            SELECT c.name
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = 'DetectorEnrollmentTokens' AND t.schema_id = SCHEMA_ID('dbo')
            """)).ToList();

        columns.Should().Contain(new[]
        {
            "Id",
            "TokenPrefix",
            "TokenHash",
            "Label",
            "Status",
            "ExpiresAtUtc",
            "CreatedByOfficer",
            "CreatedAtUtc",
            "ConsumedAtUtc",
            "ConsumedByDetectorId"
        });

        // Verify indexes
        var indexes = (await conn.QueryAsync<string>(
            """
            SELECT i.name
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name = 'DetectorEnrollmentTokens' AND t.schema_id = SCHEMA_ID('dbo')
            """)).ToList();

        indexes.Should().Contain("IX_DetectorEnrollmentTokens_Status_Expires");
        indexes.Should().Contain("UX_DetectorEnrollmentTokens_TokenHash");
    }

    [Fact]
    public async Task Migration005_ExtendsDetectorsTableWithCredentialsAndDriftColumns()
    {
        using var conn = _factory.CreateDbConnection();

        var columns = (await conn.QueryAsync<string>(
            """
            SELECT c.name
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = 'Detectors' AND t.schema_id = SCHEMA_ID('dbo')
            """)).ToList();

        columns.Should().Contain(new[]
        {
            "ApiKeyHash",
            "Version",
            "LastActualIntervalSeconds",
            "CadenceDriftWarning"
        });

        var indexes = (await conn.QueryAsync<string>(
            """
            SELECT i.name
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name = 'Detectors' AND t.schema_id = SCHEMA_ID('dbo')
            """)).ToList();

        indexes.Should().Contain("IX_Detectors_ApiKeyHash");
    }

    [Fact]
    public async Task Migration005_ProvisionsSimrsSchemaAndAllSixSandboxTables()
    {
        using var conn = _factory.CreateDbConnection();

        // Verify simrs schema exists
        var schemaExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.schemas WHERE name = 'simrs'");
        schemaExists.Should().Be(1);

        // Verify all 6 tables exist
        var expectedTables = new[]
        {
            "acc_journal_entries",
            "ar_receivables",
            "trx_patient_bills",
            "emr_sync_logs",
            "hfis_doctor_schedules",
            "infra_print_queues"
        };

        foreach (var tableName in expectedTables)
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM sys.tables WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('simrs')");
            exists.Should().Be(1, $"table simrs.{tableName} should exist");
        }
    }

    [Fact]
    public async Task Migration005_SeedsRealisticBaselineAndAnomalyFixturesForSimrsTables()
    {
        using var conn = _factory.CreateDbConnection();

        // 1. Journal entries fixtures
        var journalBatchCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.acc_journal_entries WHERE journal_batch_no IN ('JRN-202611-0001', 'JRN-202611-ERR01')");
        journalBatchCount.Should().BeGreaterOrEqualTo(4);

        // 2. Patient receivables fixtures
        var arCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.ar_receivables WHERE source_trx_no IN ('TRX-AR-202611-001', 'TRX-AR-202611-DUP01')");
        arCount.Should().BeGreaterOrEqualTo(3);

        // 3. Patient bills fixtures
        var billCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.trx_patient_bills WHERE bill_invoice_no IN ('BILL-202611-001', 'BILL-202611-NOJRN01')");
        billCount.Should().BeGreaterOrEqualTo(2);

        // 4. EMR sync logs fixtures
        var emrCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.emr_sync_logs WHERE encounter_no IN ('ENC-202611-001', 'ENC-202611-FAIL01', 'ENC-202611-STALL01')");
        emrCount.Should().BeGreaterOrEqualTo(3);

        // 5. HFIS schedules fixtures
        var scheduleCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.hfis_doctor_schedules WHERE doctor_schedule_code IN ('SCH-INT-001', 'SCH-OBG-002')");
        scheduleCount.Should().BeGreaterOrEqualTo(2);

        // 6. Print queues fixtures
        var queueCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM simrs.infra_print_queues WHERE station_queue_name IN ('PRN-FARMASI-01', 'PRN-KASIR-02', 'PRN-IGD-01')");
        queueCount.Should().BeGreaterOrEqualTo(3);
    }
}
