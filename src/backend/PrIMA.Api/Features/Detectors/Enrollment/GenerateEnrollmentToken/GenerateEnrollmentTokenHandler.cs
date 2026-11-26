using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;

public sealed class GenerateEnrollmentTokenHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string InsertEnrollmentTokenSql = """
        INSERT INTO dbo.DetectorEnrollmentTokens (
            Id,
            TokenPrefix,
            TokenHash,
            Label,
            Status,
            ExpiresAtUtc,
            CreatedByOfficer,
            CreatedAtUtc
        )
        VALUES (
            @Id,
            @TokenPrefix,
            @TokenHash,
            @Label,
            'Pending',
            @ExpiresAtUtc,
            @CreatedByOfficer,
            @CreatedAtUtc
        );
        """;

    public GenerateEnrollmentTokenHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result<GenerateEnrollmentTokenResponse>> HandleAsync(
        GenerateEnrollmentTokenRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var base64Url = WebEncoders.Base64UrlEncode(randomBytes);
        var token = $"PRM-TOKEN-{base64Url}";
        var tokenPrefix = token.Length > 16 ? token[..16] : token;

        var tokenHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToHexString(tokenHashBytes).ToLowerInvariant();

        var id = Guid.NewGuid();
        var officerName = httpContext.GetCurrentOfficerName();
        var utcNow = DateTime.UtcNow;
        var expiresAtUtc = utcNow.AddHours(24);

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var parameters = new
        {
            Id = id,
            TokenPrefix = tokenPrefix,
            TokenHash = tokenHash,
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedByOfficer = officerName,
            CreatedAtUtc = utcNow
        };

        var command = new CommandDefinition(InsertEnrollmentTokenSql, parameters, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);

        var response = new GenerateEnrollmentTokenResponse(
            Id: id,
            Token: token,
            TokenPrefix: tokenPrefix,
            ExpiresAtUtc: expiresAtUtc,
            Label: parameters.Label);

        return Result.Success(response);
    }
}
