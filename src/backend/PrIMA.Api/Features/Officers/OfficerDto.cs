namespace PrIMA.Api.Features.Officers;

public sealed record OfficerDto(
    Guid Id,
    string Username,
    string FullName,
    string Role)
{
    public OfficerDto() : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}
