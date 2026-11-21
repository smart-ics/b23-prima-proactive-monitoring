using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.History.GetHistoricalIssues;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class HistoricalIssuesEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HistoricalIssuesEndpointTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await _factory.GetAuthTokenAsync(_client);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private async Task SeedIssueAsync(
        Guid id,
        Guid detectorId,
        string detectorName,
        string title,
        string correlationKey,
        string scope = "DataIntegrity",
        string baseSeverity = "High",
        string currentSeverity = "High",
        byte severityRank = 3,
        string status = "Open",
        int escalationLevel = 0,
        int occurrenceCount = 1,
        string? currentAssignee = null,
        DateTime? createdAtUtc = null,
        DateTime? lastOccurrenceAtUtc = null,
        DateTime? acknowledgedAtUtc = null,
        DateTime? resolvedAtUtc = null,
        DateTime? dismissedAtUtc = null,
        DateTime? reopenedAtUtc = null)
    {
        var created = createdAtUtc ?? DateTime.UtcNow;
        var lastOccurrence = lastOccurrenceAtUtc ?? created;

        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope, BaseSeverity,
                CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount,
                CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc, AcknowledgedAtUtc,
                ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @CorrelationKey, @Title, @DetectorId, @DetectorName, @Scope, @BaseSeverity,
                @CurrentSeverity, @SeverityRank, @Status, @EscalationLevel, @OccurrenceCount,
                @CurrentAssignee, @CreatedAtUtc, @LastOccurrenceAtUtc, @AcknowledgedAtUtc,
                @ResolvedAtUtc, @DismissedAtUtc, @ReopenedAtUtc
            );
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("CorrelationKey", correlationKey);
        parameters.Add("Title", title);
        parameters.Add("DetectorId", detectorId);
        parameters.Add("DetectorName", detectorName);
        parameters.Add("Scope", scope);
        parameters.Add("BaseSeverity", baseSeverity);
        parameters.Add("CurrentSeverity", currentSeverity);
        parameters.Add("SeverityRank", severityRank);
        parameters.Add("Status", status);
        parameters.Add("EscalationLevel", escalationLevel);
        parameters.Add("OccurrenceCount", occurrenceCount);
        parameters.Add("CurrentAssignee", currentAssignee);
        parameters.Add("CreatedAtUtc", created, DbType.DateTime2);
        parameters.Add("LastOccurrenceAtUtc", lastOccurrence, DbType.DateTime2);
        parameters.Add("AcknowledgedAtUtc", acknowledgedAtUtc, DbType.DateTime2);
        parameters.Add("ResolvedAtUtc", resolvedAtUtc, DbType.DateTime2);
        parameters.Add("DismissedAtUtc", dismissedAtUtc, DbType.DateTime2);
        parameters.Add("ReopenedAtUtc", reopenedAtUtc, DbType.DateTime2);

        await _factory.ExecuteSqlAsync(sql, parameters);
    }

    private static string Iso(DateTime dt) => dt.ToUniversalTime().ToString("O");
    private static string IsoMillis(DateTime dt) => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    [Fact]
    public async Task GetHistoricalIssues_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var from = Iso(DateTime.UtcNow.AddDays(-1));
        var to = Iso(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync($"/api/history/issues?fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(to)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistoricalIssues_WhenFromUtcGreaterThanToUtc_Returns422ValidationProblemDetails()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var from = Iso(new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));
        var to = Iso(new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc));

        // Act
        var response = await authClient.GetAsync(
            $"/api/history/issues?fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(to)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Errors.Should().ContainKey("FromUtc");
    }

    [Fact]
    public async Task GetHistoricalIssues_WhenRequiredDateParametersMissing_Returns400BadRequest()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        // Act
        var response = await authClient.GetAsync("/api/history/issues");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalIssues_StatusAwareLifecycleDateFiltering_FiltersByLifecycleTimestamp()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync("Ledger Audit Detector", "DataIntegrity", "Billing");

        // Window anchored around WIB boundary in UTC: 2026-11-21 17:00:00Z to 2026-11-22 16:59:59.999Z
        var windowFrom = new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
        var windowTo = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);

        // 1. Resolved inside window, created 10 days earlier (must appear for Status=Resolved, NOT for Status=All)
        var resolvedTodayOldCreatedId = Guid.NewGuid();
        await SeedIssueAsync(
            resolvedTodayOldCreatedId,
            detectorId,
            "Ledger Audit Detector",
            "Resolved Today Created Earlier",
            "LEDGER:RES-01",
            status: nameof(IssueStatus.Resolved),
            escalationLevel: 1,
            occurrenceCount: 4,
            currentAssignee: "ahmad.budi",
            createdAtUtc: windowFrom.AddDays(-10),
            lastOccurrenceAtUtc: windowFrom.AddHours(2),
            acknowledgedAtUtc: windowFrom.AddDays(-9),
            resolvedAtUtc: windowFrom.AddHours(3));

        // 2. Resolved outside window (before window), created before window (must NOT appear for Status=Resolved)
        var resolvedOutsideId = Guid.NewGuid();
        await SeedIssueAsync(
            resolvedOutsideId,
            detectorId,
            "Ledger Audit Detector",
            "Resolved Last Week",
            "LEDGER:RES-OLD",
            status: nameof(IssueStatus.Resolved),
            createdAtUtc: windowFrom.AddDays(-12),
            lastOccurrenceAtUtc: windowFrom.AddDays(-8),
            resolvedAtUtc: windowFrom.AddDays(-7));

        // 3. Dismissed inside window, created 15 days earlier (must appear for Status=Dismissed, NOT for Status=All)
        var dismissedTodayId = Guid.NewGuid();
        await SeedIssueAsync(
            dismissedTodayId,
            detectorId,
            "Ledger Audit Detector",
            "Dismissed Today",
            "LEDGER:DISM-01",
            status: nameof(IssueStatus.Dismissed),
            createdAtUtc: windowFrom.AddDays(-15),
            lastOccurrenceAtUtc: windowFrom.AddHours(1),
            dismissedAtUtc: windowFrom.AddHours(4));

        // 4. Reopened inside window, created 20 days earlier (must appear for Status=Reopened, NOT for Status=All)
        var reopenedTodayId = Guid.NewGuid();
        await SeedIssueAsync(
            reopenedTodayId,
            detectorId,
            "Ledger Audit Detector",
            "Reopened Today",
            "LEDGER:REOPEN-01",
            status: nameof(IssueStatus.Reopened),
            createdAtUtc: windowFrom.AddDays(-20),
            lastOccurrenceAtUtc: windowFrom.AddHours(5),
            reopenedAtUtc: windowFrom.AddHours(5));

        // 5. Open created inside window (must appear for Status=Open and Status=All)
        var openTodayId = Guid.NewGuid();
        await SeedIssueAsync(
            openTodayId,
            detectorId,
            "Ledger Audit Detector",
            "Open Created Today",
            "LEDGER:OPEN-01",
            status: nameof(IssueStatus.Open),
            createdAtUtc: windowFrom.AddMinutes(30),
            lastOccurrenceAtUtc: windowFrom.AddHours(6));

        var fromParam = Uri.EscapeDataString(Iso(windowFrom));
        var toParam = Uri.EscapeDataString(Iso(windowTo));

        // Act & Assert 1: Status=Resolved filters by ResolvedAtUtc and preserves all DTO properties
        var resolvedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Resolved", JsonOptions);
        resolvedResp.Should().NotBeNull();
        resolvedResp!.TotalReturned.Should().Be(1);
        resolvedResp.ClampedLimit.Should().Be(500);
        resolvedResp.Items.Should().ContainSingle(x => x.Id == resolvedTodayOldCreatedId);

        var item = resolvedResp.Items[0];
        item.CorrelationKey.Should().Be("LEDGER:RES-01");
        item.Title.Should().Be("Resolved Today Created Earlier");
        item.DetectorId.Should().Be(detectorId);
        item.DetectorName.Should().Be("Ledger Audit Detector");
        item.Scope.Should().Be("DataIntegrity");
        item.CurrentSeverity.Should().Be("High");
        item.SeverityRank.Should().Be(3);
        item.Status.Should().Be("Resolved");
        item.EscalationLevel.Should().Be(1);
        item.OccurrenceCount.Should().Be(4);
        item.CurrentAssignee.Should().Be("ahmad.budi");
        item.CreatedAtUtc.Should().BeCloseTo(windowFrom.AddDays(-10), TimeSpan.FromSeconds(1));
        item.AcknowledgedAtUtc.Should().BeCloseTo(windowFrom.AddDays(-9), TimeSpan.FromSeconds(1));
        item.ResolvedAtUtc.Should().BeCloseTo(windowFrom.AddHours(3), TimeSpan.FromSeconds(1));
        item.DismissedAtUtc.Should().BeNull();
        item.ReopenedAtUtc.Should().BeNull();
        item.LastOccurrenceAtUtc.Should().BeCloseTo(windowFrom.AddHours(2), TimeSpan.FromSeconds(1));

        // Act & Assert 2: Status=Dismissed filters by DismissedAtUtc
        var dismissedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Dismissed", JsonOptions);
        dismissedResp!.Items.Should().ContainSingle(x => x.Id == dismissedTodayId);

        // Act & Assert 3: Status=Reopened filters by ReopenedAtUtc
        var reopenedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Reopened", JsonOptions);
        reopenedResp!.Items.Should().ContainSingle(x => x.Id == reopenedTodayId);

        // Act & Assert 4: Status=All (and omitted status) filters by CreatedAtUtc
        var allResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=All", JsonOptions);
        allResp!.Items.Should().ContainSingle(x => x.Id == openTodayId);

        var openResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Open", JsonOptions);
        openResp!.Items.Should().ContainSingle(x => x.Id == openTodayId);

        // Act & Assert 5: Case-insensitive status=resolved + inclusive boundary check
        var resolvedLowerResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={Uri.EscapeDataString(Iso(windowFrom.AddHours(3)))}&toUtc={Uri.EscapeDataString(Iso(windowFrom.AddHours(3)))}&status=resolved",
            JsonOptions);
        resolvedLowerResp!.Items.Should().ContainSingle(x => x.Id == resolvedTodayOldCreatedId);
    }

    [Fact]
    public async Task GetHistoricalIssues_WibMillisecondBoundaryPrecision_IncludesExact999ZAndExcludesNextDayMidnightWib()
    {
        // Arrange: 2026-11-22 00:00:00.000 WIB to 23:59:59.999 WIB
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync("WIB Boundary Detector", "DataIntegrity", "Billing");

        var fromUtc = new DateTime(2026, 11, 21, 17, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc);
        var justBeforeFrom = fromUtc.AddMilliseconds(-1); // 2026-11-21T16:59:59.999Z (prev day 23:59:59.999 WIB)
        var nextDayMidnightWib = toUtc.AddMilliseconds(1); // 2026-11-22T17:00:00.000Z (next day 00:00:00.000 WIB)

        // CreatedAtUtc boundaries (Status=All)
        var createdAtStartId = Guid.NewGuid();
        var createdAtEndId = Guid.NewGuid();
        var createdBeforeStartId = Guid.NewGuid();
        var createdAtNextDayMidnightId = Guid.NewGuid();

        await SeedIssueAsync(createdAtStartId, detectorId, "WIB Boundary Detector", "Created At Start", "BND:CR:START",
            status: nameof(IssueStatus.Open), createdAtUtc: fromUtc);
        await SeedIssueAsync(createdAtEndId, detectorId, "WIB Boundary Detector", "Created At End 999ms", "BND:CR:END",
            status: nameof(IssueStatus.Open), createdAtUtc: toUtc);
        await SeedIssueAsync(createdBeforeStartId, detectorId, "WIB Boundary Detector", "Created Before Start", "BND:CR:BEFORE",
            status: nameof(IssueStatus.Open), createdAtUtc: justBeforeFrom);
        await SeedIssueAsync(createdAtNextDayMidnightId, detectorId, "WIB Boundary Detector", "Created Next Day Midnight WIB", "BND:CR:NEXT",
            status: nameof(IssueStatus.Open), createdAtUtc: nextDayMidnightWib);

        // ResolvedAtUtc boundaries (Status=Resolved; CreatedAtUtc earlier so excluded from Status=All)
        var resolvedAtStartId = Guid.NewGuid();
        var resolvedAtEndId = Guid.NewGuid();
        var resolvedBeforeStartId = Guid.NewGuid();
        var resolvedAtNextDayMidnightId = Guid.NewGuid();

        await SeedIssueAsync(resolvedAtStartId, detectorId, "WIB Boundary Detector", "Resolved At Start", "BND:RS:START",
            status: nameof(IssueStatus.Resolved), createdAtUtc: fromUtc.AddDays(-5), resolvedAtUtc: fromUtc);
        await SeedIssueAsync(resolvedAtEndId, detectorId, "WIB Boundary Detector", "Resolved At End 999ms", "BND:RS:END",
            status: nameof(IssueStatus.Resolved), createdAtUtc: fromUtc.AddDays(-5), resolvedAtUtc: toUtc);
        await SeedIssueAsync(resolvedBeforeStartId, detectorId, "WIB Boundary Detector", "Resolved Before Start", "BND:RS:BEFORE",
            status: nameof(IssueStatus.Resolved), createdAtUtc: fromUtc.AddDays(-5), resolvedAtUtc: justBeforeFrom);
        await SeedIssueAsync(resolvedAtNextDayMidnightId, detectorId, "WIB Boundary Detector", "Resolved Next Day Midnight WIB", "BND:RS:NEXT",
            status: nameof(IssueStatus.Resolved), createdAtUtc: fromUtc.AddDays(-5), resolvedAtUtc: nextDayMidnightWib);

        // DismissedAtUtc & ReopenedAtUtc end boundaries (.999Z vs .000Z next day WIB)
        var dismissedAtEndId = Guid.NewGuid();
        var dismissedAtNextDayId = Guid.NewGuid();
        await SeedIssueAsync(dismissedAtEndId, detectorId, "WIB Boundary Detector", "Dismissed At End 999ms", "BND:DM:END",
            status: nameof(IssueStatus.Dismissed), createdAtUtc: fromUtc.AddDays(-5), dismissedAtUtc: toUtc);
        await SeedIssueAsync(dismissedAtNextDayId, detectorId, "WIB Boundary Detector", "Dismissed Next Day Midnight WIB", "BND:DM:NEXT",
            status: nameof(IssueStatus.Dismissed), createdAtUtc: fromUtc.AddDays(-5), dismissedAtUtc: nextDayMidnightWib);

        var reopenedAtEndId = Guid.NewGuid();
        var reopenedAtNextDayId = Guid.NewGuid();
        await SeedIssueAsync(reopenedAtEndId, detectorId, "WIB Boundary Detector", "Reopened At End 999ms", "BND:RO:END",
            status: nameof(IssueStatus.Reopened), createdAtUtc: fromUtc.AddDays(-5), reopenedAtUtc: toUtc);
        await SeedIssueAsync(reopenedAtNextDayId, detectorId, "WIB Boundary Detector", "Reopened Next Day Midnight WIB", "BND:RO:NEXT",
            status: nameof(IssueStatus.Reopened), createdAtUtc: fromUtc.AddDays(-5), reopenedAtUtc: nextDayMidnightWib);

        var fromParam = Uri.EscapeDataString(IsoMillis(fromUtc));
        var toParam = Uri.EscapeDataString(IsoMillis(toUtc));

        // Act & Assert 1: Status=All (CreatedAtUtc)
        var allResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=All", JsonOptions);
        allResp!.Items.Select(x => x.Id).Should().BeEquivalentTo(new[] { createdAtStartId, createdAtEndId });

        // Act & Assert 2: Status=Resolved (ResolvedAtUtc)
        var resolvedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Resolved", JsonOptions);
        resolvedResp!.Items.Select(x => x.Id).Should().BeEquivalentTo(new[] { resolvedAtStartId, resolvedAtEndId });

        // Act & Assert 3: Status=Dismissed (DismissedAtUtc)
        var dismissedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Dismissed", JsonOptions);
        dismissedResp!.Items.Select(x => x.Id).Should().BeEquivalentTo(new[] { dismissedAtEndId });

        // Act & Assert 4: Status=Reopened (ReopenedAtUtc)
        var reopenedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&status=Reopened", JsonOptions);
        reopenedResp!.Items.Select(x => x.Id).Should().BeEquivalentTo(new[] { reopenedAtEndId });
    }

    [Fact]
    public async Task GetHistoricalIssues_LimitClamping_ClampsBetween1And1000AndDefaultsTo500()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorId = await _factory.SeedDetectorAsync("Limit Test Detector");
        var baseTime = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await SeedIssueAsync(id1, detectorId, "Limit Test Detector", "Issue 1 (Oldest)", "LIM:1",
            createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(1));
        await SeedIssueAsync(id2, detectorId, "Limit Test Detector", "Issue 2 (Middle)", "LIM:2",
            createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(2));
        await SeedIssueAsync(id3, detectorId, "Limit Test Detector", "Issue 3 (Newest)", "LIM:3",
            createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(3));

        var fromParam = Uri.EscapeDataString(Iso(baseTime.AddHours(-1)));
        var toParam = Uri.EscapeDataString(Iso(baseTime.AddHours(1)));

        // Act & Assert 1: Default limit -> 500
        var defaultResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}", JsonOptions);
        defaultResp!.ClampedLimit.Should().Be(500);
        defaultResp.TotalReturned.Should().Be(3);
        defaultResp.Items.Select(x => x.Id).Should().ContainInOrder(id3, id2, id1);

        // Act & Assert 2: Negative / zero limit -> clamped to 1
        var lowResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&limit=-5", JsonOptions);
        lowResp!.ClampedLimit.Should().Be(1);
        lowResp.TotalReturned.Should().Be(1);
        lowResp.Items.Should().ContainSingle(x => x.Id == id3);

        // Act & Assert 3: Over-max limit (2000) -> clamped to 1000
        var highResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&limit=2000", JsonOptions);
        highResp!.ClampedLimit.Should().Be(1000);
        highResp.TotalReturned.Should().Be(3);

        // Act & Assert 4: Explicit limit within range (2)
        var exactResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&limit=2", JsonOptions);
        exactResp!.ClampedLimit.Should().Be(2);
        exactResp.TotalReturned.Should().Be(2);
        exactResp.Items.Select(x => x.Id).Should().ContainInOrder(id3, id2);
    }

    [Fact]
    public async Task GetHistoricalIssues_MultiFilterAndSearch_ReturnsMatchingIssuesOrderedByLastOccurrenceDesc()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var detectorBillingId = await _factory.SeedDetectorAsync("Billing Variance Detector", "DataIntegrity", "Billing");
        var detectorAdtId = await _factory.SeedDetectorAsync("ADT Queue Monitor", "ProcessIntegrity", "ADT");
        var baseTime = new DateTime(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);

        var issueA = Guid.NewGuid();
        var issueB = Guid.NewGuid();
        var issueC = Guid.NewGuid();

        await SeedIssueAsync(
            issueA, detectorBillingId, "Billing Variance Detector", "Invoice 100% Drift Detected", "BILL:DRIFT:001",
            scope: "DataIntegrity", currentSeverity: "Critical", severityRank: SeverityRank.Critical,
            status: "Investigating", createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(10));

        await SeedIssueAsync(
            issueB, detectorAdtId, "ADT Queue Monitor", "Admission Transfer Delay", "ADT:DELAY:002",
            scope: "ProcessIntegrity", currentSeverity: "High", severityRank: SeverityRank.High,
            status: "Open", createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(30));

        await SeedIssueAsync(
            issueC, detectorBillingId, "Billing Variance Detector", "Tariff Rounding Notice", "BILL:TARIFF:003",
            scope: "DataIntegrity", currentSeverity: "Critical", severityRank: SeverityRank.Critical,
            status: "Open", createdAtUtc: baseTime, lastOccurrenceAtUtc: baseTime.AddMinutes(20));

        var fromParam = Uri.EscapeDataString(Iso(baseTime.AddHours(-1)));
        var toParam = Uri.EscapeDataString(Iso(baseTime.AddHours(2)));

        // Act & Assert 1: Scope filter
        var scopeResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&scope=ProcessIntegrity", JsonOptions);
        scopeResp!.Items.Should().ContainSingle(x => x.Id == issueB);

        // Act & Assert 2: DetectorId + Severity + Status combined
        var combinedResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&detectorId={detectorBillingId}&severity=Critical&status=Open",
            JsonOptions);
        combinedResp!.Items.Should().ContainSingle(x => x.Id == issueC);

        // Act & Assert 3: Search by Title (including literal '%')
        var searchTitleResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&search={Uri.EscapeDataString("100%")}",
            JsonOptions);
        searchTitleResp!.Items.Should().ContainSingle(x => x.Id == issueA);

        // Act & Assert 4: Search by DetectorName
        var searchDetectorResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&search=Queue+Monitor",
            JsonOptions);
        searchDetectorResp!.Items.Should().ContainSingle(x => x.Id == issueB);

        // Act & Assert 5: Search by CorrelationKey & verify LastOccurrenceAtUtc DESC order across matches
        var searchKeyResp = await authClient.GetFromJsonAsync<HistoricalIssuesResponse>(
            $"/api/history/issues?fromUtc={fromParam}&toUtc={toParam}&search=BILL:",
            JsonOptions);
        searchKeyResp!.Items.Select(x => x.Id).Should().ContainInOrder(issueC, issueA);
    }

    [Fact]
    public async Task GetHistoricalIssues_EmptyResultSet_Returns200WithEmptyItems()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var from = Iso(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var to = Iso(new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var response = await authClient.GetAsync(
            $"/api/history/issues?fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(to)}&status=Resolved&limit=250");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HistoricalIssuesResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
        body.TotalReturned.Should().Be(0);
        body.ClampedLimit.Should().Be(250);
    }

    [Fact]
    public async Task SwaggerDocument_ContainsHistoricalIssuesEndpointAndSchemas()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var rootNode = JsonNode.Parse(content);
        rootNode.Should().NotBeNull();

        var paths = rootNode!["paths"]?.AsObject();
        paths.Should().NotBeNull();
        paths!.ContainsKey("/api/history/issues").Should().BeTrue();

        var schemas = rootNode["components"]?["schemas"]?.AsObject();
        schemas.Should().NotBeNull();
        schemas!.ContainsKey("HistoricalIssuesResponse").Should().BeTrue();
        schemas!.ContainsKey("HistoricalIssueItemDto").Should().BeTrue();
    }
}
