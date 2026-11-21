using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.RecordManualVerification;

public sealed class RecordManualVerificationHandler
{
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _connectionFactory;

    public RecordManualVerificationHandler(IIssueRepository issueRepository, ISqlConnectionFactory connectionFactory)
    {
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result> HandleAsync(
        Guid issueId,
        RecordManualVerificationRequest request,
        CurrentOfficer officer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(officer);

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            if (connection is DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }
        }

        using var tx = connection.BeginTransaction();
        try
        {
            var issue = await _issueRepository.GetIssueWithGovernanceByIdAsync(issueId, connection, tx, cancellationToken);
            if (issue is null)
            {
                return Result.Failure(IssueErrors.NotFound(issueId));
            }

            var now = DateTime.UtcNow;
            var domainResult = issue.RecordVerification(
                method: "ManualQueryConfirmed",
                cleanPassed: request.CleanPassed,
                evidenceNotes: request.EvidenceNotes,
                verifiedBy: officer.FullName,
                timestampUtc: now);

            if (domainResult.IsFailure)
            {
                return domainResult;
            }

            var verification = new VerificationRecord(
                id: Guid.NewGuid(),
                issueId: issueId,
                verificationMethod: "ManualQueryConfirmed",
                cleanPassed: request.CleanPassed,
                evidenceNotes: request.EvidenceNotes.Trim(),
                verifiedBy: officer.FullName,
                timestampUtc: now,
                isSuperseded: false);

            await _issueRepository.RecordVerificationAsync(verification, connection, tx, cancellationToken);

            var outcome = request.CleanPassed ? "CleanPassed" : "AnomalyStillPresent";
            var summary = $"Manual verification recorded: {outcome}. {request.EvidenceNotes.Trim()}";

            await _issueRepository.InsertActivityLogAsync(
                connection,
                tx,
                issueId,
                activityType: "VerificationRecorded",
                actorName: officer.FullName,
                summary: summary,
                detailsJson: null,
                cancellationToken: cancellationToken);

            tx.Commit();

            return Result.Success();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
