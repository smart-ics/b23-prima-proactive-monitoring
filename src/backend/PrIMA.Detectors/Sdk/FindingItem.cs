namespace PrIMA.Detectors.Sdk;

/// <summary>
/// Represents a discrete operational anomaly finding produced by a detector probe.
/// </summary>
public sealed record FindingItem
{
    public string TargetEntityReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string BaseSeverity { get; init; } = "Medium";
    public FindingEvidenceItem Evidence { get; init; } = new();

    public FindingItem()
    {
    }

    public FindingItem(
        string targetEntityReference,
        string title,
        string baseSeverity,
        FindingEvidenceItem evidence)
    {
        TargetEntityReference = targetEntityReference;
        Title = title;
        BaseSeverity = baseSeverity;
        Evidence = evidence;
    }
}

/// <summary>
/// Telemetry evidence payload capturing diagnostics for an anomaly finding.
/// </summary>
public sealed record FindingEvidenceItem
{
    public string Module { get; init; } = string.Empty;
    public string? SourceTable { get; init; }
    public string? RecordKeys { get; init; }
    public decimal? DebitTotal { get; init; }
    public decimal? CreditTotal { get; init; }
    public decimal? VarianceDelta { get; init; }
    public string? Trace { get; init; }

    public FindingEvidenceItem()
    {
    }

    public FindingEvidenceItem(
        string module,
        string? sourceTable = null,
        string? recordKeys = null,
        decimal? debitTotal = null,
        decimal? creditTotal = null,
        decimal? varianceDelta = null,
        string? trace = null)
    {
        Module = module;
        SourceTable = sourceTable;
        RecordKeys = recordKeys;
        DebitTotal = debitTotal;
        CreditTotal = creditTotal;
        VarianceDelta = varianceDelta;
        Trace = trace;
    }
}
