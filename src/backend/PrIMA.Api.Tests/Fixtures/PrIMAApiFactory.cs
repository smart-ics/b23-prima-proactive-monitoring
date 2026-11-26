using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrIMA.Api.Features.Auth;

namespace PrIMA.Api.Tests.Fixtures;

public class PrIMAApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString = "Server=localhost;Database=PrIMA_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
    public const string TestApiKey = "prima-dev-key-alpha-99";
    public const string TestJwtSigningKey = "prima-super-secret-development-key-hospital-2026-audit-workbench";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PrIMADb"] = TestConnectionString,
                ["Jwt:SigningKey"] = TestJwtSigningKey,
                ["Jwt:ExpirationHours"] = "12",
                ["Ingestion:ApiKeys:0"] = TestApiKey,
                ["OperationalTimeZone"] = "Asia/Jakarta",
                ["SeedDemoData"] = "false",
                ["Supervisor:PollingIntervalSeconds"] = "3600"
            });
        });
    }

    public IDbConnection CreateDbConnection() => new SqlConnection(TestConnectionString);
    public IDbConnection CreateConnection() => CreateDbConnection();
    public IDbConnection GetConnection() => CreateDbConnection();

    public async Task ExecuteSqlAsync(string sql, object? param = null)
    {
        using var conn = CreateDbConnection();
        await conn.ExecuteAsync(sql, param);
    }

    public async Task<T?> QuerySingleOrDefaultSqlAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateDbConnection();
        return await conn.QuerySingleOrDefaultAsync<T>(sql, param);
    }

    public async Task<IEnumerable<T>> QuerySqlAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateDbConnection();
        return await conn.QueryAsync<T>(sql, param);
    }

    public async Task ResetDatabaseAsync()
    {
        const string cleanupSql = """
            BEGIN TRANSACTION;
            DELETE FROM dbo.IssueActivityLogs WITH (TABLOCKX);
            DELETE FROM dbo.InvestigationNotes WITH (TABLOCKX);
            DELETE FROM dbo.Occurrences WITH (TABLOCKX);
            DELETE FROM dbo.RemediationRecords WITH (TABLOCKX);
            DELETE FROM dbo.VerificationRecords WITH (TABLOCKX);
            DELETE FROM dbo.DismissalRecords WITH (TABLOCKX);
            DELETE FROM dbo.Issues WITH (TABLOCKX);
            DELETE FROM dbo.InspectionRuns WITH (TABLOCKX);
            DELETE FROM dbo.DetectorEnrollmentTokens WITH (TABLOCKX);
            DELETE FROM dbo.Detectors WITH (TABLOCKX);
            DELETE FROM dbo.Officers WHERE Username NOT IN ('ahmad.budi', 'siti.rahma', 'edp.lead');
            UPDATE dbo.Officers SET IsActive = 1 WHERE Username IN ('ahmad.budi', 'siti.rahma', 'edp.lead');
            COMMIT TRANSACTION;
            """;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var conn = CreateDbConnection();
                await conn.ExecuteAsync(cleanupSql);
                return;
            }
            catch (SqlException) when (attempt < 5)
            {
                await Task.Delay(150 * attempt);
            }
        }
    }

    public async Task<string> GetAuthTokenAsync(HttpClient client, string username = "ahmad.budi", string password = "Hospital@2026!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    public async Task<Guid> SeedDetectorAsync(
        string name = "Test Billing Detector",
        string scope = "DataIntegrity",
        string targetModule = "Billing",
        string adminStatus = "Active",
        string healthStatus = "Healthy",
        string? apiKeyHash = null,
        int expectedCadenceSeconds = 300,
        DateTime? lastRunAtUtc = null)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.Detectors (
                Id, Name, Description, Scope, TargetModule, ExpectedCadenceSeconds,
                WebhookUrl, AdminStatus, HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc, CreatedAtUtc,
                ApiKeyHash
            ) VALUES (
                @Id, @Name, 'Test detector description', @Scope, @TargetModule, @ExpectedCadenceSeconds,
                NULL, @AdminStatus, @HealthStatus, @LastRunAtUtc, SYSUTCDATETIME(), SYSUTCDATETIME(),
                @ApiKeyHash
            );
            """;

        await ExecuteSqlAsync(sql, new
        {
            Id = id,
            Name = name,
            Scope = scope,
            TargetModule = targetModule,
            AdminStatus = adminStatus,
            HealthStatus = healthStatus,
            ExpectedCadenceSeconds = expectedCadenceSeconds,
            LastRunAtUtc = lastRunAtUtc ?? DateTime.UtcNow,
            ApiKeyHash = apiKeyHash
        });

        return id;
    }
}
