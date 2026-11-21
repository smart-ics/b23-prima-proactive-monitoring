using Dapper;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Officers;

public sealed class GetOfficersHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string GetActiveOfficersSql = """
        SELECT Id, Username, FullName, Role 
        FROM dbo.Officers 
        WHERE IsActive = 1 
        ORDER BY FullName ASC;
        """;

    public GetOfficersHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<OfficerDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(GetActiveOfficersSql, cancellationToken: cancellationToken);
        var officers = await connection.QueryAsync<OfficerDto>(command);
        return officers.AsList();
    }
}
