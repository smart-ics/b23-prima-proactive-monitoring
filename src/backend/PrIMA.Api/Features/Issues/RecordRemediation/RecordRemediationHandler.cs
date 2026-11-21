using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.RecordRemediation;

public sealed class RecordRemediationHandler
{
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _connectionFactory;

    public RecordRemediationHandler(IIssueRepository issueRepository, ISqlConnectionFactory connectionFactory)
    {
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result> HandleAsync(
        Guid issueId,
        RecordRemediationRequest request,
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
            var issue = await _issueRepository.GetByIdAsync(issueId, tx, cancellationToken);
            if (issue is null)
            {
                return Result.Failure(IssueErrors.NotFound(issueId));
            }

            var now = DateTime.UtcNow;
            var domainResult = issue.RecordRemediation(request.ActionCategory, request.SummaryDescription, officer.FullName, now);
            if (domainResult.IsFailure)
            {
                return domainResult;
            }

            var actionCategory = Enum.Parse<RemediationActionCategory>(request.ActionCategory, ignoreCase: true).ToString();

            var remediation = new RemediationRecord(
                id: Guid.NewGuid(),
                issueId: issueId,
                actionCategory: actionCategory,
                summaryDescription: request.SummaryDescription.Trim(),
                performedBy: officer.FullName,
                timestampUtc: now,
                isSuperseded: false);

            await _issueRepository.RecordRemediationAsync(remediation, connection, tx, cancellationToken);
            await _issueRepository.SupersedeActiveVerificationsAsync(issueId, connection, tx, cancellationToken);

            var summary = $"Remediation action recorded [{actionCategory}]: {request.SummaryDescription.Trim()}";
            await _issueRepository.InsertActivityLogAsync(
                connection,
                tx,
                issueId,
                activityType: "RemediationRecorded",
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
