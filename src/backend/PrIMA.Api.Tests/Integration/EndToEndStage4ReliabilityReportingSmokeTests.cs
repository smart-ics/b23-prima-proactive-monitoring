using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PrIMA.Api.Common;
using PrIMA.Api.Features.History.GetHistoricalIssues;
using PrIMA.Api.Features.History.GetReliabilityKpi;
using PrIMA.Api.Seeding;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class EndToEndStage4ReliabilityReportingSmokeTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndToEndStage4ReliabilityReportingSmokeTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedDemoData"] = "true",
                ["OperationalTimeZone"] = "Asia/Jakarta"
            })
            .Build();

        var seeder = new DevelopmentDemoDataSeeder(
            env,
            new DefaultSqlConnectionFactory(PrIMAApiFactory.TestConnectionString),
            config,
            NullLogger<DevelopmentDemoDataSeeder>.Instance);

        await seeder.SeedAsync();
    }

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
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _factory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private static (DateTime FromUtc, DateTime ToUtc) GetWibTodayRange()
    {
        var wib = GetWibTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wib);
        var localStart = localNow.Date;
        var localEnd = localStart.AddDays(1).AddMilliseconds(-1);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, wib),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, wib));
    }

    private static (DateTime FromUtc, DateTime ToUtc, int LocalDayOfMonth) GetWibThisMonthRange()
    {
        var wib = GetWibTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wib);
        var localMonthStart = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localNow.Date.AddDays(1).AddMilliseconds(-1);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localMonthStart, wib),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, wib),
            localNow.Day);
    }

    private static TimeZoneInfo GetWibTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
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

    [Fact]
    public async Task EndToEnd_Stage4ReliabilityReporting_CompleteFlow_VerifiesKpiBreakdownsLedgerAndLimitClamping()
    {
        // -------------------------------------------------------------------------
        // STEP 1: Authenticate Test Officer
        // -------------------------------------------------------------------------
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi");

        var (todayFromUtc, todayToUtc) = GetWibTodayRange();
        var (monthFromUtc, monthToUtc, localDayOfMonth) = GetWibThisMonthRange();

        // -------------------------------------------------------------------------
        // STEP 2: GET /api/history/reliability-kpi for "Today"
        // -------------------------------------------------------------------------
        var todayKpiUrl = $"/api/history/reliability-kpi?fromUtc={Uri.EscapeDataString(todayFromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(todayToUtc.ToString("O"))}";
        var todayKpiResponse = await authClient.GetAsync(todayKpiUrl);
        todayKpiResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var todayKpi = await todayKpiResponse.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        todayKpi.Should().NotBeNull();
        todayKpi!.TotalIssuesCreated.Should().BeGreaterOrEqualTo(2);
        todayKpi.ResolutionRatePercentage.Should().BeGreaterThan(0);
        todayKpi.AvgTimeToAcknowledgeHours.Should().NotBeNull();
        todayKpi.AvgTimeToAcknowledgeHours!.Value.Should().BeGreaterThan(0);
        todayKpi.AvgTimeToResolveHours.Should().NotBeNull();
        todayKpi.AvgTimeToResolveHours!.Value.Should().BeGreaterThan(0);
        todayKpi.DetectorPassRatePercentage.Should().BeGreaterThan(0).And.BeLessOrEqualTo(100);
        todayKpi.ReopenedIssuesCount.Should().BeGreaterOrEqualTo(1);
        todayKpi.ScopeBreakdown.Should().NotBeEmpty();
        todayKpi.ScopeBreakdown.Sum(b => b.Count).Should().Be(todayKpi.TotalIssuesCreated);
        todayKpi.SeverityBreakdown.Should().NotBeEmpty();
        todayKpi.SeverityBreakdown.Sum(b => b.Count).Should().Be(todayKpi.TotalIssuesCreated);

        // -------------------------------------------------------------------------
        // STEP 3: GET /api/history/reliability-kpi for "This Month"
        // -------------------------------------------------------------------------
        var monthKpiUrl = $"/api/history/reliability-kpi?fromUtc={Uri.EscapeDataString(monthFromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(monthToUtc.ToString("O"))}";
        var monthKpiResponse = await authClient.GetAsync(monthKpiUrl);
        monthKpiResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var monthKpi = await monthKpiResponse.Content.ReadFromJsonAsync<ReliabilityKpiResponse>(JsonOptions);
        monthKpi.Should().NotBeNull();
        monthKpi!.TotalIssuesCreated.Should().BeGreaterOrEqualTo(todayKpi.TotalIssuesCreated);
        monthKpi.ResolutionRatePercentage.Should().BeGreaterThan(0).And.BeLessOrEqualTo(100);
        monthKpi.AvgTimeToAcknowledgeHours.Should().NotBeNull();
        monthKpi.AvgTimeToResolveHours.Should().NotBeNull();
        monthKpi.DetectorPassRatePercentage.Should().BeGreaterThan(0).And.BeLessOrEqualTo(100);
        monthKpi.ReopenedIssuesCount.Should().BeGreaterOrEqualTo(1);
        monthKpi.ScopeBreakdown.Select(s => s.Category).Should().Contain("DataIntegrity");
        monthKpi.ScopeBreakdown.Sum(b => b.Count).Should().Be(monthKpi.TotalIssuesCreated);
        monthKpi.SeverityBreakdown.Select(s => s.Category).Should().Contain("High");
        monthKpi.SeverityBreakdown.Sum(b => b.Count).Should().Be(monthKpi.TotalIssuesCreated);

        if (localDayOfMonth >= 11)
        {
            monthKpi.ScopeBreakdown.Select(s => s.Category).Should().Contain(new[]
            {
                "DataIntegrity",
                "ProcessIntegrity",
                "Infrastructure"
            });
            monthKpi.SeverityBreakdown.Select(s => s.Category).Should().Contain(new[]
            {
                "Low",
                "Medium",
                "High",
                "Critical"
            });
        }

        // -------------------------------------------------------------------------
        // STEP 4: GET /api/history/issues?status=Resolved with date range
        // -------------------------------------------------------------------------
        var todayResolvedUrl = $"/api/history/issues?fromUtc={Uri.EscapeDataString(todayFromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(todayToUtc.ToString("O"))}&status=Resolved";
        var todayResolvedResponse = await authClient.GetAsync(todayResolvedUrl);
        todayResolvedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var todayResolved = await todayResolvedResponse.Content.ReadFromJsonAsync<HistoricalIssuesResponse>(JsonOptions);
        todayResolved.Should().NotBeNull();
        todayResolved!.Items.Should().NotBeEmpty();
        todayResolved.Items.Should().OnlyContain(i =>
            i.Status == "Resolved" &&
            i.ResolvedAtUtc.HasValue &&
            i.ResolvedAtUtc.Value >= todayFromUtc &&
            i.ResolvedAtUtc.Value <= todayToUtc);
        todayResolved.Items.Select(i => i.Id).Should().Contain(new[]
        {
            DevelopmentDemoDataSeeder.ResolvedIssueId,
            DevelopmentDemoDataSeeder.ResolvedTodayProcessIssueId
        });

        var monthResolvedUrl = $"/api/history/issues?fromUtc={Uri.EscapeDataString(monthFromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(monthToUtc.ToString("O"))}&status=Resolved";
        var monthResolvedResponse = await authClient.GetAsync(monthResolvedUrl);
        monthResolvedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var monthResolved = await monthResolvedResponse.Content.ReadFromJsonAsync<HistoricalIssuesResponse>(JsonOptions);
        monthResolved.Should().NotBeNull();
        monthResolved!.Items.Should().OnlyContain(i => i.Status == "Resolved" && i.ResolvedAtUtc.HasValue);

        if (localDayOfMonth >= 11)
        {
            monthResolved.Items.Select(i => i.Id).Should().Contain(new[]
            {
                DevelopmentDemoDataSeeder.ResolvedIssueId,
                DevelopmentDemoDataSeeder.ResolvedTodayProcessIssueId,
                DevelopmentDemoDataSeeder.ResolvedLastWeekPharmacyIssueId,
                DevelopmentDemoDataSeeder.ResolvedLastWeekInfraIssueId
            });
        }

        // -------------------------------------------------------------------------
        // STEP 5: Verify Limit Clamping when requesting limit=2000
        // -------------------------------------------------------------------------
        var clampedUrl = $"/api/history/issues?fromUtc={Uri.EscapeDataString(monthFromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(monthToUtc.ToString("O"))}&limit=2000";
        var clampedResponse = await authClient.GetAsync(clampedUrl);
        clampedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var clampedPayload = await clampedResponse.Content.ReadFromJsonAsync<HistoricalIssuesResponse>(JsonOptions);
        clampedPayload.Should().NotBeNull();
        clampedPayload!.ClampedLimit.Should().Be(1000);
        clampedPayload.TotalReturned.Should().Be(clampedPayload.Items.Count);
        clampedPayload.TotalReturned.Should().BeLessOrEqualTo(1000);
        clampedPayload.Items.Should().NotBeEmpty();
    }
}
