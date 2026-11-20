using Microsoft.AspNetCore.Http;

namespace PrIMA.Api.Common;

public sealed record DomainError(string Code, string Message, int StatusCode = StatusCodes.Status400BadRequest)
{
    public static readonly DomainError None = new(string.Empty, string.Empty, StatusCodes.Status200OK);

    public static DomainError Validation(string code, string message) =>
        new(code, message, StatusCodes.Status422UnprocessableEntity);

    public static DomainError NotFound(string code, string message) =>
        new(code, message, StatusCodes.Status404NotFound);

    public static DomainError Conflict(string code, string message) =>
        new(code, message, StatusCodes.Status409Conflict);

    public static DomainError Unauthorized(string code, string message) =>
        new(code, message, StatusCodes.Status401Unauthorized);

    public static DomainError Forbidden(string code, string message) =>
        new(code, message, StatusCodes.Status403Forbidden);

    public static DomainError Unprocessable(string code, string message) =>
        new(code, message, StatusCodes.Status422UnprocessableEntity);
}
