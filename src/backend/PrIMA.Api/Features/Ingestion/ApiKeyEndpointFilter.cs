using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using PrIMA.Api.Common;

namespace PrIMA.Api.Features.Ingestion;

public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Detector-API-Key";
    private readonly IConfiguration _configuration;
    private readonly ISqlConnectionFactory? _connectionFactory;

    public ApiKeyEndpointFilter(IConfiguration configuration, ISqlConnectionFactory? connectionFactory = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var extractedApiKey) ||
            string.IsNullOrWhiteSpace(extractedApiKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"Missing or invalid {HeaderName} header.",
                instance: httpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "UNAUTHORIZED",
                    ["errorCode"] = "UNAUTHORIZED"
                });
        }

        var incomingKeyString = extractedApiKey.ToString();
        var incomingHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(incomingKeyString));

        // 1. Try per-detector hashed key validation
        var detectorId = ExtractDetectorId(context, httpContext);
        if (_connectionFactory != null && detectorId.HasValue && detectorId.Value != Guid.Empty)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                const string sql = "SELECT ApiKeyHash FROM dbo.Detectors WHERE Id = @Id;";
                var dbApiKeyHash = await connection.QuerySingleOrDefaultAsync<string>(
                    new CommandDefinition(sql, new { Id = detectorId.Value }));

                if (!string.IsNullOrWhiteSpace(dbApiKeyHash))
                {
                    var isDetectorKeyMatch = ValidateHashedKey(incomingHashBytes, dbApiKeyHash);
                    if (isDetectorKeyMatch)
                    {
                        return await next(context);
                    }
                }
            }
            catch
            {
                // Fall back to global keys on database error
            }
        }

        // 2. Fallback to global Ingestion:ApiKeys array for backward compatibility
        var configuredKeys = _configuration.GetSection("Ingestion:ApiKeys").Get<string[]>()
            ?? _configuration.GetSection("Ingestion:ApiKeys").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToArray();

        var providedBytes = Encoding.UTF8.GetBytes(incomingKeyString);
        var isGlobalMatch = false;

        foreach (var key in configuredKeys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (providedBytes.Length == keyBytes.Length &&
                CryptographicOperations.FixedTimeEquals(providedBytes, keyBytes))
            {
                isGlobalMatch = true;
                break;
            }
        }

        if (isGlobalMatch)
        {
            return await next(context);
        }

        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: $"Missing or invalid {HeaderName} header.",
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "UNAUTHORIZED",
                ["errorCode"] = "UNAUTHORIZED"
            });
    }

    private static Guid? ExtractDetectorId(EndpointFilterInvocationContext context, HttpContext httpContext)
    {
        foreach (var arg in context.Arguments)
        {
            if (arg is IngestTelemetryRequest req && req.DetectorId != Guid.Empty)
            {
                return req.DetectorId;
            }
        }

        if (httpContext.GetRouteValue("detectorId") is string routeDetIdStr &&
            Guid.TryParse(routeDetIdStr, out var routeDetId))
        {
            return routeDetId;
        }

        if (httpContext.GetRouteValue("id") is string routeIdStr &&
            Guid.TryParse(routeIdStr, out var routeId))
        {
            return routeId;
        }

        if (httpContext.Request.Query.TryGetValue("detectorId", out var qDetId) &&
            Guid.TryParse(qDetId.ToString(), out var qParsedId))
        {
            return qParsedId;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Detector-Id", out var hDetId) &&
            Guid.TryParse(hDetId.ToString(), out var hParsedId))
        {
            return hParsedId;
        }

        return null;
    }

    private static bool ValidateHashedKey(byte[] incomingHashBytes, string storedApiKeyHash)
    {
        var trimmed = storedApiKeyHash.Trim();

        // Check 1: Try hex-decoded bytes
        try
        {
            var storedBytes = Convert.FromHexString(trimmed);
            if (storedBytes.Length == incomingHashBytes.Length &&
                CryptographicOperations.FixedTimeEquals(incomingHashBytes, storedBytes))
            {
                return true;
            }
        }
        catch
        {
            // Not pure hex, check other encodings
        }

        // Check 2: Hex string constant-time comparison
        var incomingHexLowerBytes = Encoding.UTF8.GetBytes(Convert.ToHexString(incomingHashBytes).ToLowerInvariant());
        var storedUtf8Bytes = Encoding.UTF8.GetBytes(trimmed.ToLowerInvariant());

        if (incomingHexLowerBytes.Length == storedUtf8Bytes.Length &&
            CryptographicOperations.FixedTimeEquals(incomingHexLowerBytes, storedUtf8Bytes))
        {
            return true;
        }

        return false;
    }
}
