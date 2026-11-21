using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.GetDashboardFeed;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class EndToEndManualResolutionSmokeTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ActivityLogRow(
        Guid Id,
        Guid IssueId,
        string ActivityType,
        string ActorName,
        DateTime TimestampUtc,
        string Summary,
        string? DetailsJson);

    public EndToEndManualResolutionSmokeTests(PrIMAApiFactory factory)
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

    [Fact]
    public async Task EndToEnd_ManualResolutionLoop_CompleteFlow_ExecutesCleanly()
    {
        // -------------------------------------------------------------------------
        // STEP 1: Ingest Finding via Telemetry Ingestion Endpoint (Simulator / Prober)
        // -------------------------------------------------------------------------
        var detectorId = await _factory.SeedDetectorAsync(
            name: "EndToEnd Smoke Test Detector",
            scope: "DataIntegrity");

        var testEntityRef = $"E2E-SMOKE-TX-{Guid.NewGuid():N}";
        var ingestRequest = new IngestTelemetryRequest
        {
            DetectorId = detectorId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            TriggerSource = "Scheduled",
            Outcome = "FindingDiscovered",
            DurationMs = 150,
            Findings = new List<FindingItem>
            {
                new()
                {
                    TargetEntityReference = testEntityRef,
                    Title = "End-to-End Smoke Finding Unbalanced Ledger",
                    BaseSeverity = "High",
                    Evidence = new FindingEvidenceItem
                    {
                        Module = "Billing",
                        SourceTable = "BillingJournals",
                        RecordKeys = testEntityRef,
                        DebitTotal = 15_000_000m,
                        CreditTotal = 14_250_000m,
                        VarianceDelta = 750_000m,
                        Trace = "Unbalanced debit credit variance delta of IDR 750.000 in journal entry"
                    }
                }
            }
        };

        var telemetryMessage = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/telemetry")
        {
            Content = JsonContent.Create(ingestRequest)
        };
        telemetryMessage.Headers.Add("X-Detector-API-Key", PrIMAApiFactory.TestApiKey);

        var ingestResponse = await _client.SendAsync(telemetryMessage);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Telemetry ingestion must succeed");

        var ingestResult = await ingestResponse.Content.ReadFromJsonAsync<IngestTelemetryResponse>(JsonOptions);
        ingestResult.Should().NotBeNull();
        ingestResult!.ProcessedFindingsCount.Should().Be(1);
        ingestResult.Results.Should().HaveCount(1);

        var issueId = ingestResult.Results[0].IssueId;

        // -------------------------------------------------------------------------
        // STEP 2: Verify Newly Discovered Issue in Dashboard Feed (S-01)
        // -------------------------------------------------------------------------
        var authClient = await CreateAuthenticatedClientAsync("ahmad.budi"); // Full name: Ahmad Budi

        var feedResponse = await authClient.GetAsync("/api/issues/dashboard-feed");
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var feedData = await feedResponse.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        feedData.Should().NotBeNull();
        var discoveredIssue = feedData!.Items.FirstOrDefault(i => i.Id == issueId);
        discoveredIssue.Should().NotBeNull("Ingested finding must appear in the active dashboard feed");
        discoveredIssue!.CorrelationKey.Should().Contain(testEntityRef.ToUpperInvariant());
        discoveredIssue.Status.Should().Be("Open");
        discoveredIssue.CurrentAssignee.Should().BeNull();
        discoveredIssue.OccurrenceCount.Should().Be(1);

        // Verify initial state via S-02 Issue Detail composite projection
        var initialDetailResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        initialDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialDetail = await initialDetailResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        initialDetail.Should().NotBeNull();
        initialDetail!.Status.Should().Be("Open");
        initialDetail.CurrentAssignee.Should().BeNull();
        initialDetail.Occurrences.Should().HaveCount(1);
        initialDetail.Notes.Should().BeEmpty();
        initialDetail.Remediations.Should().BeEmpty();
        initialDetail.Verifications.Should().BeEmpty();

        // -------------------------------------------------------------------------
        // STEP 3: Execute Single-Tap `Take It` Triage Action (Open -> Investigating)
        // -------------------------------------------------------------------------
        var takeItResponse = await authClient.PostAsync($"/api/issues/{issueId}/take-it", null);
        takeItResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify IssueDetail reflects Investigating and assignee Ahmad Budi
        var afterTakeItResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        var afterTakeItDetail = await afterTakeItResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        afterTakeItDetail!.Status.Should().Be("Investigating");
        afterTakeItDetail.CurrentAssignee.Should().Be("Ahmad Budi");
        afterTakeItDetail.AcknowledgedAtUtc.Should().NotBeNull();

        // -------------------------------------------------------------------------
        // STEP 4: Append Investigation Note (Ctrl+Enter flow)
        // -------------------------------------------------------------------------
        var noteRequest = new AddNoteRequest("Identified unbalanced debit entry in billing journal line items. Commencing ledger reconciliation.");
        var noteResponse = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/notes", noteRequest);
        noteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var afterNoteResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        var afterNoteDetail = await afterNoteResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        afterNoteDetail!.Notes.Should().HaveCount(1);
        afterNoteDetail.Notes[0].OfficerName.Should().Be("Ahmad Budi");
        afterNoteDetail.Notes[0].NoteText.Should().Contain("Identified unbalanced debit entry");

        // -------------------------------------------------------------------------
        // STEP 5: Verify Verification Gate Locks `Mark Resolved` Prior to Remediation (BR-PRIMA-004)
        // -------------------------------------------------------------------------
        var prematureResolveResponse = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        prematureResolveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict, "Resolving without verification must be rejected");

        var conflictProblem = await prematureResolveResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        conflictProblem.Should().NotBeNull();
        conflictProblem!.Detail.Should().Contain("verification gate requires a clean, passed verification record");

        // -------------------------------------------------------------------------
        // STEP 6: Record Remediation Action via Modal M-02
        // -------------------------------------------------------------------------
        var remediationRequest = new RecordRemediationRequest(
            ActionCategory: "DataCorrection",
            SummaryDescription: "Executed ledger reconciliation script and corrected unbalanced debit row according to SOP-FIN-01.");
        var remediationResponse = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation", remediationRequest);
        remediationResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify IssueDetail reflects recorded remediation
        var afterRemediationResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        var afterRemediationDetail = await afterRemediationResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        afterRemediationDetail!.Remediations.Should().HaveCount(1);
        var recordedRemediation = afterRemediationDetail.Remediations[0];
        recordedRemediation.ActionCategory.Should().Be("DataCorrection");
        recordedRemediation.PerformedBy.Should().Be("Ahmad Budi");
        recordedRemediation.SummaryDescription.Should().Contain("SOP-FIN-01");

        // -------------------------------------------------------------------------
        // STEP 7: Verify Verification Gate Locks `Mark Resolved` Prior to Verification (BR-PRIMA-004)
        // -------------------------------------------------------------------------
        var postRemediationResolve = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        postRemediationResolve.StatusCode.Should().Be(HttpStatusCode.Conflict, "Resolving with remediation but without clean verification must be rejected");

        // -------------------------------------------------------------------------
        // STEP 8: Record Clean Manual Verification Outcome via Modal M-03
        // -------------------------------------------------------------------------
        var verificationRequest = new RecordManualVerificationRequest(
            CleanPassed: true,
            EvidenceNotes: "Queried BillingJournals table after reconciliation. Debit equals Credit (IDR 15.000.000), 0 variance delta confirmed.");
        var verificationResponse = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual", verificationRequest);
        verificationResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // -------------------------------------------------------------------------
        // STEP 9: Verify Verification Gate Unlocks (BR-PRIMA-004)
        // -------------------------------------------------------------------------
        var afterVerificationResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        var afterVerificationDetail = await afterVerificationResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        afterVerificationDetail!.Verifications.Should().HaveCount(1);
        var recordedVerification = afterVerificationDetail.Verifications[0];
        recordedVerification.CleanPassed.Should().BeTrue();
        recordedVerification.IsSuperseded.Should().BeFalse();
        recordedVerification.VerifiedBy.Should().Be("Ahmad Budi");
        recordedVerification.EvidenceNotes.Should().Contain("Debit equals Credit");

        // -------------------------------------------------------------------------
        // STEP 10: Execute `Mark Resolved` Action
        // -------------------------------------------------------------------------
        var resolveResponse = await authClient.PostAsync($"/api/issues/{issueId}/resolve", null);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "Resolving an issue with clean verification must succeed");

        // -------------------------------------------------------------------------
        // STEP 11: Verify Issue is Marked Resolved & Removed From Active Feed
        // -------------------------------------------------------------------------
        var finalDetailResponse = await authClient.GetAsync($"/api/issues/{issueId}");
        var finalDetail = await finalDetailResponse.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        finalDetail!.Status.Should().Be("Resolved");
        finalDetail.ResolvedAtUtc.Should().NotBeNull();

        // Feed check: resolved issue is excluded from active items
        var finalFeedResponse = await authClient.GetAsync("/api/issues/dashboard-feed");
        var finalFeed = await finalFeedResponse.Content.ReadFromJsonAsync<GetDashboardFeedResponse>(JsonOptions);
        finalFeed!.Items.Any(i => i.Id == issueId).Should().BeFalse("Resolved issue must be removed from active feed");
        finalFeed.Summary.ResolvedTodayCount.Should().BeGreaterOrEqualTo(1, "Resolved today summary counter must be incremented");

        // -------------------------------------------------------------------------
        // STEP 12: Verify Complete Chronological Activity Audit Log in dbo.IssueActivityLogs
        // -------------------------------------------------------------------------
        var logs = (await _factory.QuerySqlAsync<ActivityLogRow>(
            """
            SELECT Id, IssueId, ActivityType, ActorName, TimestampUtc, Summary, DetailsJson
            FROM dbo.IssueActivityLogs
            WHERE IssueId = @IssueId
            ORDER BY TimestampUtc ASC;
            """,
            new { IssueId = issueId })).ToList();

        logs.Should().HaveCount(6, "Complete manual resolution workflow must record exactly 6 activity events");

        // Event 1: Ingestion finding discovered
        logs[0].ActivityType.Should().Be("Created");
        logs[0].ActorName.Should().Be("System");
        logs[0].Summary.Should().Contain("Initial anomaly discovered");

        // Event 2: Take It
        logs[1].ActivityType.Should().Be("InvestigationStarted");
        logs[1].ActorName.Should().Be("Ahmad Budi");
        logs[1].Summary.Should().Contain("Take It");

        // Event 3: Note added
        logs[2].ActivityType.Should().Be("NoteAdded");
        logs[2].ActorName.Should().Be("Ahmad Budi");
        logs[2].Summary.Should().Contain("Investigation note added");

        // Event 4: Remediation recorded
        logs[3].ActivityType.Should().Be("RemediationRecorded");
        logs[3].ActorName.Should().Be("Ahmad Budi");
        logs[3].Summary.Should().Contain("DataCorrection");

        // Event 5: Clean Manual verification passed
        logs[4].ActivityType.Should().Be("VerificationRecorded");
        logs[4].ActorName.Should().Be("Ahmad Budi");
        logs[4].Summary.Should().Contain("CleanPassed");

        // Event 6: Issue resolved
        logs[5].ActivityType.Should().Be("Resolved");
        logs[5].ActorName.Should().Be("Ahmad Budi");
        logs[5].Summary.Should().Contain("Issue resolved");
    }
}
