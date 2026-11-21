using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class NotesRemediationVerificationEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotesRemediationVerificationEndpointTests(PrIMAApiFactory factory)
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

    private async Task<Guid> SeedIssueAsync(string status = "Investigating", string currentAssignee = "Ahmad Budi")
    {
        var detectorId = await _factory.SeedDetectorAsync($"Detector {Guid.NewGuid():N}");
        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        const string sql = """
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc
            ) VALUES (
                @Id, @CorrelationKey, 'Test Issue for Notes/Remediation', @DetectorId, 'Test Billing Detector', 'DataIntegrity',
                'High', 'High', 3, @Status, 0,
                1, @CurrentAssignee, @Now, @Now
            );
            """;

        await _factory.ExecuteSqlAsync(sql, new
        {
            Id = issueId,
            CorrelationKey = $"TEST:CORR:{issueId:N}",
            DetectorId = detectorId,
            Status = status,
            CurrentAssignee = currentAssignee,
            Now = now
        });

        return issueId;
    }

    // --- Add Note Endpoint Tests ---

    [Fact]
    public async Task AddNote_WithoutAuth_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest("Test note"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddNote_WithEmptyText_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest("   "));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AddNote_WhenIssueNotFound_Returns404NotFound()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{nonExistentId}/notes", new AddNoteRequest("A valid note"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddNote_WhenIssueClosed_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Resolved");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest("A note on resolved issue"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddNote_WhenValid_Returns201Created_AndPersistsNoteAndActivityLog()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Investigating");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/notes", new AddNoteRequest("Found incorrect GL balance in journal entry."));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var noteDto = await response.Content.ReadFromJsonAsync<NoteDto>(JsonOptions);
        noteDto.Should().NotBeNull();
        noteDto!.IssueId.Should().Be(issueId);
        noteDto.NoteText.Should().Be("Found incorrect GL balance in journal entry.");
        noteDto.OfficerName.Should().Be("Ahmad Budi");

        // Verify database persistence in dbo.InvestigationNotes
        var notes = await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.InvestigationNotes WHERE IssueId = @IssueId",
            new { IssueId = issueId });
        notes.Should().HaveCount(1);

        // Verify audit log in dbo.IssueActivityLogs
        var activityLogs = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'NoteAdded'",
            new { IssueId = issueId })).ToList();
        activityLogs.Should().HaveCount(1);
        string summary = activityLogs[0].Summary;
        summary.Should().Contain("Investigation note added");
    }

    // --- Record Remediation Endpoint Tests ---

    [Fact]
    public async Task RecordRemediation_WithoutAuth_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "Fixed entry"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordRemediation_WithInvalidCategory_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("InvalidCategoryName", "Some description"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RecordRemediation_WithEmptyDescription_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "   "));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RecordRemediation_WhenIssueNotInInvestigating_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Open");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("ServiceRestart", "Restarted integration service"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RecordRemediation_WhenValid_Returns204NoContent_SupersedesActiveVerifications_AndWritesAuditLog()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Investigating");

        // Seed an active verification record beforehand to verify supersession
        var priorVerificationId = Guid.NewGuid();
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.VerificationRecords (
                Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                @Id, @IssueId, 'ManualQueryConfirmed', 0, 'Prior failed verification', 'Ahmad Budi', SYSUTCDATETIME(), 0
            );
            """,
            new { Id = priorVerificationId, IssueId = issueId });

        // Act
        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/remediation",
            new RecordRemediationRequest("DataCorrection", "Posted balancing manual journal entry JRN-4401."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify dbo.RemediationRecords
        var remediations = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.RemediationRecords WHERE IssueId = @IssueId",
            new { IssueId = issueId })).ToList();
        remediations.Should().HaveCount(1);
        string category = remediations[0].ActionCategory;
        category.Should().Be("DataCorrection");

        // Verify prior verification was superseded
        var verifications = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @IssueId",
            new { IssueId = issueId })).ToList();
        verifications.Should().HaveCount(1);
        bool isSuperseded = verifications[0].IsSuperseded;
        isSuperseded.Should().BeTrue();

        // Verify dbo.IssueActivityLogs
        var activityLogs = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'RemediationRecorded'",
            new { IssueId = issueId })).ToList();
        activityLogs.Should().HaveCount(1);
        string summary = activityLogs[0].Summary;
        summary.Should().Contain("DataCorrection");
    }

    // --- Record Manual Verification Endpoint Tests ---

    [Fact]
    public async Task RecordManualVerification_WithoutAuth_Returns401Unauthorized()
    {
        var issueId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "All clean"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordManualVerification_WithEmptyEvidence_Returns422UnprocessableEntity()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = Guid.NewGuid();

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "   "));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RecordManualVerification_WhenNoRemediationExists_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Investigating");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Query verified 0 discrepancy rows"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RecordManualVerification_WhenIssueNotInvestigating_Returns409Conflict()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Acknowledged");

        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(true, "Verified"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RecordManualVerification_CleanPassed_Returns204NoContent_AndWritesActivityLog()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Investigating");

        // Seed remediation first
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.RemediationRecords (
                Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                NEWID(), @IssueId, 'DataCorrection', 'Fixed GL ledger balance', 'Ahmad Budi', SYSUTCDATETIME(), 0
            );
            """,
            new { IssueId = issueId });

        // Act: CleanPassed = true
        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(CleanPassed: true, EvidenceNotes: "Verification query returned zero variance rows."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify verification record
        var verifications = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @IssueId",
            new { IssueId = issueId })).ToList();
        verifications.Should().HaveCount(1);
        bool cleanPassed = verifications[0].CleanPassed;
        cleanPassed.Should().BeTrue();

        // Verify activity log notes CleanPassed
        var activityLogs = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'VerificationRecorded'",
            new { IssueId = issueId })).ToList();
        activityLogs.Should().HaveCount(1);
        string summary = activityLogs[0].Summary;
        summary.Should().Contain("CleanPassed");
    }

    [Fact]
    public async Task RecordManualVerification_AnomalyStillPresent_Returns204NoContent_AndWritesActivityLog()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var issueId = await SeedIssueAsync(status: "Investigating");

        // Seed remediation first
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.RemediationRecords (
                Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded
            ) VALUES (
                NEWID(), @IssueId, 'ServiceRestart', 'Service restart attempted', 'Ahmad Budi', SYSUTCDATETIME(), 0
            );
            """,
            new { IssueId = issueId });

        // Act: CleanPassed = false
        var response = await authClient.PostAsJsonAsync($"/api/issues/{issueId}/verification/manual",
            new RecordManualVerificationRequest(CleanPassed: false, EvidenceNotes: "Query still shows 3 unbalanced transactions."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify verification record
        var verifications = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.VerificationRecords WHERE IssueId = @IssueId",
            new { IssueId = issueId })).ToList();
        verifications.Should().HaveCount(1);
        bool cleanPassed = verifications[0].CleanPassed;
        cleanPassed.Should().BeFalse();

        // Verify activity log notes AnomalyStillPresent
        var activityLogs = (await _factory.QuerySqlAsync<dynamic>(
            "SELECT * FROM dbo.IssueActivityLogs WHERE IssueId = @IssueId AND ActivityType = 'VerificationRecorded'",
            new { IssueId = issueId })).ToList();
        activityLogs.Should().HaveCount(1);
        string summary = activityLogs[0].Summary;
        summary.Should().Contain("AnomalyStillPresent");
    }
}
