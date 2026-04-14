using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;

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
    public string PriceText => ProductPriceFormatter.Format(Product.Price);
    public string StoreQuantityText => ProductPriceFormatter.FormatNumber(Product.QuantityStore);
    public string WarehouseQuantityText => ProductPriceFormatter.FormatNumber(Product.QuantityWarehouse);
    public string TotalQuantityText => ProductPriceFormatter.FormatNumber(Product.TotalQuantity);
    public string StockSummaryText => $"Store {StoreQuantityText} · Inventory {WarehouseQuantityText} · Total {TotalQuantityText}";
    public string InventoryHealthText => Product.HasLegacyStockAlert
        ? "Legacy stock alert"
        : Product.HasShelfLowAlert || Product.HasWarehouseLowAlert
            ? "Inventory attention needed"
            : "Inventory healthy";
    public long TaxCategoryId => Product.TaxCategoryId;
    public decimal TaxRatePercent => Product.TaxRatePercent;
}

internal static class ProductPriceFormatter
{
    public static string Format(decimal amount)
    {
        var numericAmount = CurrencyDisplayHelper.FormatNumber(amount);
        var currencyLabel = CurrencyDisplayHelper.ResolveConfiguredDisplayCode();
        return string.IsNullOrWhiteSpace(currencyLabel)
            ? numericAmount
            : $"{numericAmount} {currencyLabel}";
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


