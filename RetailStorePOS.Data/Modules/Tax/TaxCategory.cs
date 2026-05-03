namespace RetailStorePOS.Data.Modules.Tax;

public enum TaxType
{
    PercentageExclusive,
    PercentageInclusive,
    FixedAmount
}

public class TaxCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public bool IsDefault { get; set; }

    // Phase 1: In-memory only fields (not persisted to DB yet)
    public TaxType Type { get; set; } = TaxType.PercentageExclusive;
    public decimal RateOrAmount { get; set; }

    /// <summary>Returns a human-readable summary of the tax rule.</summary>
    public string DisplaySummary => Type switch
    {
        TaxType.PercentageExclusive => $"{RateOrAmount}% added to subtotal",
        TaxType.PercentageInclusive => $"{RateOrAmount}% included in price",
        TaxType.FixedAmount => $"${RateOrAmount:F2} flat fee",
        _ => $"{RateOrAmount}"
    };

    public string TypeLabel => Type switch
    {
        TaxType.PercentageExclusive => "Exclusive %",
        TaxType.PercentageInclusive => "Inclusive %",
        TaxType.FixedAmount => "Fixed $",
        _ => "Unknown"
    };
}
