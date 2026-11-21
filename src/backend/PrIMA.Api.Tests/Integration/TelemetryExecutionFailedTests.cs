using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class TelemetryExecutionFailedTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TelemetryExecutionFailedTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpRequestMessage CreateRequest(object content, string? apiKey = PrIMAApiFactory.TestApiKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(content)
        };

        if (apiKey is not null)
        {
            message.Headers.Add(ApiKeyEndpointFilter.HeaderName, apiKey);
        }

        return message;
    }

    [Fact]
    public async Task Ingestion_WhenOutcomeIsExecutionFailed_SynthesizesFailIssueAndSetsHealthToFailing()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(
            name: "Claims Pipeline Validator",
            scope: "ProcessFlow",
            targetModule: "Claims");

        var failureTime = DateTime.UtcNow;
        var failRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = failureTime,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 1500,
            FailureReason = "SQL connection timeout after 30 seconds querying ClaimsQueue."
        };

        // Act
        var response = await _client.SendAsync(CreateRequest(failRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseDto = await response.Content.ReadFromJsonAsync<TelemetryIngestionResponseDto>(JsonOptions);
        responseDto.Should().NotBeNull();
        responseDto!.Outcome.Should().Be("ExecutionFailed");
        responseDto.NewIssuesCreated.Should().Be(1);
        responseDto.RecurrencesRecorded.Should().Be(0);
        responseDto.Results.Should().ContainSingle(r => r.CorrelationKey == $"FAIL:{detectorId}" && r.Action == "Created");

        // Verify Detector HealthStatus = Failing in DB
        using var connection = _factory.CreateConnection();
        var detector = await connection.QuerySingleAsync<Detector>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id", new { Id = detectorId });
        detector.HealthStatus.Should().Be(DetectorHealth.Failing);
        detector.LastRunAtUtc.Should().BeCloseTo(failureTime, TimeSpan.FromSeconds(2));

        // Verify Issue created in DB (BR-PRIMA-005)
        var issue = await connection.QuerySingleAsync<Issue>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key", new { Key = $"FAIL:{detectorId}" });
        issue.Title.Should().Be("Execution Failure: Claims Pipeline Validator");
        issue.DetectorId.Should().Be(detectorId);
        issue.DetectorName.Should().Be("Claims Pipeline Validator");
        issue.Scope.Should().Be(MonitoringScope.Infrastructure);
        issue.BaseSeverity.Should().Be(IssueSeverity.High);
        issue.CurrentSeverity.Should().Be(IssueSeverity.High);
        issue.SeverityRank.Should().Be(SeverityRank.High);
        issue.Status.Should().Be(IssueStatus.Open);
        issue.OccurrenceCount.Should().Be(1);

        // Verify Occurrence #1 inserted
        var occurrences = (await connection.QueryAsync<OccurrenceRecord>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC",
            new { IssueId = issue.Id })).ToList();
        occurrences.Should().ContainSingle();
        occurrences[0].OccurrenceNumber.Should().Be(1);
        occurrences[0].InspectionRunId.Should().Be(responseDto.InspectionRunId);

        // Verify Activity Log inserted
        var logs = (await connection.QueryAsync<ActivityLogRecord>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC",
            new { IssueId = issue.Id })).ToList();
        logs.Should().ContainSingle();
        logs[0].ActivityType.Should().Be("Created");
        logs[0].ActorName.Should().Be("System");
    }

    [Fact]
    public async Task Ingestion_WhenSubsequentExecutionFailedOccurs_AppendsRecurringOccurrence()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(name: "Recurring Failure Probe");
        var time1 = DateTime.UtcNow.AddMinutes(-5);
        var time2 = DateTime.UtcNow;

        var request1 = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = time1,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 800,
            FailureReason = "First failure"
        };
        await _client.SendAsync(CreateRequest(request1));
        await Task.Delay(25);

        var request2 = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = time2,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 900,
            FailureReason = "Second failure"
        };

        // Act
        var response2 = await _client.SendAsync(CreateRequest(request2));

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseDto = await response2.Content.ReadFromJsonAsync<TelemetryIngestionResponseDto>(JsonOptions);
        responseDto!.NewIssuesCreated.Should().Be(0);
        responseDto.RecurrencesRecorded.Should().Be(1);
        responseDto.Results.Should().ContainSingle(r => r.CorrelationKey == $"FAIL:{detectorId}" && r.Action == "Appended" && r.OccurrenceNumber == 2);

        using var connection = _factory.CreateConnection();
        var issue = await connection.QuerySingleAsync<Issue>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key", new { Key = $"FAIL:{detectorId}" });
        issue.OccurrenceCount.Should().Be(2);

        var occurrences = (await connection.QueryAsync<OccurrenceRecord>(
            "SELECT * FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber ASC",
            new { IssueId = issue.Id })).ToList();
        occurrences.Should().HaveCount(2);
        occurrences[1].OccurrenceNumber.Should().Be(2);

        var logs = (await connection.QueryAsync<ActivityLogRecord>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC",
            new { IssueId = issue.Id })).ToList();
        logs.Should().HaveCount(2);
        logs[1].ActivityType.Should().Be("RecurrenceObserved");
    }

    [Fact]
    public async Task Ingestion_WhenCleanTelemetryFollowsExecutionFailed_RecoversHealthAndLogsActivityDEC004()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(name: "Self-Healing Probe");
        var failTime = DateTime.UtcNow.AddMinutes(-10);

        // Step 1: Trigger failure
        var failRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = failTime,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 1200,
            FailureReason = "Temporary network partition"
        };
        var failResp = await _client.SendAsync(CreateRequest(failRequest));
        failResp.StatusCode.Should().Be(HttpStatusCode.OK);
        await Task.Delay(25);

        using var connection = _factory.CreateConnection();
        var detAfterFail = await connection.QuerySingleAsync<Detector>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id", new { Id = detectorId });
        detAfterFail.HealthStatus.Should().Be(DetectorHealth.Failing);

        var issue = await connection.QuerySingleAsync<Issue>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key", new { Key = $"FAIL:{detectorId}" });
        issue.Status.Should().Be(IssueStatus.Open);

        // Step 2: Clean run arrives (Passed with zero findings)
        var recoveryTime = DateTime.UtcNow;
        var cleanRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = recoveryTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 95
        };

        // Act
        var cleanResp = await _client.SendAsync(CreateRequest(cleanRequest));

        // Assert
        cleanResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleanDto = await cleanResp.Content.ReadFromJsonAsync<TelemetryIngestionResponseDto>(JsonOptions);
        cleanDto.Should().NotBeNull();
        cleanDto!.Outcome.Should().Be("Passed");

        // Verify detector health recovered to Healthy (DEC-004)
        var detAfterRecovery = await connection.QuerySingleAsync<Detector>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id", new { Id = detectorId });
        detAfterRecovery.HealthStatus.Should().Be(DetectorHealth.Healthy);
        detAfterRecovery.LastHealthyHeartbeatUtc.Should().BeCloseTo(recoveryTime, TimeSpan.FromSeconds(2));

        // Verify DEC-004: Issue remains open, but "DetectorRecovered" activity log appended
        var issueAfterRecovery = await connection.QuerySingleAsync<Issue>(
            "SELECT * FROM dbo.Issues WHERE CorrelationKey = @Key", new { Key = $"FAIL:{detectorId}" });
        issueAfterRecovery.Status.Should().Be(IssueStatus.Open); // DEC-004: Remains open for EDP verification

        var logs = (await connection.QueryAsync<ActivityLogRecord>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC",
            new { IssueId = issue.Id })).ToList();
        logs.Should().HaveCount(2);
        logs[1].ActivityType.Should().Be("DetectorRecovered");
        logs[1].ActorName.Should().Be("System");
        logs[1].Summary.Should().Contain($"Detector resumed healthy reporting via run '{cleanDto.InspectionRunId}'");
    }

    [Fact]
    public async Task Ingestion_WhenCleanTelemetryFollowsSilentStatus_RecoversHealthAndLogsActivityDEC004()
    {
        // Arrange: Seed detector in Silent state with SILENT:{id} active issue
        var detectorId = await _factory.SeedDetectorAsync(
            name: "Silent Restored Probe",
            healthStatus: "Silent");

        var issueId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        const string seedSilentIssueSql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope, BaseSeverity, CurrentSeverity,
                SeverityRank, Status, EscalationLevel, OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
            ) VALUES (
                @IssueId, @Key, 'Silent Detector: Silent Restored Probe', @DetectorId, 'Silent Restored Probe',
                'Infrastructure', 'High', 'High', 3, 'Open', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            );
            """;
        await _factory.ExecuteSqlAsync(seedSilentIssueSql, new
        {
            IssueId = issueId,
            Key = $"SILENT:{detectorId}",
            DetectorId = detectorId
        });

        // Act: Clean telemetry arrives
        var recoveryTime = DateTime.UtcNow;
        var cleanRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = recoveryTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 120
        };
        var cleanResp = await _client.SendAsync(CreateRequest(cleanRequest));

        // Assert
        cleanResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleanDto = await cleanResp.Content.ReadFromJsonAsync<TelemetryIngestionResponseDto>(JsonOptions);

        using var connection = _factory.CreateConnection();
        var detector = await connection.QuerySingleAsync<Detector>(
            "SELECT * FROM dbo.Detectors WHERE Id = @Id", new { Id = detectorId });
        detector.HealthStatus.Should().Be(DetectorHealth.Healthy);
        detector.LastHealthyHeartbeatUtc.Should().BeCloseTo(recoveryTime, TimeSpan.FromSeconds(2));

        var logs = (await connection.QueryAsync<ActivityLogRecord>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId ORDER BY TimestampUtc ASC",
            new { IssueId = issueId })).ToList();
        logs.Should().ContainSingle(l => l.ActivityType == "DetectorRecovered");
        logs[0].Summary.Should().Contain($"Detector resumed healthy reporting via run '{cleanDto!.InspectionRunId}'");
    }

    private sealed class OccurrenceRecord
    {
        public Guid Id { get; init; }
        public Guid IssueId { get; init; }
        public Guid InspectionRunId { get; init; }
        public int OccurrenceNumber { get; init; }
        public DateTime TimestampUtc { get; init; }
    }

    private sealed class ActivityLogRecord
    {
        public Guid Id { get; init; }
        public Guid IssueId { get; init; }
        public string ActivityType { get; init; } = default!;
        public string ActorName { get; init; } = default!;
        public string Summary { get; init; } = default!;
        public DateTime TimestampUtc { get; init; }
    }
}
