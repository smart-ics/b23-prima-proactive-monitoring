namespace PrIMA.Api.Domain.Issues;

public sealed record CorrelationKey
{
    public const int MaxLength = 250;

    public string Value { get; }

    private CorrelationKey(string value)
    {
        Value = value;
    }

    public static CorrelationKey Create(Guid detectorId, string targetEntityReference)
    {
        if (detectorId == Guid.Empty)
        {
            throw new ArgumentException("Detector ID cannot be empty.", nameof(detectorId));
        }

        if (string.IsNullOrWhiteSpace(targetEntityReference))
        {
            throw new ArgumentException("Target entity reference cannot be empty.", nameof(targetEntityReference));
        }

        var canonical = $"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}";
        if (canonical.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Correlation key length ({canonical.Length}) exceeds maximum allowed length of {MaxLength}.",
                nameof(targetEntityReference));
        }

        return new CorrelationKey(canonical);
    }

    public static CorrelationKey FromCanonical(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Correlation key cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Correlation key length ({trimmed.Length}) exceeds maximum allowed length of {MaxLength}.",
                nameof(value));
        }

        return new CorrelationKey(trimmed);
    }

    public override string ToString() => Value;

    public static implicit operator string(CorrelationKey key) => key?.Value ?? string.Empty;

    public static implicit operator CorrelationKey(string value) => FromCanonical(value);
}
