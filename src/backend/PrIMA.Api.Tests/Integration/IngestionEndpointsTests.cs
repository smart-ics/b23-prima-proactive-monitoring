using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class IngestionEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IngestionEndpointsTests(PrIMAApiFactory factory)
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
    public async Task Ingest_MissingApiKey_Returns401ProblemDetails()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 120
        };

        var message = CreateRequest(request, apiKey: null);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Missing or invalid X-Detector-API-Key header.");
    }

    [Fact]
    public async Task Ingest_InvalidApiKey_Returns401ProblemDetails()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 120
        };

        var message = CreateRequest(request, apiKey: "unauthorized-bogus-key");

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Missing or invalid X-Detector-API-Key header.");
    }

    [Fact]
    public async Task Ingest_UnknownDetectorId_Returns404ProblemDetails()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        var request = new IngestTelemetryRequest
        {
            DetectorId = unknownId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 100
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public async Task Ingest_PausedDetector_Returns409ProblemDetails()
    {
        // Arrange
        var pausedDetectorId = await _factory.SeedDetectorAsync(
            name: "Paused Sync Detector",
            adminStatus: "Paused");

        var request = new IngestTelemetryRequest
        {
            DetectorId = pausedDetectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 150
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Detector is not active.");
    }

    [Fact]
    public async Task Ingest_ExecutionFailedOutcome_SynthesizesFailIssue_MarksDetectorFailing_AndReturns200()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(name: "GL Balance Validator");
        var now = DateTime.UtcNow;
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = now,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 500,
            FailureReason = "Database timeout occurred during sweep"
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Outcome.Should().Be("ExecutionFailed");
        body.TotalFindingsProcessed.Should().Be(0);
        body.NewIssuesCreated.Should().Be(1);
        body.RecurrencesRecorded.Should().Be(0);
        body.Results.Should().HaveCount(1);
        body.Results[0].CorrelationKey.Should().Be($"FAIL:{detectorId}");
        body.Results[0].Action.Should().Be("Created");

        // Verify InspectionRun recorded with FailureReason
        const string runSql = "SELECT Outcome, FailureReason FROM dbo.InspectionRuns WHERE Id = @Id;";
        var runRow = await _factory.QuerySingleOrDefaultSqlAsync<InspectionRunRow>(runSql, new { Id = body.InspectionRunId });
        runRow.Should().NotBeNull();
        runRow!.Outcome.Should().Be("ExecutionFailed");
        runRow.FailureReason.Should().Be("Database timeout occurred during sweep");

        // Verify Detector HealthStatus transitioned to Failing
        const string detSql = "SELECT HealthStatus, LastHealthyHeartbeatUtc FROM dbo.Detectors WHERE Id = @Id;";
        var detRow = await _factory.QuerySingleOrDefaultSqlAsync<DetectorStatusRow>(detSql, new { Id = detectorId });
        detRow.Should().NotBeNull();
        detRow!.HealthStatus.Should().Be("Failing");

        // Verify synthetic issue created per BR-PRIMA-005
        const string issueSql = "SELECT Scope, Title, BaseSeverity, Status, OccurrenceCount FROM dbo.Issues WHERE CorrelationKey = @Key;";
        var issueRow = await _factory.QuerySingleOrDefaultSqlAsync<IssueVerifyRow>(issueSql, new { Key = $"FAIL:{detectorId}" });
        issueRow.Should().NotBeNull();
        issueRow!.Scope.Should().Be("Infrastructure");
        issueRow.Title.Should().Be("Execution Failure: GL Balance Validator");
        issueRow.BaseSeverity.Should().Be("High");
        issueRow.Status.Should().Be("Open");
        issueRow.OccurrenceCount.Should().Be(1);

        // Verify Activity Log entry Created
        const string logSql = "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'Created';";
        var logRow = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = body.Results[0].IssueId });
        logRow.Should().NotBeNull();
        logRow!.ActivityType.Should().Be("Created");
        logRow.ActorName.Should().Be("System");
    }

    [Fact]
    public async Task Ingest_ExecutionFailedOutcome_SubsequentFailure_AppendsOccurrence_AndLogsRecurrence()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(name: "GL Balance Validator 2");
        var now = DateTime.UtcNow;
        var initialRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = now,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 500,
            FailureReason = "Initial failure"
        };

        var firstResp = await _client.SendAsync(CreateRequest(initialRequest));
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResp.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        var issueId = firstBody!.Results[0].IssueId;

        // Act - Second failure
        var recurrenceRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = now.AddMinutes(5),
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 600,
            FailureReason = "Second failure"
        };

        var secondResp = await _client.SendAsync(CreateRequest(recurrenceRequest));

        // Assert
        secondResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await secondResp.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        secondBody!.Outcome.Should().Be("ExecutionFailed");
        secondBody.TotalFindingsProcessed.Should().Be(0);
        secondBody.NewIssuesCreated.Should().Be(0);
        secondBody.RecurrencesRecorded.Should().Be(1);
        secondBody.Results[0].Action.Should().Be("Appended");
        secondBody.Results[0].IssueId.Should().Be(issueId);
        secondBody.Results[0].OccurrenceNumber.Should().Be(2);

        // Verify OccurrenceCount incremented
        const string issueSql = "SELECT OccurrenceCount FROM dbo.Issues WHERE Id = @Id;";
        var occurrenceCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(issueSql, new { Id = issueId });
        occurrenceCount.Should().Be(2);

        // Verify Activity Log entry RecurrenceObserved
        const string logSql = "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'RecurrenceObserved';";
        var logRow = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = issueId });
        logRow.Should().NotBeNull();
        logRow!.ActivityType.Should().Be("RecurrenceObserved");
        logRow.ActorName.Should().Be("System");
    }

    [Fact]
    public async Task Ingest_CleanRunFromFailingOrSilentDetector_ResetsHealthToHealthy_AndLogsRecoveryAudit()
    {
        // Arrange: detector in Failing health with an active FAIL issue
        var detectorId = await _factory.SeedDetectorAsync(name: "Recovering Detector", healthStatus: "Failing");
        var failIssueId = Guid.NewGuid();
        var silentIssueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Insert active FAIL issue and active SILENT issue
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CreatedAtUtc, LastOccurrenceAtUtc
            ) VALUES 
                (@FailId, @FailKey, 'Execution Failure: Recovering Detector', @DetectorId, 'Recovering Detector', 'Infrastructure', 'High', 'High', 3, 'Open', 0, 1, @Now, @Now),
                (@SilentId, @SilentKey, 'Silent Detector: Recovering Detector', @DetectorId, 'Recovering Detector', 'Infrastructure', 'High', 'High', 3, 'Open', 0, 1, @Now, @Now);
            """,
            new
            {
                FailId = failIssueId,
                FailKey = $"FAIL:{detectorId}",
                SilentId = silentIssueId,
                SilentKey = $"SILENT:{detectorId}",
                DetectorId = detectorId,
                Now = now
            });

        // Act: clean Passed telemetry ingested
        var cleanRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = now.AddMinutes(2),
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 120
        };

        var response = await _client.SendAsync(CreateRequest(cleanRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify detector health reset to Healthy and heartbeat updated
        const string detSql = "SELECT HealthStatus, LastHealthyHeartbeatUtc FROM dbo.Detectors WHERE Id = @Id;";
        var detRow = await _factory.QuerySingleOrDefaultSqlAsync<DetectorStatusRow>(detSql, new { Id = detectorId });
        detRow.Should().NotBeNull();
        detRow!.HealthStatus.Should().Be("Healthy");
        detRow.LastHealthyHeartbeatUtc.Should().NotBeNull();
        detRow.LastHealthyHeartbeatUtc!.Value.Should().BeCloseTo(now.AddMinutes(2), TimeSpan.FromSeconds(2));

        // Verify FAIL issue remains Open per DEC-004
        const string issueSql = "SELECT Status FROM dbo.Issues WHERE Id = @Id;";
        var failStatus = await _factory.QuerySingleOrDefaultSqlAsync<string>(issueSql, new { Id = failIssueId });
        failStatus.Should().Be("Open");

        // Verify DetectorRecovered activity logs inserted for both issues
        const string logSql = """
            SELECT ActivityType, ActorName, Summary 
            FROM dbo.IssueActivityLogs 
            WHERE IssueId = @IssueId AND ActivityType = 'DetectorRecovered';
            """;
        var failLog = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = failIssueId });
        failLog.Should().NotBeNull();
        failLog!.ActivityType.Should().Be("DetectorRecovered");
        failLog.ActorName.Should().Be("System");
        failLog.Summary.Should().Contain("Detector resumed healthy reporting");
        failLog.Summary.Should().Contain("Issue remains open for EDP verification.");

        var silentLog = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = silentIssueId });
        silentLog.Should().NotBeNull();
        silentLog!.ActivityType.Should().Be("DetectorRecovered");
        silentLog.ActorName.Should().Be("System");
        silentLog.Summary.Should().Contain("Detector resumed healthy reporting");
    }

    [Fact]
    public async Task Ingest_DuplicateTargetEntityReferenceInSamePayload_Returns422ProblemDetails()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var findings = new List<FindingItem>
        {
            new()
            {
                TargetEntityReference = "BILLING-TX-100",
                Title = "Imbalance 100",
                BaseSeverity = "High",
                Evidence = new FindingEvidenceItem
                {
                    Module = "Billing",
                    SourceTable = "BillingJournals",
                    RecordKeys = "100",
                    DebitTotal = 100m,
                    CreditTotal = 90m,
                    VarianceDelta = 10m,
                    Trace = "Imbalance"
                }
            },
            new()
            {
                TargetEntityReference = "  billing-tx-100  ",
                Title = "Imbalance Duplicate",
                BaseSeverity = "High",
                Evidence = new FindingEvidenceItem
                {
                    Module = "Billing",
                    SourceTable = "BillingJournals",
                    RecordKeys = "100",
                    DebitTotal = 100m,
                    CreditTotal = 90m,
                    VarianceDelta = 10m,
                    Trace = "Imbalance Duplicate"
                }
            }
        };

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 300,
            Findings = findings
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Values.SelectMany(x => x).Should().ContainMatch("*duplicate targetEntityReference*");
    }

    [Fact]
    public async Task Ingest_PassedOutcome_RecordsInspectionRunWithZeroFindingsAndReturns200OK()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var executionTime = DateTime.UtcNow;
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Scheduled",
            Outcome = "Passed",
            DurationMs = 85
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        responseBody.Should().NotBeNull();
        responseBody!.DetectorId.Should().Be(detectorId);
        responseBody.Outcome.Should().Be("Passed");
        responseBody.ProcessedFindingsCount.Should().Be(0);
        responseBody.Results.Should().BeEmpty();

        // Verify Database State
        const string runSql = "SELECT Outcome, DurationMs FROM dbo.InspectionRuns WHERE Id = @Id;";
        var runRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(runSql, new { Id = responseBody.InspectionRunId });
        ((string)runRow!.Outcome).Should().Be("Passed");
        ((int)runRow.DurationMs).Should().Be(85);

        const string detectorSql = "SELECT HealthStatus, LastRunAtUtc, LastHealthyHeartbeatUtc FROM dbo.Detectors WHERE Id = @Id;";
        var detectorRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(detectorSql, new { Id = detectorId });
        ((string)detectorRow!.HealthStatus).Should().Be("Healthy");
        ((DateTime)detectorRow.LastRunAtUtc).Should().BeCloseTo(executionTime, TimeSpan.FromSeconds(2));
        ((DateTime)detectorRow.LastHealthyHeartbeatUtc).Should().BeCloseTo(executionTime, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Ingest_FindingDiscoveredOutcome_RecordsRunAndCreatesOpenIssueWithOccurrence1()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync(name: "Prescription Stock Checker", targetModule: "Pharmacy");
        var executionTime = DateTime.UtcNow;
        var finding = new FindingItem
        {
            TargetEntityReference = "RX-STOCK-9901",
            Title = "Negative Physical Inventory",
            BaseSeverity = "High",
            Evidence = new FindingEvidenceItem
            {
                Module = "Pharmacy",
                SourceTable = "DrugStockBatches",
                RecordKeys = "STOCK-9901",
                DebitTotal = 0m,
                CreditTotal = 50m,
                VarianceDelta = 50m,
                Trace = "Negative stock detected."
            }
        };

        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = executionTime,
            TriggerSource = "Manual",
            Outcome = "FindingDiscovered",
            DurationMs = 210,
            Findings = [finding]
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        responseBody.Should().NotBeNull();
        responseBody!.ProcessedFindingsCount.Should().Be(1);
        responseBody.Results.Should().HaveCount(1);
        responseBody.Results[0].Action.Should().Be("Created");
        responseBody.Results[0].CorrelationKey.Should().Be($"{detectorId:D}:RX-STOCK-9901");

        // Verify Issue Record in DB
        const string issueSql = """
            SELECT
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope, BaseSeverity,
                CurrentSeverity, SeverityRank, Status, EscalationLevel, OccurrenceCount, CurrentAssignee
            FROM dbo.Issues
            WHERE Id = @Id;
            """;
        var issueRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(issueSql, new { Id = responseBody.Results[0].IssueId });
        ((object?)issueRow).Should().NotBeNull();
        ((string)issueRow!.CorrelationKey).Should().Be($"{detectorId:D}:RX-STOCK-9901");
        ((string)issueRow.Title).Should().Be("Negative Physical Inventory");
        ((string)issueRow.Status).Should().Be("Open");
        ((byte)issueRow.SeverityRank).Should().Be(3); // High rank = 3
        ((int)issueRow.OccurrenceCount).Should().Be(1);
        ((int)issueRow.EscalationLevel).Should().Be(0);
        (issueRow.CurrentAssignee as string).Should().BeNull();

        // Verify Occurrence in DB
        const string occSql = "SELECT OccurrenceNumber, InspectionRunId FROM dbo.Occurrences WHERE IssueId = @IssueId;";
        var occRows = (await _factory.QuerySqlAsync<dynamic>(occSql, new { IssueId = responseBody.Results[0].IssueId })).ToList();
        occRows.Should().HaveCount(1);
        ((int)occRows[0].OccurrenceNumber).Should().Be(1);
        ((Guid)occRows[0].InspectionRunId).Should().Be(responseBody.InspectionRunId);

        // Verify Created Activity Log in DB
        const string logSql = "SELECT ActivityType, ActorName, Summary FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'Created';";
        var logRow = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = responseBody.Results[0].IssueId });
        logRow.Should().NotBeNull();
        logRow!.ActivityType.Should().Be("Created");
        logRow.ActorName.Should().Be("System");
    }

    [Fact]
    public async Task Ingest_RecurringFinding_AppendsOccurrence2AndIncrementsCountWithoutCreatingNewIssue()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var finding = new FindingItem
        {
            TargetEntityReference = "RECURRING-ENTITY-01",
            Title = "Recurring Anomaly",
            BaseSeverity = "Medium",
            Evidence = new FindingEvidenceItem
            {
                Module = "Billing",
                SourceTable = "Journals",
                RecordKeys = "K-1",
                Trace = "Trace 1"
            }
        };

        var firstRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow.AddMinutes(-5),
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 100,
            Findings = [finding]
        };

        var firstResponse = await _client.SendAsync(CreateRequest(firstRequest));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        var createdIssueId = firstBody!.Results[0].IssueId;

        // Act - Ingest recurring finding with matching target entity reference
        var secondRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 110,
            Findings = [finding]
        };

        var secondResponse = await _client.SendAsync(CreateRequest(secondRequest));

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        secondBody.Should().NotBeNull();
        secondBody!.ProcessedFindingsCount.Should().Be(1);
        secondBody.Results[0].Action.Should().Be("Appended");
        secondBody.Results[0].IssueId.Should().Be(createdIssueId);

        // Verify single issue exists with OccurrenceCount = 2
        const string countSql = "SELECT COUNT(*) FROM dbo.Issues WHERE DetectorId = @DetectorId;";
        var issueCount = await _factory.QuerySingleOrDefaultSqlAsync<int>(countSql, new { DetectorId = detectorId });
        issueCount.Should().Be(1);

        const string issueSql = "SELECT OccurrenceCount, Status FROM dbo.Issues WHERE Id = @Id;";
        var issueRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(issueSql, new { Id = createdIssueId });
        ((int)issueRow!.OccurrenceCount).Should().Be(2);
        ((string)issueRow.Status).Should().Be("Open");

        // Verify 2 Occurrences
        const string occSql = "SELECT OccurrenceNumber FROM dbo.Occurrences WHERE IssueId = @IssueId ORDER BY OccurrenceNumber;";
        var occNumbers = (await _factory.QuerySqlAsync<int>(occSql, new { IssueId = createdIssueId })).ToList();
        occNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Ingest_RecurringFindingOnClosedIssue_ReopensIssueAndMarksPriorGovernanceRecordsSuperseded()
    {
        // Arrange - Seed detector and ingest initial finding
        var detectorId = await _factory.SeedDetectorAsync();
        var finding = new FindingItem
        {
            TargetEntityReference = "REOPEN-TARGET-777",
            Title = "Closed Issue Finding",
            BaseSeverity = "Critical",
            Evidence = new FindingEvidenceItem
            {
                Module = "EMR",
                SourceTable = "SyncLog",
                RecordKeys = "E-77",
                Trace = "Trace Closed"
            }
        };

        var initialRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow.AddHours(-3),
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 90,
            Findings = [finding]
        };

        var initialResponse = await _client.SendAsync(CreateRequest(initialRequest));
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialBody = await initialResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        var issueId = initialBody!.Results[0].IssueId;

        // Transition issue to Resolved and attach governance records
        const string resolveSql = """
            UPDATE dbo.Issues
            SET Status = 'Resolved',
                CurrentAssignee = 'ahmad.budi',
                AcknowledgedAtUtc = SYSUTCDATETIME(),
                ResolvedAtUtc = SYSUTCDATETIME()
            WHERE Id = @IssueId;

            INSERT INTO dbo.RemediationRecords (Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded)
            VALUES (NEWID(), @IssueId, 'DataCorrection', 'Fixed incorrect balance', 'ahmad.budi', SYSUTCDATETIME(), 0);

            INSERT INTO dbo.VerificationRecords (Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded)
            VALUES (NEWID(), @IssueId, 'AutomatedReRun', 1, 'Clean re-run passed', 'ahmad.budi', SYSUTCDATETIME(), 0);

            INSERT INTO dbo.DismissalRecords (Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded)
            VALUES (NEWID(), @IssueId, 'AcceptedOperationalException', 'Prior exception note', 'edp.lead', SYSUTCDATETIME(), 0);
            """;
        await _factory.ExecuteSqlAsync(resolveSql, new { IssueId = issueId });

        // Act - Ingest recurring finding on the closed issue (BR-PRIMA-002)
        var recurrenceRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 120,
            Findings = [finding]
        };

        var recurrenceResponse = await _client.SendAsync(CreateRequest(recurrenceRequest));

        // Assert
        recurrenceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recurrenceBody = await recurrenceResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        recurrenceBody!.Results[0].Action.Should().Be("Reopened");
        recurrenceBody.Results[0].IssueId.Should().Be(issueId);

        // Verify Issue State transitioned to Reopened and timestamps reset
        const string verifyIssueSql = """
            SELECT Status, OccurrenceCount, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, ReopenedAtUtc
            FROM dbo.Issues
            WHERE Id = @Id;
            """;
        var issueRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(verifyIssueSql, new { Id = issueId });
        ((string)issueRow!.Status).Should().Be("Reopened");
        ((int)issueRow.OccurrenceCount).Should().Be(2);
        (issueRow.CurrentAssignee as string).Should().BeNull();
        (issueRow.AcknowledgedAtUtc as DateTime?).Should().BeNull();
        (issueRow.ResolvedAtUtc as DateTime?).Should().BeNull();
        (issueRow.ReopenedAtUtc as DateTime?).Should().NotBeNull();

        // Verify Governance Records are marked IsSuperseded = 1
        const string remSql = "SELECT IsSuperseded FROM dbo.RemediationRecords WHERE IssueId = @IssueId;";
        var remSuperseded = await _factory.QuerySingleOrDefaultSqlAsync<bool>(remSql, new { IssueId = issueId });
        remSuperseded.Should().BeTrue();

        const string verSql = "SELECT IsSuperseded FROM dbo.VerificationRecords WHERE IssueId = @IssueId;";
        var verSuperseded = await _factory.QuerySingleOrDefaultSqlAsync<bool>(verSql, new { IssueId = issueId });
        verSuperseded.Should().BeTrue();

        const string disSql = "SELECT IsSuperseded FROM dbo.DismissalRecords WHERE IssueId = @IssueId;";
        var disSuperseded = await _factory.QuerySingleOrDefaultSqlAsync<bool>(disSql, new { IssueId = issueId });
        disSuperseded.Should().BeTrue();

        // Verify Activity Log contains Reopened entry
        const string reopenLogSql = """
            SELECT ActivityType, ActorName, Summary
            FROM dbo.IssueActivityLogs
            WHERE IssueId = @IssueId AND ActivityType = 'Reopened';
            """;
        var reopenLogRow = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(reopenLogSql, new { IssueId = issueId });
        reopenLogRow.Should().NotBeNull();
        reopenLogRow!.ActivityType.Should().Be("Reopened");
        reopenLogRow.ActorName.Should().Be("System");
    }

    [Fact]
    public async Task Ingest_RecurringFindingOnInvestigatingIssue_SupersedesActiveVerificationsAndWritesVerificationInvalidatedActivityLog()
    {
        // Arrange - Seed detector and ingest initial finding
        var detectorId = await _factory.SeedDetectorAsync();
        var finding = new FindingItem
        {
            TargetEntityReference = "INVESTIGATING-TARGET-404",
            Title = "Investigating Anomaly",
            BaseSeverity = "High",
            Evidence = new FindingEvidenceItem
            {
                Module = "Pharmacy",
                SourceTable = "Prescriptions",
                RecordKeys = "RX-404",
                Trace = "Trace Investigating"
            }
        };

        var initialRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow.AddHours(-1),
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 80,
            Findings = [finding]
        };

        var initialResponse = await _client.SendAsync(CreateRequest(initialRequest));
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialBody = await initialResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        var issueId = initialBody!.Results[0].IssueId;

        // Transition issue to Investigating and attach an active VerificationRecord
        const string setupSql = """
            UPDATE dbo.Issues
            SET Status = 'Investigating',
                CurrentAssignee = 'ahmad.budi',
                AcknowledgedAtUtc = SYSUTCDATETIME()
            WHERE Id = @IssueId;

            INSERT INTO dbo.VerificationRecords (Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded)
            VALUES (NEWID(), @IssueId, 'ManualQuery', 1, 'Manual verification passed', 'ahmad.budi', SYSUTCDATETIME(), 0);
            """;
        await _factory.ExecuteSqlAsync(setupSql, new { IssueId = issueId });

        // Act - Ingest recurring finding on active Investigating issue (GAP-005, OQ-005)
        var recurrenceRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 95,
            Findings = [finding]
        };

        var recurrenceResponse = await _client.SendAsync(CreateRequest(recurrenceRequest));

        // Assert
        recurrenceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recurrenceBody = await recurrenceResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        recurrenceBody!.Results[0].Action.Should().Be("Appended");
        recurrenceBody.Results[0].IssueId.Should().Be(issueId);
        recurrenceBody.Results[0].OccurrenceNumber.Should().Be(2);

        // Verify Issue status remains Investigating with incremented OccurrenceCount
        const string issueSql = "SELECT Status, OccurrenceCount, CurrentAssignee FROM dbo.Issues WHERE Id = @Id;";
        var issueRow = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(issueSql, new { Id = issueId });
        ((string)issueRow!.Status).Should().Be("Investigating");
        ((int)issueRow.OccurrenceCount).Should().Be(2);
        ((string)issueRow.CurrentAssignee).Should().Be("ahmad.budi");

        // Verify VerificationRecord is now superseded (IsSuperseded = 1)
        const string verSql = "SELECT IsSuperseded FROM dbo.VerificationRecords WHERE IssueId = @IssueId;";
        var verSuperseded = await _factory.QuerySingleOrDefaultSqlAsync<bool>(verSql, new { IssueId = issueId });
        verSuperseded.Should().BeTrue();

        // Verify Activity Log contains VerificationInvalidated entry
        const string logSql = """
            SELECT ActivityType, ActorName, Summary
            FROM dbo.IssueActivityLogs
            WHERE IssueId = @IssueId AND ActivityType = 'VerificationInvalidated';
            """;
        var logRow = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(logSql, new { IssueId = issueId });
        logRow.Should().NotBeNull();
        logRow!.ActivityType.Should().Be("VerificationInvalidated");
        logRow.ActorName.Should().Be("System");
        logRow.Summary.Should().Contain("Occurrence #2");
    }

    private sealed record ActivityLogRow(string ActivityType, string ActorName, string Summary);
    private sealed record InspectionRunRow(string Outcome, string? FailureReason);
    private sealed record DetectorStatusRow(string HealthStatus, DateTime? LastHealthyHeartbeatUtc);
    private sealed record IssueVerifyRow(string Scope, string Title, string BaseSeverity, string Status, int OccurrenceCount);
}
