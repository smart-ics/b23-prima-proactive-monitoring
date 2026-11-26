using System.Data;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.Enrollment.RevokeEnrollmentToken;

public sealed class RevokeEnrollmentTokenHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string SelectTokenSql = """
        SELECT Id, Status
        FROM dbo.DetectorEnrollmentTokens
        WHERE Id = @Id;
        """;

    private const string RevokeTokenSql = """
        UPDATE dbo.DetectorEnrollmentTokens
        SET Status = 'Revoked'
        WHERE Id = @Id AND Status = 'Pending';
        """;

    public RevokeEnrollmentTokenHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var selectCommand = new CommandDefinition(SelectTokenSql, new { Id = id }, cancellationToken: cancellationToken);
        var existing = await connection.QuerySingleOrDefaultAsync<TokenStatusRecord>(selectCommand);

        if (existing is null)
        {
            return Result.Failure(EnrollmentTokenErrors.NotFound(id));
        }

        if (string.Equals(existing.Status, "Consumed", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(EnrollmentTokenErrors.AlreadyConsumed);
        }

        if (string.Equals(existing.Status, "Revoked", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(EnrollmentTokenErrors.AlreadyRevoked);
        }

        if (!string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(DomainError.Conflict("EnrollmentToken.InvalidStatus", $"Cannot revoke enrollment token with status '{existing.Status}'."));
        }

        var updateCommand = new CommandDefinition(RevokeTokenSql, new { Id = id }, cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(updateCommand);

        if (rowsAffected == 0)
        {
            // Concurrently modified
            return Result.Failure(EnrollmentTokenErrors.AlreadyConsumed);
        }

        return Result.Success();
    }

    private sealed record TokenStatusRecord(Guid Id, string Status);
}
