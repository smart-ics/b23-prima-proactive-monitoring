using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Tests.Fixtures;
using PrIMA.Detectors.Sdk;
using PrIMA.Detectors.Services;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public sealed class SimrsNonInterferenceSafetyTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IReadOnlySimrsConnectionFactory _simrsConnectionFactory;

    public SimrsNonInterferenceSafetyTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _simrsConnectionFactory = new ReadOnlySimrsConnectionFactory(PrIMAApiFactory.TestConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string HashKey(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Test 1 (Sub-500ms latency): Executes all 6 pilot SIMRS detector queries against seeded tables,
    /// asserting that every probe query executes in under 500ms (BR-PRIMA-007).
    /// </summary>
    [Fact]
    public async Task ProbeQueries_AgainstSeededSimrsTables_ExecuteUnder500Milliseconds()
    {
        // 1. Journal Balance Query (Accounting)
        const string journalSql = @"
            SELECT journal_batch_no AS TargetEntityReference,
                   SUM(debit) AS DebitTotal,
                   SUM(credit) AS CreditTotal,
                   ABS(SUM(debit) - SUM(credit)) AS VarianceDelta,
                   COUNT(*) AS LineCount
            FROM simrs.acc_journal_entries
            WHERE posted_status = 'Posted'
              AND entry_date >= DATEADD(day, -3, GETUTCDATE())
            GROUP BY journal_batch_no
            HAVING ABS(SUM(debit) - SUM(credit)) > 0.01;";

        // 2. Duplicate Receivable Query (Billing)
        const string receivableSql = @"
            SELECT source_trx_no AS TargetEntityReference,
                   COUNT(*) AS DuplicateCount,
                   SUM(receivable_amount) AS DebitTotal,
                   MAX(receivable_amount) AS CreditTotal,
                   SUM(receivable_amount) - MAX(receivable_amount) AS VarianceDelta
            FROM simrs.ar_receivables
            WHERE status = 'Active'
              AND created_at >= DATEADD(day, -7, GETUTCDATE())
            GROUP BY source_trx_no
            HAVING COUNT(*) > 1;";

        // 3. Unprocessed Billing Pipeline Query (Billing)
        const string billingSql = @"
            SELECT b.bill_invoice_no AS TargetEntityReference,
                   b.bill_amount AS DebitTotal,
                   0.00 AS CreditTotal,
                   b.bill_amount AS VarianceDelta,
                   b.settled_at AS SettledAt
            FROM simrs.trx_patient_bills b
            WHERE b.settlement_status = 'Settled'
              AND b.settled_at <= DATEADD(minute, -15, GETUTCDATE())
              AND b.settled_at >= DATEADD(day, -3, GETUTCDATE())
              AND NOT EXISTS (
                  SELECT 1 FROM simrs.acc_journal_entries j
                  WHERE j.journal_batch_no = b.bill_invoice_no
              );";

        // 4. EMR Synchronization Query (EMR)
        const string emrSql = @"
            SELECT encounter_no AS TargetEntityReference,
                   sync_type AS SyncType,
                   sync_status AS SyncStatus,
                   error_message AS ErrorMessage,
                   DATEDIFF(minute, created_at, GETUTCDATE()) AS StalledMinutes
            FROM simrs.emr_sync_logs
            WHERE sync_status = 'Failed'
               OR (sync_status = 'Pending' AND created_at <= DATEADD(minute, -30, GETUTCDATE()));";

        // 5. SIMRS-HFIS Doctor Schedule Query (Outpatient)
        const string scheduleSql = @"
            SELECT doctor_schedule_code AS TargetEntityReference,
                   doctor_name AS DoctorName,
                   clinic_name AS ClinicName,
                   simrs_start_time AS SimrsStart,
                   hfis_start_time AS HfisStart,
                   simrs_quota AS SimrsQuota,
                   hfis_quota AS HfisQuota
            FROM simrs.hfis_doctor_schedules
            WHERE is_active = 1
              AND schedule_date >= CAST(GETUTCDATE() AS DATE)
              AND (simrs_start_time <> hfis_start_time
                   OR simrs_end_time <> hfis_end_time
                   OR simrs_quota <> hfis_quota);";

        // 6. Remote Print Server Queue Query (Infrastructure)
        const string printQueueSql = @"
            SELECT station_queue_name AS TargetEntityReference,
                   hospital_unit AS HospitalUnit,
                   printer_name AS PrinterName,
                   pending_jobs AS PendingJobs,
                   oldest_job_age_seconds AS OldestJobAgeSeconds,
                   spooler_status AS SpoolerStatus
            FROM simrs.infra_print_queues
            WHERE spooler_status <> 'Online'
               OR (pending_jobs > 0 AND oldest_job_age_seconds >= 300);";

        var queries = new (string Name, string Sql)[]
        {
            ("Journal Balance", journalSql),
            ("Duplicate Receivable", receivableSql),
            ("Unprocessed Billing", billingSql),
            ("EMR Sync", emrSql),
            ("HFIS Schedule", scheduleSql),
            ("Print Queue", printQueueSql)
        };

        foreach (var (name, sql) in queries)
        {
            var sw = Stopwatch.StartNew();
            var results = (await _simrsConnectionFactory.QueryAsync<dynamic>(sql)).ToList();
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(500, $"Query '{name}' must execute in < 500ms per BR-PRIMA-007");
            results.Should().NotBeNull();
            // Seeded sandbox fixtures contain representative anomalies for each table
            results.Count.Should().BeGreaterThan(0, $"Query '{name}' should find seeded anomaly fixtures in sandbox schema");
        }
    }

    /// <summary>
    /// Test 2 (Zero blocking locks): Simulates concurrent hospital OLTP writes (holding uncommitted table locks)
    /// and verifies probe queries execute with READ UNCOMMITTED without blocking or waiting behind write locks.
    /// </summary>
    [Fact]
    public async Task ProbeQuery_ExecutesWithReadUncommitted_DoesNotBlockBehindConcurrentExclusiveLock()
    {
        // Arrange: Open a writer connection holding an exclusive row/table lock in an uncommitted transaction
        using var writerConn = new SqlConnection(PrIMAApiFactory.TestConnectionString);
        await writerConn.OpenAsync();
        using var transaction = writerConn.BeginTransaction();

        // Mutate a row in simrs.acc_journal_entries holding an exclusive lock (XLOCK)
        await writerConn.ExecuteAsync(
            @"UPDATE simrs.acc_journal_entries WITH (XLOCK, ROWLOCK)
              SET credit = credit + 777.00
              WHERE journal_batch_no = 'JRN-202611-0001';",
            transaction: transaction);

        try
        {
            // Act: Probe executes a query on the exact locked row using ReadOnlySimrsConnectionFactory
            var sw = Stopwatch.StartNew();
            var rows = (await _simrsConnectionFactory.QueryAsync<dynamic>(
                "SELECT journal_batch_no, debit, credit FROM simrs.acc_journal_entries WHERE journal_batch_no = 'JRN-202611-0001'"))
                .ToList();
            sw.Stop();

            // Assert: Execution must not block behind the writer transaction (lock timeout is 2000ms, should complete in < 500ms)
            sw.ElapsedMilliseconds.Should().BeLessThan(1000, "READ UNCOMMITTED query must not wait behind exclusive write lock");
            rows.Should().NotBeEmpty();
            // Verify dirty read succeeded (uncommitted credit reflected without blocking)
            decimal creditVal = Convert.ToDecimal(rows[0].credit);
            creditVal.Should().BeGreaterThan(700m);
        }
        finally
        {
            // Clean up: rollback simulated hospital transaction
            transaction.Rollback();
        }
    }

    /// <summary>
    /// Test 3 (DML rejection): Asserts SimrsSqlSafetyGuard throws exception when query contains mutation keywords.
    /// </summary>
    [Theory]
    [InlineData("INSERT INTO simrs.acc_journal_entries (journal_batch_no) VALUES ('TEST')")]
    [InlineData("UPDATE simrs.acc_journal_entries SET posted_status = 'Pending'")]
    [InlineData("DELETE FROM simrs.acc_journal_entries WHERE id = 1")]
    [InlineData("DROP TABLE simrs.acc_journal_entries")]
    [InlineData("ALTER TABLE simrs.acc_journal_entries ADD test_col INT")]
    [InlineData("TRUNCATE TABLE simrs.acc_journal_entries")]
    [InlineData("EXEC sp_who2")]
    [InlineData("EXECUTE sp_help")]
    [InlineData("MERGE INTO simrs.acc_journal_entries USING source ON 1=1 WHEN MATCHED THEN DELETE;")]
    [InlineData("CREATE TABLE #temp_table (id INT)")]
    [InlineData("GRANT SELECT ON simrs.acc_journal_entries TO public")]
    [InlineData("REVOKE SELECT ON simrs.acc_journal_entries FROM public")]
    [InlineData("select * from simrs.acc_journal_entries; update simrs.acc_journal_entries set debit = 0;")]
    [InlineData("/* comment */ delete from simrs.ar_receivables")]
    public void SimrsSqlSafetyGuard_RejectsForbiddenMutationKeywords(string mutationSql)
    {
        // Act
        var act = () => SimrsSqlSafetyGuard.EnsureReadOnlySelect(mutationSql);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Forbidden SQL mutation*");
    }

    /// <summary>
    /// Test 3b: Verifies valid SELECT statements are accepted by SimrsSqlSafetyGuard without throwing.
    /// </summary>
    [Theory]
    [InlineData("SELECT * FROM simrs.acc_journal_entries")]
    [InlineData("SELECT journal_batch_no, SUM(debit) FROM simrs.acc_journal_entries GROUP BY journal_batch_no")]
    [InlineData("WITH Cte AS (SELECT 1 AS Val) SELECT Val FROM Cte")]
    [InlineData("SELECT b.* FROM simrs.trx_patient_bills b WHERE NOT EXISTS (SELECT 1 FROM simrs.acc_journal_entries j WHERE j.journal_batch_no = b.bill_invoice_no)")]
    public void SimrsSqlSafetyGuard_AllowsValidSelectStatements(string selectSql)
    {
        // Act
        var result = SimrsSqlSafetyGuard.EnsureReadOnlySelect(selectSql);

        // Assert
        result.Should().Be(selectSql);
        SimrsSqlSafetyGuard.IsReadOnlySelect(selectSql).Should().BeTrue();
    }

    /// <summary>
    /// Test 3c: Asserts ReadOnlySimrsConnectionFactory also rejects mutation statements prior to opening connection.
    /// </summary>
    [Fact]
    public async Task ReadOnlySimrsConnectionFactory_QueryAsync_RejectsMutationBeforeExecution()
    {
        // Act
        var act = async () => await _simrsConnectionFactory.QueryAsync<dynamic>("UPDATE simrs.acc_journal_entries SET debit = 0");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Forbidden SQL mutation*");
    }

    /// <summary>
    /// Test 4 (Timeout trapping): Simulates a query taking > 5 seconds and asserts probe terminates
    /// and traps into ExecutionFailed run (BR-PRIMA-005).
    /// </summary>
    [Fact]
    public async Task ProbeQuery_WhenExceedsTimeout_TerminatesAndTrapsIntoExecutionFailedReport()
    {
        // Arrange: Detector probe executing a query exceeding the default 5s CommandTimeout
        var slowProbe = new SlowQuerySimulationProbe(_simrsConnectionFactory);
        var executor = new ProbeExecutor(NullLogger<ProbeExecutor>.Instance);

        // Act: Execute inspection via ProbeExecutor (which traps timeout into ExecutionFailed)
        var report = await executor.ExecuteAsync(slowProbe);

        // Assert: Probe report converted to ExecutionFailed with duration >= 5000ms
        report.Outcome.Should().Be(TelemetryOutcome.ExecutionFailed);
        report.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        report.ErrorMessage.Should().ContainEquivalentOf("timeout");
        report.ExecutionDurationMs.Should().BeGreaterThanOrEqualTo(4900);

        // Arrange 2: Ingest the ExecutionFailed report to PrIMA server endpoint
        const string rawKey = "prm_key_test_timeout_detector_key_99";
        var keyHash = HashKey(rawKey);

        var detectorId = await _factory.SeedDetectorAsync(
            name: "Timeout Test Detector",
            apiKeyHash: keyHash,
            expectedCadenceSeconds: 60);

        var telemetryRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = report.Outcome.ToString(),
            DurationMs = report.ExecutionDurationMs,
            FailureReason = report.ErrorMessage
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(telemetryRequest)
        };
        request.Headers.Add("X-Detector-API-Key", rawKey);

        // Act 2: Submit to PrIMA ingestion API
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Assert 2: Verify InspectionRun was stored with Outcome = 'ExecutionFailed' (BR-PRIMA-005)
        var runs = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.InspectionRuns WHERE DetectorId = @DetectorId",
            new { DetectorId = detectorId })).ToList();

        runs.Should().HaveCount(1);
        string outcome = runs[0].Outcome;
        outcome.Should().Be("ExecutionFailed");
        string failureReason = runs[0].FailureReason;
        failureReason.Should().ContainEquivalentOf("timeout");
    }

    /// <summary>
    /// Probe fixture simulating a long-running query against SQL Server that exceeds the 5-second timeout.
    /// </summary>
    private sealed class SlowQuerySimulationProbe : IDetectorProbe
    {
        private readonly IReadOnlySimrsConnectionFactory _factory;

        public DetectorManifest Manifest { get; } = new(
            name: "Slow Query Simulation Probe",
            targetModule: "Accounting",
            scope: "DataIntegrity",
            expectedCadenceSeconds: 60,
            description: "Simulates a SIMRS query that exceeds 5-second CommandTimeout",
            version: "1.0.0");

        public SlowQuerySimulationProbe(IReadOnlySimrsConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default)
        {
            // WAITFOR DELAY '00:00:06' takes 6 seconds, exceeding the 5-second CommandTimeout
            const string slowSql = "WAITFOR DELAY '00:00:06'; SELECT 1 AS Value;";
            var result = await _factory.QueryAsync<int>(slowSql, ct: ct);
            return TelemetryReport.Passed(durationMs: 6000);
        }
    }
}
