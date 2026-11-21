using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetDetectorList;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Features.Detectors.RegisterDetector;
using PrIMA.Api.Features.Detectors.ToggleDetectorStatus;
using PrIMA.Api.Features.Detectors.UpdateDetector;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class DetectorEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DetectorEndpointsTests(PrIMAApiFactory factory)
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

    [Fact]
    public async Task DetectorEndpoints_UnauthenticatedRequests_Return401Unauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var dummyId = Guid.NewGuid();

        var getList = await unauthClient.GetAsync("/api/detectors");
        getList.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getSummary = await unauthClient.GetAsync("/api/detectors/summary");
        getSummary.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getDetail = await unauthClient.GetAsync($"/api/detectors/{dummyId}");
        getDetail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getRuns = await unauthClient.GetAsync($"/api/detectors/{dummyId}/runs");
        getRuns.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postRegister = await unauthClient.PostAsJsonAsync("/api/detectors", new RegisterDetectorRequest("Test", "Test Desc", "DataQuality", "Module", 300, null));
        postRegister.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var putUpdate = await unauthClient.PutAsJsonAsync($"/api/detectors/{dummyId}", new UpdateDetectorRequest("Test Desc", "DataQuality", "Module", 300, null));
        putUpdate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postToggle = await unauthClient.PostAsJsonAsync($"/api/detectors/{dummyId}/toggle-status", new ToggleDetectorStatusRequest("Pause"));
        postToggle.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DetectorEndpoints_CompleteCrudAndLifecycleFlow()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. POST /api/detectors - Register new detector
        var registerRequest = new RegisterDetectorRequest(
            Name: "Inventory Ledger Balance Probe",
            Description: "Monitors warehouse inventory reconciliation",
            Scope: "DataQuality",
            TargetModule: "Warehouse",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: "https://probes.internal/api/warehouse");

        var registerResponse = await authClient.PostAsJsonAsync("/api/detectors", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await registerResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Inventory Ledger Balance Probe");
        created.AdminStatus.Should().Be("Active");
        created.HealthStatus.Should().Be("Healthy");

        var detectorId = created.Id;

        // 2. GET /api/detectors/{id} - Query detail
        var detailResponse = await authClient.GetAsync($"/api/detectors/{detectorId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(detectorId);
        detail.TargetModule.Should().Be("Warehouse");
        detail.WebhookUrl.Should().Be("https://probes.internal/api/warehouse");

        // 3. PUT /api/detectors/{id} - Update configuration
        var updateRequest = new UpdateDetectorRequest(
            Description: "Updated warehouse inventory reconciliation probe",
            Scope: "BusinessLogic",
            TargetModule: "SupplyChain",
            ExpectedCadenceSeconds: 600,
            WebhookUrl: "https://probes.internal/api/supplychain");

        var updateResponse = await authClient.PutAsJsonAsync($"/api/detectors/{detectorId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify update persisted
        var updatedDetailResponse = await authClient.GetAsync($"/api/detectors/{detectorId}");
        var updatedDetail = await updatedDetailResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        updatedDetail!.Description.Should().Be("Updated warehouse inventory reconciliation probe");
        updatedDetail.Scope.Should().Be("BusinessLogic");
        updatedDetail.TargetModule.Should().Be("SupplyChain");
        updatedDetail.ExpectedCadenceSeconds.Should().Be(600);
        updatedDetail.WebhookUrl.Should().Be("https://probes.internal/api/supplychain");

        // 4. POST /api/detectors/{id}/toggle-status - Pause -> Resume -> Retire
        var pauseResponse = await authClient.PostAsJsonAsync($"/api/detectors/{detectorId}/toggle-status", new ToggleDetectorStatusRequest("Pause"));
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pausedDetailResponse = await authClient.GetAsync($"/api/detectors/{detectorId}");
        var pausedDetail = await pausedDetailResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        pausedDetail!.AdminStatus.Should().Be("Paused");

        var resumeResponse = await authClient.PostAsJsonAsync($"/api/detectors/{detectorId}/toggle-status", new ToggleDetectorStatusRequest("Resume"));
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resumedDetailResponse = await authClient.GetAsync($"/api/detectors/{detectorId}");
        var resumedDetail = await resumedDetailResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        resumedDetail!.AdminStatus.Should().Be("Active");

        var retireResponse = await authClient.PostAsJsonAsync($"/api/detectors/{detectorId}/toggle-status", new ToggleDetectorStatusRequest("Retire"));
        retireResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var retiredDetailResponse = await authClient.GetAsync($"/api/detectors/{detectorId}");
        var retiredDetail = await retiredDetailResponse.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);
        retiredDetail!.AdminStatus.Should().Be("Retired");

        // Retired detector rejects subsequent updates
        var rejectedUpdate = await authClient.PutAsJsonAsync($"/api/detectors/{detectorId}", updateRequest);
        rejectedUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DetectorEndpoints_Queries_ReturnSummaryListAndRunLogs()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // Register 2 detectors
        var det1 = await authClient.PostAsJsonAsync("/api/detectors", new RegisterDetectorRequest(
            Name: "Billing Sync Probe",
            Description: "Sync checker",
            Scope: "DataQuality",
            TargetModule: "Billing",
            ExpectedCadenceSeconds: 300,
            WebhookUrl: null));
        var det1Dto = await det1.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);

        var det2 = await authClient.PostAsJsonAsync("/api/detectors", new RegisterDetectorRequest(
            Name: "Claims Pipeline Probe",
            Description: "Claims pipeline checker",
            Scope: "ProcessFlow",
            TargetModule: "Claims",
            ExpectedCadenceSeconds: 600,
            WebhookUrl: null));
        var det2Dto = await det2.Content.ReadFromJsonAsync<DetectorDetailDto>(JsonOptions);

        // Pause det2
        await authClient.PostAsJsonAsync($"/api/detectors/{det2Dto!.Id}/toggle-status", new ToggleDetectorStatusRequest("Pause"));

        // Seed an inspection run for det1
        var runId = Guid.NewGuid();
        const string runSql = """
            INSERT INTO dbo.InspectionRuns (
                Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, FailureReason, CreatedAtUtc
            ) VALUES (
                @Id, @DetectorId, SYSUTCDATETIME(), 'Scheduled', 'Passed', 145, NULL, SYSUTCDATETIME()
            );
            """;
        await _factory.ExecuteSqlAsync(runSql, new { Id = runId, DetectorId = det1Dto!.Id });

        // 1. GET /api/detectors/summary
        var summaryResponse = await authClient.GetAsync("/api/detectors/summary");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<DetectorSummaryDto>(JsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalCount.Should().Be(2);
        summary.HealthyCount.Should().Be(1);
        summary.PausedCount.Should().Be(1);
        summary.FailingCount.Should().Be(0);
        summary.SilentCount.Should().Be(0);

        // 2. GET /api/detectors (Portfolio list)
        var listResponse = await authClient.GetAsync("/api/detectors?Scope=DataQuality");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<List<DetectorListItemDto>>(JsonOptions);
        list.Should().NotBeNull();
        list!.Should().ContainSingle(d => d.Id == det1Dto!.Id);
        list![0].PassRate30d.Should().Be(100.0m);
        list![0].TotalRuns30d.Should().Be(1);

        // 3. GET /api/detectors/{id}/runs (Run logs)
        var runsResponse = await authClient.GetAsync($"/api/detectors/{det1Dto!.Id}/runs?outcome=Passed&limit=10");
        runsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runLog = await runsResponse.Content.ReadFromJsonAsync<InspectionRunLogResponse>(JsonOptions);
        runLog.Should().NotBeNull();
        runLog!.TotalCount.Should().Be(1);
        runLog.Runs.Should().ContainSingle(r => r.Id == runId && r.Outcome == "Passed" && r.DurationMs == 145);
    }
}
