namespace PrIMA.Api.Features.Ingestion;

public sealed record FindingItem
{
    public string TargetEntityReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string BaseSeverity { get; init; } = string.Empty;
    public FindingEvidenceItem Evidence { get; init; } = null!;
}

public sealed record FindingEvidenceItem
{
    public string Module { get; init; } = string.Empty;
    public string? SourceTable { get; init; }
    public string? RecordKeys { get; init; }
    public decimal? DebitTotal { get; init; }
    public decimal? CreditTotal { get; init; }
    public decimal? VarianceDelta { get; init; }
    public string? Trace { get; init; }
}
