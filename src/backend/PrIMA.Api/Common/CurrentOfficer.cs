using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PrIMA.Api.Common;

public sealed record CurrentOfficer(Guid Id, string Username, string FullName, string Role)
{
    public static CurrentOfficer? FromClaimsPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subClaim, out var officerId))
        {
            return null;
        }

        var username = principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? string.Empty;

        var fullName = principal.FindFirst("fullName")?.Value
            ?? principal.FindFirst("FullName")?.Value
            ?? username;

        var role = principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value
            ?? string.Empty;

        return new CurrentOfficer(officerId, username, fullName, role);
    }
}

public static class CurrentOfficerExtensions
{
    public static CurrentOfficer? GetCurrentOfficer(this ClaimsPrincipal? principal) =>
        CurrentOfficer.FromClaimsPrincipal(principal);

    public static CurrentOfficer? GetCurrentOfficer(this HttpContext? context) =>
        CurrentOfficer.FromClaimsPrincipal(context?.User);
}
