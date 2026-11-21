using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Tests.Fixtures;

namespace PrIMA.Api.Tests.Integration;

[Collection("ApiTestCollection")]
public class GetIssueByIdEndpointTests : IAsyncLifetime
{
    private readonly PrIMAApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GetIssueByIdEndpointTests(PrIMAApiFactory factory)
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
    public async Task GetIssueById_WithoutAuthentication_Returns401Unauthorized()
    {
        // Arrange
        var issueId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/issues/{issueId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetIssueById_WhenIssueDoesNotExist_Returns404NotFoundProblemDetails()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await authClient.GetAsync($"/api/issues/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Detail.Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task GetIssueById_WhenIssueExists_ReturnsCompositeIssueDetailDto()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        var issueId = Guid.NewGuid();
        var inspectionRunId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 1. Seed Detector
        var detectorId = await _factory.SeedDetectorAsync("Unbalanced Journal Detector", "DataIntegrity", "Finance");

        // 2. Seed Inspection Run
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.InspectionRuns (Id, DetectorId, ExecutionTimestampUtc, TriggerSource, Outcome, DurationMs, CreatedAtUtc)
            VALUES (@Id, @DetectorId, @Now, 'Scheduled', 'FindingDiscovered', 120, @Now);
            """,
            new { Id = inspectionRunId, DetectorId = detectorId, Now = now });

        // 3. Seed Issue Metadata
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, 'JOURNAL:UNBALANCED:TX-9988', 'Unbalanced Journal Batch TX-9988', @DetectorId, 'Unbalanced Journal Detector', 'DataIntegrity',
                'High', 'High', 3, 'Investigating', 0,
                2, 'Ahmad Budi', @Now, @Now,
                @Now, NULL, NULL, NULL
            );
            """,
            new { Id = issueId, DetectorId = detectorId, Now = now });

        // 4. Seed Occurrences (Occurrence 1 earlier, Occurrence 2 later)
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Occurrences (Id, IssueId, InspectionRunId, OccurrenceNumber, TimestampUtc, Module, SourceTable, RecordKeys, DebitTotal, CreditTotal, VarianceDelta, TraceString)
            VALUES 
                (@OccId1, @IssueId, @RunId, 1, @Time1, 'Finance', 'dbo.JournalEntries', 'Batch=9988', 1000000, 950000, 50000, 'Trace 1'),
                (@OccId2, @IssueId, @RunId, 2, @Time2, 'Finance', 'dbo.JournalEntries', 'Batch=9988', 1000000, 900000, 100000, 'Trace 2');
            """,
            new
            {
                OccId1 = Guid.NewGuid(),
                OccId2 = Guid.NewGuid(),
                IssueId = issueId,
                RunId = inspectionRunId,
                Time1 = now.AddMinutes(-30),
                Time2 = now
            });

        // 5. Seed Notes (chronological order)
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.InvestigationNotes (Id, IssueId, OfficerName, NoteText, CreatedAtUtc)
            VALUES 
                (@NoteId1, @IssueId, 'Ahmad Budi', 'First note - started check', @NoteTime1),
                (@NoteId2, @IssueId, 'Ahmad Budi', 'Second note - found difference', @NoteTime2);
            """,
            new
            {
                NoteId1 = Guid.NewGuid(),
                NoteId2 = Guid.NewGuid(),
                IssueId = issueId,
                NoteTime1 = now.AddMinutes(-20),
                NoteTime2 = now.AddMinutes(-10)
            });

        // 6. Seed Remediation Records
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.RemediationRecords (Id, IssueId, ActionCategory, SummaryDescription, PerformedBy, TimestampUtc, IsSuperseded)
            VALUES 
                (@RemId1, @IssueId, 'DataCorrection', 'Adjusted missing journal line', 'Ahmad Budi', @RemTime1, 0);
            """,
            new
            {
                RemId1 = Guid.NewGuid(),
                IssueId = issueId,
                RemTime1 = now.AddMinutes(-8)
            });

        // 7. Seed Verification Records
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.VerificationRecords (Id, IssueId, VerificationMethod, CleanPassed, EvidenceNotes, VerifiedBy, TimestampUtc, IsSuperseded)
            VALUES 
                (@VerId1, @IssueId, 'ManualQuery', 1, 'Re-ran SQL check; variance is 0.', 'Ahmad Budi', @VerTime1, 0);
            """,
            new
            {
                VerId1 = Guid.NewGuid(),
                IssueId = issueId,
                VerTime1 = now.AddMinutes(-5)
            });

