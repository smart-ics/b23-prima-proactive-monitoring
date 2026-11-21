using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.StartInvestigation;

public sealed class StartInvestigationHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IIssueRepository _issueRepository;

    public StartInvestigationHandler(ISqlConnectionFactory connectionFactory, IIssueRepository issueRepository)
    {
        _connectionFactory = connectionFactory;
        _issueRepository = issueRepository;
    }

    public async Task<Result> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var officerName = httpContext.GetCurrentOfficerName();
        var utcNow = DateTime.UtcNow;

        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }

        using var tx = conn.BeginTransaction();

        var issue = await _issueRepository.GetByIdAsync(id, tx, cancellationToken);
        if (issue is null)
        {
            return Result.Failure(IssueErrors.NotFound(id));
        }

        var effectiveAssignee = !string.IsNullOrWhiteSpace(issue.CurrentAssignee)
            ? issue.CurrentAssignee
            : officerName;

        var domainResult = issue.StartInvestigation(effectiveAssignee, utcNow);
        if (domainResult.IsFailure)
        {
            return Result.Failure(domainResult.Error);
        }

        await _issueRepository.StartInvestigationAsync(id, effectiveAssignee, utcNow, conn, tx, cancellationToken);

        var summary = $"Investigation started by {officerName}.";
        var detailsJson = JsonSerializer.Serialize(new
        {
            action = "StartInvestigation",
            officerName,
            assignee = effectiveAssignee,
            timestampUtc = utcNow
        });

        await _issueRepository.InsertActivityLogAsync(
            conn,
            tx,
            id,
            "InvestigationStarted",
            officerName,
            summary,
            detailsJson,
            cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
