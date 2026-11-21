using PrIMA.Api.Features.Officers;

namespace PrIMA.Api.Features.Auth;

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, OfficerDto Officer);
