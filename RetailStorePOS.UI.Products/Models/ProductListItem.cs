using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Products.Models;

public sealed partial class ProductListItem
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
    public string? ThumbnailPath => Product.ThumbnailPath;
    public string BarcodeText => string.IsNullOrWhiteSpace(Product.Barcode) ? "—" : Product.Barcode!;
    public string SkuText => string.IsNullOrWhiteSpace(Product.Sku) ? "—" : Product.Sku!;
    public string UnitText => string.IsNullOrWhiteSpace(Product.Unit) ? "—" : Product.Unit!;
    public string PriceText => ProductPriceFormatter.Format(Product.Price);
    public string StoreQuantityText => ProductPriceFormatter.FormatNumber(Product.QuantityStore);
    public string WarehouseQuantityText => ProductPriceFormatter.FormatNumber(Product.QuantityWarehouse);
    public string TotalQuantityText => ProductPriceFormatter.FormatNumber(Product.TotalQuantity);
    public string StockSummaryText => LocalizationHelper.Format("Products_StockSummary_Format", StoreQuantityText, WarehouseQuantityText, TotalQuantityText);
    public string InventoryHealthText => Product.HasLegacyStockAlert
        ? LocalizationHelper.GetString("Products_Inventory_LegacyAlert")
        : Product.IsShelfOutOfStock || Product.IsWarehouseOutOfStock || Product.HasShelfLowAlert || Product.HasWarehouseLowAlert
            ? LocalizationHelper.GetString("Products_Inventory_AttentionNeeded")
            : LocalizationHelper.GetString("Products_Inventory_Healthy");
    public long TaxCategoryId => Product.TaxCategoryId;
    public decimal TaxRatePercent => Product.TaxRatePercent;
}

internal static class ProductPriceFormatter
{
    public static string Format(decimal amount)
    {
        return CurrencyDisplayHelper.FormatConfiguredAmount(amount);
    }

    public static string FormatNumber(decimal amount)
    {
        return CurrencyDisplayHelper.FormatNumber(amount);
    }

    public static string FormatDate(DateTime value)
    {
        return CurrencyDisplayHelper.FormatDate(value);
    }

    public static string FormatDateTime(DateTime value)
    {
        return CurrencyDisplayHelper.FormatDateTime(value);
    }
}

