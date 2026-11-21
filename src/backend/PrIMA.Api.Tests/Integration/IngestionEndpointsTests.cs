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
    public async Task Ingest_ExecutionFailedOutcome_Returns422ProblemDetails()
    {
        // Arrange
        var detectorId = await _factory.SeedDetectorAsync();
        var request = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "ExecutionFailed",
            DurationMs = 500,
            FailureReason = "Database timeout occurred during sweep"
        };

        var message = CreateRequest(request);

        // Act
        var response = await _client.SendAsync(message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Values.SelectMany(x => x).Should().ContainMatch("*ExecutionFailed outcome is supported in Stage 3*");
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
}
