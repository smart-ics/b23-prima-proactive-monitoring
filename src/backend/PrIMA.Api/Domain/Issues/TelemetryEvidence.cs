namespace PrIMA.Api.Domain.Issues;

public sealed record TelemetryEvidence
{
    public string Module { get; }
    public string? SourceTable { get; }
    public string? RecordKeys { get; }
    public decimal? DebitTotal { get; }
    public decimal? CreditTotal { get; }
    public decimal? VarianceDelta { get; }
    public string? Trace { get; }

    public string? TraceString => Trace;

    public TelemetryEvidence(
        string module,
        string? sourceTable = null,
        string? recordKeys = null,
        decimal? debitTotal = null,
        decimal? creditTotal = null,
        decimal? varianceDelta = null,
        string? trace = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new ArgumentException("Evidence module cannot be empty.", nameof(module));
        }

        Module = module.Trim();
        SourceTable = sourceTable?.Trim();
        RecordKeys = recordKeys?.Trim();
        DebitTotal = debitTotal;
        CreditTotal = creditTotal;
        VarianceDelta = varianceDelta;
        Trace = trace;
    }
}
