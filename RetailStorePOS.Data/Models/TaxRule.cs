namespace RetailStorePOS.Data.Models;

public class TaxRule
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? TaxAuthorityId { get; set; }
    public string CalcType { get; set; } = "PERCENTAGE"; // PERCENTAGE, FIXED_AMOUNT, TIERED, PER_UNIT_MEASURE, PERCENTAGE_ON_MARGIN, REVERSE_CHARGE
    public string Scope { get; set; } = "PRODUCT"; // PRODUCT, ORDER
    public decimal RateValue { get; set; }
    public bool IsInclusive { get; set; }
    public bool AppliesAfterDiscount { get; set; } = true;
    public int SequenceOrder { get; set; } = 1;

    public long? MinTaxAmountCents { get; set; }
    public long? MaxTaxAmountCents { get; set; }
    
    public long? ThresholdMinCents { get; set; }
    public long? ThresholdMaxCents { get; set; }
    public string? ThresholdScope { get; set; } // ITEM_PRICE, LINE_TOTAL, ORDER_SUBTOTAL
    
    public string? EffectiveFrom { get; set; }
    public string? EffectiveUntil { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;

    // Display helpers
    public string DisplaySummary => CalcType switch
    {
        "PERCENTAGE" => $"{RateValue:G}%",
        "FIXED_AMOUNT" => $"${RateValue:F2}",
        "TIERED" => "Tiered Rates",
        "PER_UNIT_MEASURE" => $"${RateValue:F2}/unit",
        "PERCENTAGE_ON_MARGIN" => $"{RateValue:G}% (Margin)",
        "REVERSE_CHARGE" => "Reverse Charge",
        _ => $"{RateValue}"
    };

    public string InclusiveLabel => IsInclusive ? "Inclusive" : "Exclusive";
    public string ScopeLabel => Scope == "ORDER" ? "Order" : "Product";
}
