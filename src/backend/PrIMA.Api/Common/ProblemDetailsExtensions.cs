using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PrIMA.Api.Common;

public static class ProblemDetailsExtensions
{
    public static IResult ToProblemDetails(this DomainError error, HttpContext? context = null)
    {
        return Results.Problem(
            statusCode: error.StatusCode,
            title: GetTitleForStatusCode(error.StatusCode),
            detail: error.Message,
            instance: context?.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["errorCode"] = error.Code
            }
        );
    }

    public static ProblemDetails ToProblemDetails(this Exception exception, HttpContext? context = null, bool isDevelopment = false)
    {
        var statusCode = exception switch
        {
            BadHttpRequestException badEx => badEx.StatusCode,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitleForStatusCode(statusCode),
            Detail = isDevelopment ? exception.ToString() : exception.Message,
            Instance = context?.Request.Path
        };

        problem.Extensions["errorCode"] = statusCode switch
        {
            StatusCodes.Status404NotFound => "RESOURCE_NOT_FOUND",
            StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
            StatusCodes.Status409Conflict => "CONFLICT",
            _ => "INTERNAL_SERVER_ERROR"
        };

        return problem;
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        _ => "An error occurred"
    };
}
