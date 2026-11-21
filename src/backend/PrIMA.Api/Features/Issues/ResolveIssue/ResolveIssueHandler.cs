using System.Data;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.ResolveIssue;

public sealed class ResolveIssueHandler
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IIssueRepository _issueRepository;

    public ResolveIssueHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IIssueRepository issueRepository)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _issueRepository = issueRepository;
    }

    public async Task<Result> HandleAsync(
        Guid issueId,
        string officerName,
        CancellationToken cancellationToken = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var tx = connection.BeginTransaction();

        var issue = await _issueRepository.GetIssueWithGovernanceByIdAsync(
            issueId,
            connection,
            tx,
            cancellationToken);

        if (issue is null)
        {
            return Result.Failure(IssueErrors.NotFound(issueId));
        }

        var actor = string.IsNullOrWhiteSpace(officerName) ? "EDP Officer" : officerName.Trim();
        var nowUtc = DateTime.UtcNow;

        var resolveResult = issue.MarkResolved(actor, nowUtc);
        if (resolveResult.IsFailure)
        {
            return resolveResult;
        }

        await _issueRepository.ResolveIssueAsync(
            issue.Id,
            nowUtc,
            connection,
            tx,
            cancellationToken);

        await _issueRepository.InsertActivityLogAsync(
            connection,
            tx,
            issue.Id,
            "Resolved",
            actor,
            "Issue resolved after clean verification passed.",
            null,
            cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
