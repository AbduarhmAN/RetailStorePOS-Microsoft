namespace RetailStorePOS.Data.Models;

public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Sku { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public long TaxCategoryId { get; set; } = 1;
    public long? TaxGroupId { get; set; } = 1;
    public decimal TaxRatePercent { get; set; }
    public decimal QuantityStore { get; set; }
    public decimal QuantityWarehouse { get; set; }
    public decimal Quantity
    {
        get => QuantityStore;
        set => QuantityStore = value;
    }
    public decimal TotalQuantity => QuantityStore + QuantityWarehouse;
    public decimal MinThresholdStore { get; set; } = 5m;
    public decimal MinThresholdWarehouse { get; set; } = 10m;
    public DateTime? PurchasedAt { get; set; }
    public DateTime? LastSaleAt { get; set; }
    public string CashierName { get; set; } = string.Empty;

    public bool HasShelfLowAlert => QuantityStore > 0 && QuantityStore <= MinThresholdStore;
    public bool HasWarehouseLowAlert => QuantityWarehouse > 0 && QuantityWarehouse <= MinThresholdWarehouse;
    public bool HasLegacyStockAlert
        => TotalQuantity > 0
           && PurchasedAt.HasValue
           && PurchasedAt.Value.ToUniversalTime() <= DateTime.UtcNow.AddYears(-1)
           && (!LastSaleAt.HasValue || LastSaleAt.Value.ToUniversalTime() <= DateTime.UtcNow.AddYears(-1));
}
