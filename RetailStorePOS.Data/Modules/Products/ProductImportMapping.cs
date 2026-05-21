namespace RetailStorePOS.Data.Modules.Products;

/// <summary>
/// Explicit mapping from product fields to CSV column headers, supplied by
/// the user from the column-mapping dialog. Any field left null is skipped
/// (the importer treats that field as missing).
/// </summary>
public sealed class ProductImportMapping
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
    public string? ImagePath { get; set; }
    public string? ProductDna { get; set; }
}
