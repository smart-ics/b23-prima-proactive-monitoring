using System.Data;
using System.Text.Json;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.DismissIssue;

public sealed class DismissIssueHandler
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IIssueRepository _issueRepository;

    public DismissIssueHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IIssueRepository issueRepository)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _issueRepository = issueRepository;
    }

    public async Task<Result> HandleAsync(
        Guid issueId,
        DismissIssueRequest request,
        string officerName,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result.Failure(DomainError.Unprocessable("Issues.NullRequest", "Request body is required."));
        }

        if (string.IsNullOrWhiteSpace(request.DismissalReason) ||
            !Enum.TryParse<DismissalReason>(request.DismissalReason.Trim(), true, out var reasonEnum))
        {
            return Result.Failure(IssueErrors.InvalidDismissalReason());
        }

        var actor = string.IsNullOrWhiteSpace(officerName) ? "EDP Officer" : officerName.Trim();
        var nowUtc = DateTime.UtcNow;

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

        var dismissResult = issue.Dismiss(
            reasonEnum.ToString(),
            request.JustificationRationale,
            actor,
            nowUtc);

        if (dismissResult.IsFailure)
        {
            return dismissResult;
        }

        await _issueRepository.DismissIssueAsync(
            issue.Id,
            issue.Dismissal!,
            connection,
            tx,
            cancellationToken);

        var detailsJson = JsonSerializer.Serialize(new
        {
            reason = reasonEnum.ToString(),
            rationale = request.JustificationRationale?.Trim()
        });

        var summary = $"Issue dismissed ({reasonEnum}): {request.JustificationRationale?.Trim()}";
        if (summary.Length > 500)
        {
            summary = summary[..497] + "...";
        }

        await _issueRepository.InsertActivityLogAsync(
            connection,
            tx,
            issue.Id,
            "Dismissed",
            actor,
            summary,
            detailsJson,
            cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
