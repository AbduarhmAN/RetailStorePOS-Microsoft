using RetailStorePOS.Data.Modules.Inventory;

namespace RetailStorePOS.Data.Modules.Products;

public class Product
{
    public long Id { get; set; }
    public string? ProductDna { get; set; }
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
    public decimal TotalQuantity => InventoryStockRules.GetTotalQuantity(this);
    public decimal MinThresholdStore { get; set; } = 5m;
    public decimal MinThresholdWarehouse { get; set; } = 10m;
    public DateTime? PurchasedAt { get; set; }
    public DateTime? LastSaleAt { get; set; }
    public string? ThumbnailPath { get; set; }
    public string CashierName { get; set; } = string.Empty;

    public bool IsShelfOutOfStock => InventoryStockRules.IsShelfOutOfStock(this);
    public bool IsWarehouseOutOfStock => InventoryStockRules.IsWarehouseOutOfStock(this);
    public bool HasShelfLowAlert => InventoryStockRules.HasShelfLowAlert(this);
    public bool HasWarehouseLowAlert => InventoryStockRules.HasWarehouseLowAlert(this);
    public bool HasLegacyStockAlert => InventoryStockRules.HasLegacyStockAlert(this);
}
