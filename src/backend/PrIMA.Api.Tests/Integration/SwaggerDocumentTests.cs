using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PrIMA.Api.Tests.Fixtures;
using Xunit;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class SwaggerDocumentTests
{
    private readonly PrIMAApiFactory _factory;

    public SwaggerDocumentTests(PrIMAApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerJson_ContainsAllStage2EndpointsAndSchemas()
    {
        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();

        var rootNode = JsonNode.Parse(content);
        rootNode.Should().NotBeNull();

        var paths = rootNode!["paths"]?.AsObject();
        paths.Should().NotBeNull();

        // Verify Stage 2, Stage 3, and Stage 4 endpoints
        var expectedPaths = new[]
        {
            "/api/officers",
            "/api/issues/{id}",
            "/api/issues/{id}/take-it",
            "/api/issues/{id}/assign",
            "/api/issues/{id}/acknowledge",
            "/api/issues/{id}/start-investigation",
            "/api/issues/{id}/notes",
            "/api/issues/{id}/remediation",
            "/api/issues/{id}/verification/manual",
            "/api/issues/{id}/resolve",
            "/api/issues/{id}/dismiss",
            "/api/issues/{id}/verification/trigger-run",
            "/api/detectors",
            "/api/detectors/summary",
            "/api/detectors/{id}",
            "/api/detectors/{id}/runs",
            "/api/detectors/{id}/toggle-status",
            "/api/detectors/{id}/trigger-run",
            "/api/history/reliability-kpi",
            "/api/history/issues"
        };

        foreach (var expectedPath in expectedPaths)
        {
            paths!.ContainsKey(expectedPath).Should().BeTrue($"OpenAPI path '{expectedPath}' should be defined");
        }

        // Verify schemas
        var schemas = rootNode["components"]?["schemas"]?.AsObject();
        schemas.Should().NotBeNull();

        var expectedSchemas = new[]
        {
            "OfficerDto",
            "IssueDetailDto",
            "OccurrenceItemDto",
            "InvestigationNoteDto",
            "RemediationRecordDto",
            "VerificationRecordDto",
            "DismissalRecordDto",
            "IssueActivityLogDto",
            "AssignIssueRequest",
            "AddNoteRequest",
            "RecordRemediationRequest",
            "RecordManualVerificationRequest",
            "DismissIssueRequest",
            "DetectorListItemDto",
            "DetectorSummaryDto",
            "DetectorDetailDto",
            "DetectorReliabilityStatsDto",
            "RecentRunSummaryDto",
            "LinkedActiveIssueDto",
            "InspectionRunLogResponse",
            "InspectionRunDetailDto",
            "RegisterDetectorRequest",
            "UpdateDetectorRequest",
            "ToggleDetectorStatusRequest",
            "InspectionRunResultDto",
            "VerificationResultDto",
            "ReliabilityKpiResponse",
            "BreakdownItemDto",
            "HistoricalIssuesResponse",
            "HistoricalIssueItemDto"
        };

        foreach (var expectedSchema in expectedSchemas)
        {
            schemas!.ContainsKey(expectedSchema).Should().BeTrue($"OpenAPI schema '{expectedSchema}' should be defined");
        }

        // Dump swagger.json to PrIMA.Api for contract synchronization
        var repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            var swaggerFilePath = Path.Combine(repoRoot, "src", "backend", "PrIMA.Api", "swagger.json");
            await File.WriteAllTextAsync(swaggerFilePath, content);
        }
    }

    private static string? FindRepoRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "docs", "issues", "IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md")) ||
                File.Exists(Path.Combine(current, "docs", "issues", "IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md")) ||
                File.Exists(Path.Combine(current, "docs", "issues", "IS002-PRIMA-STAGE-2-IMPLEMENTATION-PLAN.md")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return null;
    }
}
