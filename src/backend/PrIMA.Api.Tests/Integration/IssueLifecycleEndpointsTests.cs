using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.AssignIssue;
using PrIMA.Api.Features.Issues.DismissIssue;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Features.Officers;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class IssueLifecycleEndpointsTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record IssueRow(
        Guid Id,
        string Status,
        string? CurrentAssignee,
        DateTime? AcknowledgedAtUtc,
        DateTime? ResolvedAtUtc,
        DateTime? DismissedAtUtc,
        int OccurrenceCount);

    private sealed record ActivityLogRow(string ActivityType, string ActorName, string Summary, string? DetailsJson);
    private sealed record NoteRow(Guid IssueId, string OfficerName, string NoteText);
    private sealed record VerificationRow(Guid Id, bool CleanPassed, bool IsSuperseded, string VerifiedBy);
    private sealed record RemediationRow(Guid Id, string ActionCategory, string PerformedBy, bool IsSuperseded);
    private sealed record DismissalRow(string DismissalReason, string JustificationRationale, string DismissedBy);

    public IssueLifecycleEndpointsTests(PrIMAApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "ahmad.budi")
    {
        var token = await _factory.GetAuthTokenAsync(_client, username: username);
        var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authClient;
    }

    private static HttpRequestMessage CreateTelemetryRequest(IngestTelemetryRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Detector-API-Key", PrIMAApiFactory.TestApiKey);
        return request;
    }

    private async Task<(Guid IssueId, Guid DetectorId)> SeedIssueAsync(
        string status = "Open",
        string? currentAssignee = null,
        DateTime? acknowledgedAtUtc = null)
    {
        var detectorId = await _factory.SeedDetectorAsync(name: $"Lifecycle-Detector-{Guid.NewGuid():N}");
        var inspectionRunId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
            VALUES (@Id, @DetectorId, @Now, 'Scheduled', 'FindingDiscovered', 100, @Now);
            """,
            new { Id = inspectionRunId, DetectorId = detectorId, Now = now });

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, @Key, 'Lifecycle Integration Test Issue', @DetectorId, 'Lifecycle Detector', 'DataIntegrity',
                'High', 'High', 3, @Status, 0,
                1, @CurrentAssignee, @Now, @Now,
                @AcknowledgedAtUtc, NULL, NULL, NULL
            );
            """,
            new
            {
                Id = issueId,
                Key = $"TEST:LIFECYCLE:{issueId:N}",
                DetectorId = detectorId,
                Status = status,
                CurrentAssignee = currentAssignee,
                Now = now,
                AcknowledgedAtUtc = acknowledgedAtUtc
            });

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Occurrences (
                Id, IssueId, InspectionRunId, OccurrenceNumber, Module, SourceTable,
                RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString, TimestampUtc
            ) VALUES (
                NEWID(), @IssueId, @RunId, 1, 'Billing', 'BillingJournals',
                'JOURNAL-001', 1000000.00, 950000.00, 50000.00, 'Test discrepancy trace', @Now
            );
            """,
            new { IssueId = issueId, RunId = inspectionRunId, Now = now });

        return (issueId, detectorId);
    }

    // ==========================================
    // 1. POST /api/issues/{id}/take-it
    // ==========================================

    [Fact]
    public async Task TakeIt_WhenIssueOpen_TransitionsToInvestigating_AssignsCaller_AndWritesAuditLog()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Open");
        var client = await CreateAuthenticatedClientAsync("ahmad.budi"); // Full name: Ahmad Budi

        // Act
        var response = await client.PostAsync($"/api/issues/{issueId}/take-it", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var issue = await _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            "SELECT Id, Status, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, OccurrenceCount FROM dbo.Issues WHERE Id = @Id;",
            new { Id = issueId });

        issue.Should().NotBeNull();
        issue!.Status.Should().Be("Investigating");
        issue.CurrentAssignee.Should().Be("Ahmad Budi");
        issue.AcknowledgedAtUtc.Should().NotBeNull();

        var log = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'InvestigationStarted';",
            new { IssueId = issueId });

        log.Should().NotBeNull();
        log!.ActorName.Should().Be("Ahmad Budi");
        log.Summary.Should().Contain("via Take It");
    }

    // ==========================================
    // 2. POST /api/issues/{id}/assign
    // ==========================================

    [Fact]
    public async Task Assign_WhenOfficerNotFound_Returns422UnprocessableEntity_AndWhenIssueNotFound_Returns404NotFound()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Open");
        var client = await CreateAuthenticatedClientAsync();
        var nonExistentOfficerId = Guid.NewGuid();

        // Act 1: Non-existent officer -> 422 Unprocessable Entity
        var responseOfficerNotFound = await client.PostAsJsonAsync($"/api/issues/{issueId}/assign", new AssignIssueRequest(nonExistentOfficerId));

        // Assert 1
        responseOfficerNotFound.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problemOfficer = await responseOfficerNotFound.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemOfficer.Should().NotBeNull();
        problemOfficer!.Detail.Should().Contain(nonExistentOfficerId.ToString());

        // Act 2: Non-existent issue -> 404 Not Found
        var nonExistentIssueId = Guid.NewGuid();
        var responseIssueNotFound = await client.PostAsJsonAsync($"/api/issues/{nonExistentIssueId}/assign", new AssignIssueRequest(nonExistentOfficerId));

        // Assert 2
        responseIssueNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_WhenValidOfficer_TransitionsOpenToAcknowledged_AndPreservesInvestigatingStatus_OQ007()
    {
        // Arrange - Get Siti Rahma's Officer ID
        var sitiOfficer = await _factory.QuerySingleOrDefaultSqlAsync<dynamic>(
            "SELECT Id, FullName FROM dbo.Officers WHERE Username = 'siti.rahma';");
        Guid sitiId = sitiOfficer!.Id;
        string sitiFullName = sitiOfficer.FullName;

        var client = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Case A: Open -> Acknowledged
        var (openIssueId, _) = await SeedIssueAsync(status: "Open");
        var responseOpen = await client.PostAsJsonAsync($"/api/issues/{openIssueId}/assign", new AssignIssueRequest(sitiId));
        responseOpen.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var openIssue = await _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            "SELECT Id, Status, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, OccurrenceCount FROM dbo.Issues WHERE Id = @Id;",
            new { Id = openIssueId });
        openIssue!.Status.Should().Be("Acknowledged");
        openIssue.CurrentAssignee.Should().Be(sitiFullName);
        openIssue.AcknowledgedAtUtc.Should().NotBeNull();

        // Case B: Investigating issue preserves Investigating status without regression (OQ-007)
        var (investigatingIssueId, _) = await SeedIssueAsync(
            status: "Investigating",
            currentAssignee: "Ahmad Budi",
            acknowledgedAtUtc: DateTime.UtcNow.AddMinutes(-30));

        var responseInv = await client.PostAsJsonAsync($"/api/issues/{investigatingIssueId}/assign", new AssignIssueRequest(sitiId));
        responseInv.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var invIssue = await _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            "SELECT Id, Status, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, OccurrenceCount FROM dbo.Issues WHERE Id = @Id;",
            new { Id = investigatingIssueId });
        invIssue!.Status.Should().Be("Investigating");
        invIssue.CurrentAssignee.Should().Be(sitiFullName);
    }

    // ==========================================
    // 3. POST /api/issues/{id}/notes
    // ==========================================

    [Fact]
    public async Task AddNote_WhenValid_InsertsInvestigationNoteAndWritesActivityLog()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Investigating", currentAssignee: "Ahmad Budi");
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");
        const string noteContent = "Investigated transaction discrepancy: mismatched ledger rows found in journal entry #482.";

        // Act
        var response = await client.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest(noteContent));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var note = await _factory.QuerySingleOrDefaultSqlAsync<NoteRow>(
            "SELECT IssueId, OfficerName, NoteText FROM dbo.InvestigationNotes WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        note.Should().NotBeNull();
        note!.OfficerName.Should().Be("Ahmad Budi");
        note.NoteText.Should().Be(noteContent);

        var log = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'NoteAdded';",
            new { IssueId = issueId });
        log.Should().NotBeNull();
        log!.ActorName.Should().Be("Ahmad Budi");
        log.Summary.Should().Contain("Investigation note added");
    }

    // ==========================================
    // 4. POST /api/issues/{id}/remediation and POST /api/issues/{id}/verification/manual
    // ==========================================

    [Fact]
    public async Task RemediationAndManualVerification_WorkTogetherInSequence()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Investigating", currentAssignee: "Ahmad Budi");
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Act 1: Record remediation
        var remResponse = await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "Applied ledger variance adjustment script SOP-FIN-009"));
        remResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var remRow = await _factory.QuerySingleOrDefaultSqlAsync<RemediationRow>(
            "SELECT Id, ActionCategory, PerformedBy, IsSuperseded FROM dbo.RemediationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        remRow.Should().NotBeNull();
        remRow!.ActionCategory.Should().Be("DataCorrection");
        remRow.PerformedBy.Should().Be("Ahmad Budi");
        remRow.IsSuperseded.Should().BeFalse();

        // Act 2: Record manual verification
        var verResponse = await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Manual SQL query verified debit/credit balance = 0"));
        verResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var verRow = await _factory.QuerySingleOrDefaultSqlAsync<VerificationRow>(
            "SELECT Id, CleanPassed, IsSuperseded, VerifiedBy FROM dbo.VerificationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        verRow.Should().NotBeNull();
        verRow!.CleanPassed.Should().BeTrue();
        verRow.IsSuperseded.Should().BeFalse();
        verRow.VerifiedBy.Should().Be("Ahmad Budi");
    }

    // ==========================================
    // 5. POST /api/issues/{id}/resolve (BR-PRIMA-004 Gate)
    // ==========================================

    [Fact]
    public async Task Resolve_Asserts409ConflictWhenUnverified_AndAsserts204NoContentAfterCleanVerification()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Investigating", currentAssignee: "Ahmad Budi");
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Act 1: Attempt to resolve without any verification -> 409 Conflict (BR-PRIMA-004)
        var unverifiedResolveResponse = await client.PostAsync($"/api/issues/{issueId}/resolve", null);
        unverifiedResolveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await unverifiedResolveResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("verification gate");

        // Act 2: Record remediation and clean manual verification
        await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("ConfigurationAdjustment", "Adjusted billing config"));

        await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Clean passed verification evidence"));

        // Act 3: Resolve now -> 204 NoContent
        var verifiedResolveResponse = await client.PostAsync($"/api/issues/{issueId}/resolve", null);
        verifiedResolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolvedIssue = await _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            "SELECT Id, Status, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, OccurrenceCount FROM dbo.Issues WHERE Id = @Id;",
            new { Id = issueId });
        resolvedIssue!.Status.Should().Be("Resolved");
        resolvedIssue.ResolvedAtUtc.Should().NotBeNull();
    }

    // ==========================================
    // 6. POST /api/issues/{id}/dismiss (BR-PRIMA-006 Justification)
    // ==========================================

    [Fact]
    public async Task Dismiss_Asserts422UnprocessableEntityWhenUnder20Chars_AndAsserts204NoContentWhenAtLeast20Chars()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Open");
        var client = await CreateAuthenticatedClientAsync("siti.rahma");

        // Act 1: Rationale < 20 characters -> 422 Unprocessable Entity
        var shortRationaleResponse = await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/dismiss",
            new DismissIssueRequest("FalsePositive", "Short rationale")); // 15 chars
        shortRationaleResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await shortRationaleResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("JustificationRationale");
        problem.Errors["JustificationRationale"].Should().Contain(msg => msg.Contains("at least 20 characters"));

        // Act 2: Rationale >= 20 characters -> 204 NoContent
        const string validRationale = "Verified as false positive due to upstream scheduled test batch.";
        var validResponse = await client.PostAsJsonAsync(
            $"/api/issues/{issueId}/dismiss",
            new DismissIssueRequest("FalsePositive", validRationale));
        validResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dismissedIssue = await _factory.QuerySingleOrDefaultSqlAsync<IssueRow>(
            "SELECT Id, Status, CurrentAssignee, AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, OccurrenceCount FROM dbo.Issues WHERE Id = @Id;",
            new { Id = issueId });
        dismissedIssue!.Status.Should().Be("Dismissed");
        dismissedIssue.DismissedAtUtc.Should().NotBeNull();

        var dismissalRecord = await _factory.QuerySingleOrDefaultSqlAsync<DismissalRow>(
            "SELECT DismissalReason, JustificationRationale, DismissedBy FROM dbo.DismissalRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        dismissalRecord.Should().NotBeNull();
        dismissalRecord!.DismissalReason.Should().Be("FalsePositive");
        dismissalRecord.JustificationRationale.Should().Be(validRationale);
        dismissalRecord.DismissedBy.Should().Be("Siti Rahma");
    }

    // ==========================================
    // 7. GET /api/issues/{id} (Composite Projection & Logs)
    // ==========================================

    [Fact]
    public async Task GetIssueById_ReturnsCompositeProjectionWithOccurrencesNotesGovernanceAndLogs()
    {
        // Arrange
        var (issueId, _) = await SeedIssueAsync(status: "Investigating", currentAssignee: "Ahmad Budi");
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Add a note
        await client.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest("Initial inspection note."));

        // Record remediation
        await client.PostAsJsonAsync($"/api/issues/{issueId}/remediation", new RecordRemediationRequest("DataCorrection", "Fixed rows in DB"));

        // Record verification
        await client.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual", new RecordManualVerificationRequest(true, "Query returned 0 mismatches"));

        // Act
        var response = await client.GetAsync($"/api/issues/{issueId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(issueId);
        detail.Status.Should().Be("Investigating");
        detail.CurrentAssignee.Should().Be("Ahmad Budi");

        // Occurrences
        detail.Occurrences.Should().HaveCount(1);
        detail.Occurrences[0].OccurrenceNumber.Should().Be(1);
        detail.Occurrences[0].Module.Should().Be("Billing");

        // Notes
        detail.Notes.Should().HaveCount(1);
        detail.Notes[0].NoteText.Should().Be("Initial inspection note.");
        detail.Notes[0].OfficerName.Should().Be("Ahmad Budi");

        // Governance: Remediation & Verification
        detail.Remediations.Should().NotBeEmpty();
        detail.Remediations[0].ActionCategory.Should().Be("DataCorrection");
        detail.Verifications.Should().NotBeEmpty();
        detail.Verifications[0].CleanPassed.Should().BeTrue();

        // Activity Logs
        detail.ActivityLogs.Should().NotBeEmpty();
        detail.ActivityLogs.Should().Contain(l => l.ActivityType == "NoteAdded");
        detail.ActivityLogs.Should().Contain(l => l.ActivityType == "RemediationRecorded");
        detail.ActivityLogs.Should().Contain(l => l.ActivityType == "VerificationRecorded");
    }

    // ==========================================
    // 8. GET /api/officers
    // ==========================================

    [Fact]
    public async Task GetOfficers_ReturnsActiveOfficersOrderedByFullNameAscending()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");

        // Act
        var response = await client.GetAsync("/api/officers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var officers = await response.Content.ReadFromJsonAsync<List<OfficerDto>>(JsonOptions);
        officers.Should().NotBeNull();
        officers!.Should().HaveCountGreaterThanOrEqualTo(3);

        // Active officers ordered by FullName ascending: Ahmad Budi, EDP Team Lead, Siti Rahma
        var names = officers!.Select(o => o.FullName).ToList();
        names.Should().BeInAscendingOrder();
        officers.Should().Contain(o => o.Username == "ahmad.budi" && o.FullName == "Ahmad Budi");
        officers.Should().Contain(o => o.Username == "siti.rahma" && o.FullName == "Siti Rahma");
    }

    // ==========================================
    // 9. Telemetry Recurrence Invalidation (IsSuperseded = 1)
    // ==========================================

    [Fact]
    public async Task TelemetryRecurrence_OnInvestigatingIssue_InvalidatesActiveVerifications()
    {
        // Arrange - Ingest initial finding
        var detectorId = await _factory.SeedDetectorAsync(name: $"Recurrence-Detector-{Guid.NewGuid():N}");
        const string entityRef = "ENT-RECURRENCE-TEST-999";
        var finding = new FindingItem
        {
            TargetEntityReference = entityRef,
            Title = "Recurrence Test Anomaly",
            BaseSeverity = "High",
            Evidence = new FindingEvidenceItem
            {
                Module = "Billing",
                SourceTable = "BillingLedgers",
                RecordKeys = "LEDGER-999",
                Trace = "Trace ledger discrepancy"
            }
        };

        var initialRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow.AddMinutes(-30),
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 50,
            Findings = [finding]
        };

        var initialResp = await _client.SendAsync(CreateTelemetryRequest(initialRequest));
        initialResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialBody = await initialResp.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        var issueId = initialBody!.Results[0].IssueId;

        // Transition issue to Investigating and record clean verification
        var client = await CreateAuthenticatedClientAsync("ahmad.budi");
        await client.PostAsync($"/api/issues/{issueId}/take-it", null);
        await client.PostAsJsonAsync($"/api/issues/{issueId}/remediation", new RecordRemediationRequest("DataCorrection", "Remediation fix"));
        await client.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual", new RecordManualVerificationRequest(true, "Clean query passed"));

        var verBefore = await _factory.QuerySingleOrDefaultSqlAsync<VerificationRow>(
            "SELECT Id, CleanPassed, IsSuperseded, VerifiedBy FROM dbo.VerificationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        verBefore!.IsSuperseded.Should().BeFalse();

        // Act - Recurring telemetry finding is ingested while issue is Investigating
        var recurrenceRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 60,
            Findings = [finding]
        };

        var recurrenceResp = await _client.SendAsync(CreateTelemetryRequest(recurrenceRequest));

        // Assert
        recurrenceResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var recurrenceBody = await recurrenceResp.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        recurrenceBody!.Results[0].Action.Should().Be("Appended");

        // Active verification record is now marked as IsSuperseded = 1
        var verAfter = await _factory.QuerySingleOrDefaultSqlAsync<VerificationRow>(
            "SELECT Id, CleanPassed, IsSuperseded, VerifiedBy FROM dbo.VerificationRecords WHERE IssueId = @IssueId;",
            new { IssueId = issueId });
        verAfter.Should().NotBeNull();
        verAfter!.IsSuperseded.Should().BeTrue();

        // Activity log contains VerificationInvalidated
        var invalidationLog = await _factory.QuerySingleOrDefaultSqlAsync<ActivityLogRow>(
            "SELECT ActivityType, ActorName, Summary, DetailsJson FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'VerificationInvalidated';",
            new { IssueId = issueId });
        invalidationLog.Should().NotBeNull();
        invalidationLog!.Summary.Should().Contain("recurred during investigation");
    }
}
