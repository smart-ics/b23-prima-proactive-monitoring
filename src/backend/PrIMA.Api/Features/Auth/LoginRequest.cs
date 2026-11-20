namespace PrIMA.Api.Features.Auth;

public sealed record LoginRequest(string Username = "", string Password = "");
