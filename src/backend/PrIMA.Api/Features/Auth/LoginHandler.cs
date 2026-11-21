using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PrIMA.Api.Common;
using PrIMA.Api.Features.Officers;

namespace PrIMA.Api.Features.Auth;

public sealed class LoginHandler
{
    private static readonly PasswordHasher<object> PasswordHasher = new();
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IConfiguration _configuration;

    public LoginHandler(ISqlConnectionFactory sqlConnectionFactory, IConfiguration configuration)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _configuration = configuration;
    }

    public async Task<Result<LoginResponse>> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT Id, Username, FullName, PasswordHash, Role, IsActive
            FROM dbo.Officers
            WHERE Username = @Username;
            """;

        var username = request.Username?.Trim() ?? string.Empty;
        var officer = await connection.QuerySingleOrDefaultAsync<OfficerRecord>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken));

        if (officer is null)
        {
            return Result.Failure<LoginResponse>(DomainError.Unauthorized(
                "INVALID_CREDENTIALS",
                "Invalid username or password."));
        }

        if (!officer.IsActive)
        {
            return Result.Failure<LoginResponse>(DomainError.Unauthorized(
                "ACCOUNT_DEACTIVATED",
                "Account is deactivated. Contact EDP Lead."));
        }

        var verificationResult = PasswordHasher.VerifyHashedPassword(
            new object(),
            officer.PasswordHash,
            request.Password ?? string.Empty);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Result.Failure<LoginResponse>(DomainError.Unauthorized(
                "INVALID_CREDENTIALS",
                "Invalid username or password."));
        }

        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var expirationHours = _configuration.GetValue<int?>("Jwt:ExpirationHours") ?? 12;
        var expiresAtUtc = DateTime.UtcNow.AddHours(expirationHours);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(signingKey);

        var claims = new List<Claim>
        {
            new("sub", officer.Id.ToString()),
            new("name", officer.Username),
            new("fullName", officer.FullName),
            new("role", officer.Role)
        };

        var jwtToken = new JwtSecurityToken(
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256)
        );

        var tokenString = tokenHandler.WriteToken(jwtToken);

        var officerDto = new OfficerDto(
            officer.Id,
            officer.Username,
            officer.FullName,
            officer.Role);

        return Result.Success(new LoginResponse(tokenString, expiresAtUtc, officerDto));
    }

    private sealed record OfficerRecord(
        Guid Id,
        string Username,
        string FullName,
        string PasswordHash,
        string Role,
        bool IsActive);
}
