namespace PrIMA.Api.Domain.Issues;

public static class SeverityRank
{
    public const byte Critical = 4;
    public const byte High = 3;
    public const byte Medium = 2;
    public const byte Low = 1;

    public static byte From(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Critical => Critical,
        IssueSeverity.High => High,
        IssueSeverity.Medium => Medium,
        IssueSeverity.Low => Low,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, $"Unknown severity: {severity}")
    };

    public static byte From(string severity)
    {
        if (Enum.TryParse<IssueSeverity>(severity, ignoreCase: true, out var parsed))
        {
            return From(parsed);
        }

        throw new ArgumentException($"Invalid issue severity: {severity}", nameof(severity));
    }

    public static IssueSeverity ToSeverity(byte rank) => rank switch
    {
        Critical => IssueSeverity.Critical,
        High => IssueSeverity.High,
        Medium => IssueSeverity.Medium,
        Low => IssueSeverity.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, $"Unknown severity rank: {rank}")
    };
}
