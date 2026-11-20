using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PrIMA.Api.Features.Ingestion;

public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Detector-API-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyEndpointFilter(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

        var configuredKeys = _configuration.GetSection("Ingestion:ApiKeys").Get<string[]>()
            ?? _configuration.GetSection("Ingestion:ApiKeys").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToArray();

        var providedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());
        var isValid = false;

        foreach (var key in configuredKeys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (providedBytes.Length == keyBytes.Length &&
                CryptographicOperations.FixedTimeEquals(providedBytes, keyBytes))
            {
                isValid = true;
                break;
            }
        }

        if (!isValid)
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

        return await next(context);
    }
}
