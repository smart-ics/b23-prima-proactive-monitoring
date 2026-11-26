using System.Data;
using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.Enrollment.GetPendingEnrollmentTokens;

public sealed class GetPendingEnrollmentTokensHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetPendingEnrollmentTokensSql = """
        SELECT
            Id,
            TokenPrefix,
            Label,
            Status,
            ExpiresAtUtc,
            CreatedByOfficer,
            CreatedAtUtc
        FROM dbo.DetectorEnrollmentTokens
        WHERE Status = 'Pending'
          AND ExpiresAtUtc > SYSUTCDATETIME()
        ORDER BY CreatedAtUtc DESC;
        """;

    public GetPendingEnrollmentTokensHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<PendingEnrollmentTokenDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(GetPendingEnrollmentTokensSql, cancellationToken: cancellationToken);
        var tokens = await connection.QueryAsync<PendingEnrollmentTokenDto>(command);
        return tokens.AsList();
    }
}
