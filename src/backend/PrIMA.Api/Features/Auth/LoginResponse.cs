namespace PrIMA.Api.Features.Auth;

public sealed record OfficerDto(Guid Id, string Username, string FullName, string Role);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, OfficerDto Officer);
