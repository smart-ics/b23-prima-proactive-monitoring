using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.AcknowledgeIssue;

public sealed class AcknowledgeIssueHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IIssueRepository _issueRepository;

    public AcknowledgeIssueHandler(ISqlConnectionFactory connectionFactory, IIssueRepository issueRepository)
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

        var domainResult = issue.Acknowledge(officerName, utcNow);
        if (domainResult.IsFailure)
        {
            return Result.Failure(domainResult.Error);
        }

        await _issueRepository.AcknowledgeAsync(id, officerName, utcNow, conn, tx, cancellationToken);

        var summary = $"Issue acknowledged by {officerName}.";
        var detailsJson = JsonSerializer.Serialize(new
        {
            action = "Acknowledge",
            officerName,
            timestampUtc = utcNow
        });

        await _issueRepository.InsertActivityLogAsync(
            conn,
            tx,
            id,
            "Acknowledged",
            officerName,
            summary,
            detailsJson,
            cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