        // 8. Seed Superseded Dismissal (should NOT appear) & Active Dismissal (should appear if active)
        // For this test, let's keep dismissal superseded or test dismissal filtering
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.DismissalRecords (Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded)
            VALUES 
                (@DisId1, @IssueId, 'FalsePositive', 'Old superseded dismissal explanation here.', 'Officer 1', @DisTime1, 1);
            """,
            new
            {
                DisId1 = Guid.NewGuid(),
                IssueId = issueId,
                DisTime1 = now.AddMinutes(-40)
            });

        // 9. Seed Activity Logs
        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.IssueActivityLogs (Id, IssueId, ActivityType, ActorName, TimestampUtc, Summary, DetailsJson)
            VALUES 
                (@LogId1, @IssueId, 'Created', 'System', @LogTime1, 'Anomaly detected by scheduled run', NULL),
                (@LogId2, @IssueId, 'InvestigationStarted', 'Ahmad Budi', @LogTime2, 'Investigation started by Ahmad Budi via Take It.', '{"action":"TakeIt"}');
            """,
            new
            {
                LogId1 = Guid.NewGuid(),
                LogId2 = Guid.NewGuid(),
                IssueId = issueId,
                LogTime1 = now.AddMinutes(-30),
                LogTime2 = now.AddMinutes(-25)
            });

        // Act
        var response = await authClient.GetAsync($"/api/issues/{issueId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        detail.Should().NotBeNull();

        // 1. Issue Metadata
        detail!.Id.Should().Be(issueId);
        detail.CorrelationKey.Should().Be("JOURNAL:UNBALANCED:TX-9988");
        detail.Title.Should().Be("Unbalanced Journal Batch TX-9988");
        detail.DetectorName.Should().Be("Unbalanced Journal Detector");
        detail.Scope.Should().Be("DataIntegrity");
        detail.CurrentSeverity.Should().Be("High");
        detail.Status.Should().Be("Investigating");
        detail.CurrentAssignee.Should().Be("Ahmad Budi");
        detail.OccurrenceCount.Should().Be(2);

        // 2. Occurrences (ordered newest first: OccurrenceNumber DESC)
        detail.Occurrences.Should().HaveCount(2);
        detail.Occurrences[0].OccurrenceNumber.Should().Be(2);
        detail.Occurrences[0].VarianceDelta.Should().Be(100000);
        detail.Occurrences[1].OccurrenceNumber.Should().Be(1);
        detail.Occurrences[1].VarianceDelta.Should().Be(50000);

        // 3. Notes (chronological: CreatedAtUtc ASC)
        detail.Notes.Should().HaveCount(2);
        detail.Notes[0].NoteText.Should().Be("First note - started check");
        detail.Notes[1].NoteText.Should().Be("Second note - found difference");

        // 4. Remediation records (newest first)
        detail.Remediations.Should().HaveCount(1);
        detail.Remediations[0].ActionCategory.Should().Be("DataCorrection");
        detail.Remediations[0].PerformedBy.Should().Be("Ahmad Budi");
        detail.Remediations[0].IsSuperseded.Should().BeFalse();

        // 5. Verification records (newest first)
        detail.Verifications.Should().HaveCount(1);
        detail.Verifications[0].CleanPassed.Should().BeTrue();
        detail.Verifications[0].EvidenceNotes.Should().Be("Re-ran SQL check; variance is 0.");

        // 6. Dismissal record (only un-superseded; superseded is excluded)
        detail.Dismissal.Should().BeNull();

        // 7. Activity logs (newest first)
        detail.ActivityLogs.Should().HaveCount(2);
        detail.ActivityLogs[0].ActivityType.Should().Be("InvestigationStarted");
        detail.ActivityLogs[1].ActivityType.Should().Be("Created");
    }

    [Fact]
    public async Task GetIssueById_WithActiveDismissal_ReturnsDismissalRecord()
    {
        // Arrange
        var authClient = await CreateAuthenticatedClientAsync();

        var issueId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var detectorId = await _factory.SeedDetectorAsync("Detector Dismissal Test", "DataIntegrity", "Billing");

        await _factory.ExecuteSqlAsync("""
            INSERT INTO dbo.Issues (
                Id, CorrelationKey, Title, DetectorId, DetectorName, Scope,
                BaseSeverity, CurrentSeverity, SeverityRank, Status, EscalationLevel,
                OccurrenceCount, CurrentAssignee, CreatedAtUtc, LastOccurrenceAtUtc,
                AcknowledgedAtUtc, ResolvedAtUtc, DismissedAtUtc, ReopenedAtUtc
            ) VALUES (
                @Id, 'DISMISSED:KEY:1', 'Dismissed Issue', @DetectorId, 'Detector Dismissal Test', 'DataIntegrity',
                'Low', 'Low', 1, 'Dismissed', 0,
                1, 'Ahmad Budi', @Now, @Now,
                @Now, NULL, @Now, NULL
            );

            INSERT INTO dbo.DismissalRecords (Id, IssueId, DismissalReason, JustificationRationale, DismissedBy, TimestampUtc, IsSuperseded)
            VALUES 
                (NEWID(), @Id, 'FalsePositive', 'This is a verified test false positive for dismissal.', 'Ahmad Budi', @Now, 0);
            """,
            new { Id = issueId, DetectorId = detectorId, Now = now });

        // Act
        var response = await authClient.GetAsync($"/api/issues/{issueId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<IssueDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Dismissal.Should().NotBeNull();
        detail.Dismissal!.DismissalReason.Should().Be("FalsePositive");
        detail.Dismissal.JustificationRationale.Should().Be("This is a verified test false positive for dismissal.");
        detail.Dismissal.DismissedBy.Should().Be("Ahmad Budi");
        detail.Dismissal.IsSuperseded.Should().BeFalse();
    }
}
