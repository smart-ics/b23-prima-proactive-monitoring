using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorDetailAndRunLogEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DetectorDetailAndRunLogEndpointTests(PrIMAApiFactory factory)
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

    #region GET /api/detectors/{id}

    [Fact]
    public async Task GetDetectorById_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var detectorId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/detectors/{detectorId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDetectorById_WhenDetectorDoesNotExist_Returns404NotFoundProblemDetails()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await authClient.GetAsync($"/api/detectors/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Detail.Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task GetDetectorById_WhenDetectorExists_ReturnsCompositeDetectorDetailDto()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var now = DateTime.UtcNow;

        // 1. Seed Detector
        var detectorId = await _factory.SeedDetectorAsync(
            "Billing Anomaly Detector",
            "ProcessIntegrity",
            "Billing");

        // 2. Seed Inspection Runs (mixed outcomes across past days)
        var run1Id = Guid.NewGuid(); // Passed (2 days ago)
        var run2Id = Guid.NewGuid(); // FindingDiscovered (1 day ago)
        var run3Id = Guid.NewGuid(); // ExecutionFailed (today)
        var runOldId = Guid.NewGuid(); // Passed (40 days ago - outside 30d window)

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc)
            VALUES 
                (@RunOldId, @DetectorId, DATEADD(DAY, -40, @Now), 'Scheduled', 'Passed', 100, NULL, DATEADD(DAY, -40, @Now)),
                (@Run1Id, @DetectorId, DATEADD(DAY, -2, @Now), 'Scheduled', 'Passed', 150, NULL, DATEADD(DAY, -2, @Now)),
                (@Run2Id, @DetectorId, DATEADD(DAY, -1, @Now), 'Manual', 'FindingDiscovered', 220, NULL, DATEADD(DAY, -1, @Now)),
                (@Run3Id, @DetectorId, @Now, 'Scheduled', 'ExecutionFailed', 80, 'Database connection timeout', @Now);
            """,
            new { DetectorId = detectorId, RunOldId = runOldId, Run1Id = run1Id, Run2Id = run2Id, Run3Id = run3Id, Now = now });

        // 3. Seed Active Issues & Inactive Issues
        var activeIssue1Id = Guid.NewGuid();
        var activeIssue2Id = Guid.NewGuid();
        var resolvedIssueId = Guid.NewGuid();

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES 
                (@Active1, 'BILL:001', 'Critical Billing Mismatch', @DetectorId, 'Billing Anomaly Detector', 'ProcessIntegrity', 'Critical', 'Critical', 4, 'Open', 1, 3, NULL, DATEADD(DAY, -1, @Now), @Now, NULL, NULL, NULL, NULL),
                (@Active2, 'BILL:002', 'Medium Tariff Gap', @DetectorId, 'Billing Anomaly Detector', 'ProcessIntegrity', 'Medium', 'Medium', 2, 'Investigating', 0, 1, 'Budi Santoso', DATEADD(DAY, -2, @Now), DATEADD(DAY, -1, @Now), @Now, NULL, NULL, NULL),
                (@Resolved, 'BILL:003', 'Resolved Tariff Variance', @DetectorId, 'Billing Anomaly Detector', 'ProcessIntegrity', 'Low', 'Low', 1, 'Resolved', 0, 1, NULL, DATEADD(DAY, -5, @Now), DATEADD(DAY, -5, @Now), @Now, @Now, NULL, NULL);
            """,
            new { DetectorId = detectorId, Active1 = activeIssue1Id, Active2 = activeIssue2Id, Resolved = resolvedIssueId, Now = now });

        // 4. Seed Occurrences linked to run2
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Occurrences (Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc, Module, SourceTable, RecordKeys, TraceString)
            VALUES 
                (NEWID(), @Active1, @Run2Id, 1, DATEADD(DAY, -1, @Now), 'Billing', 'Invoices', 'INV-100', 'Trace data 1'),
                (NEWID(), @Active2, @Run2Id, 1, DATEADD(DAY, -1, @Now), 'Billing', 'Invoices', 'INV-200', 'Trace data 2');
            """,
            new { Active1 = activeIssue1Id, Active2 = activeIssue2Id, Run2Id = run2Id, Now = now });

        // Act
        var response = await authClient.GetAsync($"/api/detectors/{detectorId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(detectorId);
        dto.Name.Should().Be("Billing Anomaly Detector");
        dto.Scope.Should().Be("ProcessIntegrity");
        dto.TargetModule.Should().Be("Billing");
        dto.ExpectedCadenceSeconds.Should().Be(300);
        dto.AdminStatus.Should().Be("Active");

        // Reliability stats: 3 runs in last 30d (run1, run2, run3). 1 passed out of 3 -> 33.3% pass rate
        dto.ReliabilityStats.Should().NotBeNull();
        dto.ReliabilityStats.TotalRuns30d.Should().Be(3);
        dto.ReliabilityStats.PassRate30d.Should().Be(33.3m);
        dto.ReliabilityStats.LastSuccessfulRunAtUtc.Should().NotBeNull();
        dto.ReliabilityStats.LastFailureAtUtc.Should().NotBeNull();

        // Recent runs: top runs ordered by timestamp desc
        dto.RecentRuns.Should().NotBeNull();
        dto.RecentRuns.Should().HaveCount(4);
        dto.RecentRuns[0].Outcome.Should().Be("ExecutionFailed");
        dto.RecentRuns[0].FailureReason.Should().Be("Database connection timeout");
        dto.RecentRuns[0].FindingsCount.Should().Be(0);

        dto.RecentRuns[1].Outcome.Should().Be("FindingDiscovered");
        dto.RecentRuns[1].TriggerSource.Should().Be("Manual");
        dto.RecentRuns[1].FindingsCount.Should().Be(2);

        // Active issues: only Open and Investigating issues, ordered by SeverityRank DESC
        dto.ActiveIssues.Should().NotBeNull();
        dto.ActiveIssues.Should().HaveCount(2);
        dto.ActiveIssues[0].CorrelationKey.Should().Be("BILL:001");
        dto.ActiveIssues[0].CurrentSeverity.Should().Be("Critical");
        dto.ActiveIssues[0].SeverityRank.Should().Be(4);
        dto.ActiveIssues[1].CorrelationKey.Should().Be("BILL:002");
        dto.ActiveIssues[1].CurrentSeverity.Should().Be("Medium");
    }

    #endregion

    #region GET /api/detectors/{id}/runs

    [Fact]
    public async Task GetInspectionRuns_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var detectorId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/detectors/{detectorId}/runs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInspectionRuns_WhenDetectorDoesNotExist_Returns404NotFoundProblemDetails()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await authClient.GetAsync($"/api/detectors/{nonExistentId}/runs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetInspectionRuns_WithFiltersAndPagination_ReturnsFilteredRuns()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var now = DateTime.UtcNow;

        var detectorId = await _factory.SeedDetectorAsync(
            "Pharmacy Inventory Auditor",
            "DataIntegrity",
            "Pharmacy");

        var issueId = Guid.NewGuid();
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
            ) VALUES (
                @IssueId, 'PHARM:EXP:01', 'Expired Stock Present', @DetectorId, 'Pharmacy Inventory Auditor', 'DataIntegrity',
                'High', 'High', 3, 'Open', 0, 1, @Now, @Now
            );
            """,
            new { IssueId = issueId, DetectorId = detectorId, Now = now });

        // Seed 5 runs across the past 5 days
        var runIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var rid = Guid.NewGuid();
            runIds.Add(rid);
            var timestamp = now.AddDays(-i);
            var outcome = i switch
            {
                0 => "ExecutionFailed",
                1 => "FindingDiscovered",
                _ => "Passed"
            };
            var trigger = (i % 2 == 0) ? "Scheduled" : "Manual";
            var failureReason = outcome == "ExecutionFailed" ? "Network drop" : null;

            await _factory.ExecuteSqlAsync("""
                INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc)
                VALUES (@Id, @DetectorId, @Timestamp, @Trigger, @Outcome, 50, @FailureReason, @Timestamp);
                """,
                new { Id = rid, DetectorId = detectorId, Timestamp = timestamp, Trigger = trigger, Outcome = outcome, FailureReason = failureReason });
        }

        // Link an occurrence to runIds[1] (FindingDiscovered)
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Occurrences (Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc, Module, TraceString)
            VALUES (NEWID(), @IssueId, @RunId, 1, @Now, 'Pharmacy', 'Finding trace notes');
            """,
            new { IssueId = issueId, RunId = runIds[1], Now = now.AddDays(-1) });

        // Act 1: Default query (last 7 days) -> all 5 runs
        var responseAll = await authClient.GetAsync($"/api/detectors/{detectorId}/runs");
        responseAll.StatusCode.Should().Be(HttpStatusCode.OK);

        var logAll = await responseAll.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        logAll.Should().NotBeNull();
        logAll!.TotalCount.Should().Be(5);
        logAll.Runs.Should().HaveCount(5);

        // Check discovered issues mapping on runIds[1]
        var findingRun = logAll.Runs.First(r => r.Id == runIds[1]);
        findingRun.Outcome.Should().Be("FindingDiscovered");
        findingRun.FindingsCount.Should().Be(1);
        findingRun.DiscoveredIssueIds.Should().Contain(issueId);

        // Act 2: Filter by Outcome = "Passed"
        var responsePassed = await authClient.GetAsync($"/api/detectors/{detectorId}/runs?outcome=Passed");
        responsePassed.StatusCode.Should().Be(HttpStatusCode.OK);

        var logPassed = await responsePassed.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        logPassed.Should().NotBeNull();
        logPassed!.TotalCount.Should().Be(3);
        logPassed.Runs.Should().HaveCount(3);
        logPassed.Runs.Should().OnlyContain(r => r.Outcome == "Passed");

        // Act 3: Filter by TriggerSource = "Manual"
        var responseManual = await authClient.GetAsync($"/api/detectors/{detectorId}/runs?triggerSource=Manual");
        responseManual.StatusCode.Should().Be(HttpStatusCode.OK);

        var logManual = await responseManual.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        logManual.Should().NotBeNull();
        logManual!.TotalCount.Should().Be(2);
        logManual.Runs.Should().HaveCount(2);
        logManual.Runs.Should().OnlyContain(r => r.TriggerSource == "Manual");

        // Act 4: Pagination (Limit = 2, Offset = 0)
        var responsePage1 = await authClient.GetAsync($"/api/detectors/{detectorId}/runs?limit=2&offset=0");
        responsePage1.StatusCode.Should().Be(HttpStatusCode.OK);

        var logPage1 = await responsePage1.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        logPage1.Should().NotBeNull();
        logPage1!.TotalCount.Should().Be(5);
        logPage1.Runs.Should().HaveCount(2);

        // Pagination (Limit = 2, Offset = 2)
        var responsePage2 = await authClient.GetAsync($"/api/detectors/{detectorId}/runs?limit=2&offset=2");
        responsePage2.StatusCode.Should().Be(HttpStatusCode.OK);

        var logPage2 = await responsePage2.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        logPage2.Should().NotBeNull();
        logPage2!.TotalCount.Should().Be(5);
        logPage2.Runs.Should().HaveCount(2);
        logPage2.Runs[0].Id.Should().NotBe(logPage1.Runs[0].Id);
    }

    #endregion

    #region OpenAPI & Swagger Integration

    [Fact]
    public async Task SwaggerDocument_ContainsP3S08EndpointsAndSchemas()
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

        paths!.ContainsKey("/api/detectors/{id}").Should().BeTrue("GET /api/detectors/{id} should be documented in Swagger");
        paths!.ContainsKey("/api/detectors/{id}/runs").Should().BeTrue("GET /api/detectors/{id}/runs should be documented in Swagger");

        var schemas = rootNode["components"]?["schemas"]?.AsObject();
        schemas.Should().NotBeNull();

        schemas!.ContainsKey("DetectorDetailDto").Should().BeTrue("DetectorDetailDto should be in Swagger components");
        schemas!.ContainsKey("DetectorReliabilityStatsDto").Should().BeTrue("DetectorReliabilityStatsDto should be in Swagger components");
        schemas!.ContainsKey("RecentRunSummaryDto").Should().BeTrue("RecentRunSummaryDto should be in Swagger components");
        schemas!.ContainsKey("LinkedActiveIssueDto").Should().BeTrue("LinkedActiveIssueDto should be in Swagger components");
        schemas!.ContainsKey("InspectionRunLogResponse").Should().BeTrue("InspectionRunLogResponse should be in Swagger components");
        schemas!.ContainsKey("InspectionRunDetailDto").Should().BeTrue("InspectionRunDetailDto should be in Swagger components");
    }

    #endregion
}
