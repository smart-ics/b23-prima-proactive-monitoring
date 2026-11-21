using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Officers;

namespace PrIMA.Api.Features.Issues.AssignIssue;

public sealed class AssignIssueHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IIssueRepository _issueRepository;

    private const string FindActiveOfficerSql = """
        SELECT Id, Username, FullName, Role
        FROM dbo.Officers
        WHERE Id = @OfficerId AND IsActive = 1;
        """;

    public AssignIssueHandler(ISqlConnectionFactory connectionFactory, IIssueRepository issueRepository)
    {
        _connectionFactory = connectionFactory;
        _issueRepository = issueRepository;
    }

    public async Task<Result> HandleAsync(
        Guid id,
        AssignIssueRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var actorName = httpContext.GetCurrentOfficerName();
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

        var officer = await conn.QuerySingleOrDefaultAsync<OfficerDto>(
            new CommandDefinition(
                FindActiveOfficerSql,
                new { OfficerId = request.OfficerId },
                tx,
                cancellationToken: cancellationToken));

        if (officer is null)
        {
            return Result.Failure(DomainError.Unprocessable(
                "Officers.NotFound",
                $"Active officer with ID '{request.OfficerId}' was not found."));
        }

        var targetOfficerName = !string.IsNullOrWhiteSpace(officer.FullName)
            ? officer.FullName.Trim()
            : officer.Username.Trim();

        var previousAssignee = issue.CurrentAssignee;

        var domainResult = issue.Assign(targetOfficerName, utcNow);
        if (domainResult.IsFailure)
        {
            return Result.Failure(domainResult.Error);
        }

        await _issueRepository.AssignAsync(
            issue.Id,
            targetOfficerName,
            issue.Status,
            utcNow,
            conn,
            tx,
            cancellationToken);

        var isReassignment = !string.IsNullOrWhiteSpace(previousAssignee)
            && !string.Equals(previousAssignee, targetOfficerName, StringComparison.OrdinalIgnoreCase);

        var activityType = isReassignment ? "Reassigned" : "Assigned";
        var summary = isReassignment
            ? $"Issue reassigned from {previousAssignee} to {targetOfficerName} by {actorName}."
            : $"Issue assigned to {targetOfficerName} by {actorName}.";

        var detailsJson = JsonSerializer.Serialize(new
        {
            assignedToOfficerId = officer.Id,
            assignedToName = targetOfficerName,
            assignedBy = actorName,
            previousAssignee
        });

        await _issueRepository.InsertActivityLogAsync(
            conn,
            tx,
            issue.Id,
            activityType,
            actorName,
            summary,
            detailsJson,
            cancellationToken);

        tx.Commit();

        return Result.Success();
    }
}
