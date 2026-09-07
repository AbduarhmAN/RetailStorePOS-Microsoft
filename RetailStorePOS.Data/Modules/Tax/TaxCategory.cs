namespace RetailStorePOS.Data.Modules.Tax;

public partial class TaxCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public bool IsDefault { get; set; }
}
