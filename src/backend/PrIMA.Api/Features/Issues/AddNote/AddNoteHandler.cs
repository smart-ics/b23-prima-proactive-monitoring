using System.Data;
using System.Data.Common;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.AddNote;

public sealed class AddNoteHandler
{
    private readonly IIssueRepository _issueRepository;
    private readonly ISqlConnectionFactory _connectionFactory;

    public AddNoteHandler(IIssueRepository issueRepository, ISqlConnectionFactory connectionFactory)
    {
        _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result<NoteDto>> HandleAsync(
        Guid issueId,
        AddNoteRequest request,
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
                return Result<NoteDto>.Failure(IssueErrors.NotFound(issueId));
            }

            var now = DateTime.UtcNow;
            var domainResult = issue.AddNote(request.NoteText, officer.FullName, now);
            if (domainResult.IsFailure)
            {
                return Result<NoteDto>.Failure(domainResult.Error);
            }

            var note = new InvestigationNote(
                id: Guid.NewGuid(),
                issueId: issueId,
                officerName: officer.FullName,
                noteText: request.NoteText.Trim(),
                createdAtUtc: now);

            await _issueRepository.AddNoteAsync(note, connection, tx, cancellationToken);

            await _issueRepository.InsertActivityLogAsync(
                connection,
                tx,
                issueId,
                activityType: "NoteAdded",
                actorName: officer.FullName,
                summary: $"Investigation note added by {officer.FullName}.",
                detailsJson: null,
                cancellationToken: cancellationToken);

            tx.Commit();

            return Result<NoteDto>.Success(new NoteDto(
                note.Id,
                note.IssueId,
                note.OfficerName,
                note.NoteText,
                note.CreatedAtUtc));
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
