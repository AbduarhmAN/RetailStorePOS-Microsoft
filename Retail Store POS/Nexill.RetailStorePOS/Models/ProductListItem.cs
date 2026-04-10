using RetailStorePOS.Data.Models;

namespace RetailStorePOS.WinUiLogin.Models;

public sealed class ProductListItem
{
    public ProductListItem(Product product)
    {
        Product = product;
    }

    public Product Product { get; }

    public long Id => Product.Id;
    public string Name => Product.Name;
    public string? Barcode => Product.Barcode;
    public string? Sku => Product.Sku;
    public string? Unit => Product.Unit;
    public decimal Price => Product.Price;
    public string BarcodeText => string.IsNullOrWhiteSpace(Product.Barcode) ? "—" : Product.Barcode!;
    public string SkuText => string.IsNullOrWhiteSpace(Product.Sku) ? "—" : Product.Sku!;
    public string UnitText => string.IsNullOrWhiteSpace(Product.Unit) ? "—" : Product.Unit!;
    public string PriceText => Product.Price.ToString("C2");
    public string StoreQuantityText => Product.QuantityStore.ToString("N2");
    public string WarehouseQuantityText => Product.QuantityWarehouse.ToString("N2");
    public string TotalQuantityText => Product.TotalQuantity.ToString("N2");
    public string StockSummaryText => $"Store {StoreQuantityText} · Inventory {WarehouseQuantityText} · Total {TotalQuantityText}";
    public string InventoryHealthText => Product.HasLegacyStockAlert
        ? "Legacy stock alert"
        : Product.HasShelfLowAlert || Product.HasWarehouseLowAlert
            ? "Inventory attention needed"
            : "Inventory healthy";
    public long TaxCategoryId => Product.TaxCategoryId;
    public decimal TaxRatePercent => Product.TaxRatePercent;
}


