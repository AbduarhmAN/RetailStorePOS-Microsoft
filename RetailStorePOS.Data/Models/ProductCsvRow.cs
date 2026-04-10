namespace RetailStorePOS.Data.Models;

public sealed class ProductCsvRow
{
    public string? Name { get; set; }
    public string? Price { get; set; }
    public string? Barcode { get; set; }
    public string? Unit { get; set; }
    public string? Sku { get; set; }
    public string? CostPrice { get; set; }
    public string? QuantityStore { get; set; }
    public string? QuantityWarehouse { get; set; }
    public string? TaxGroupId { get; set; }
}
