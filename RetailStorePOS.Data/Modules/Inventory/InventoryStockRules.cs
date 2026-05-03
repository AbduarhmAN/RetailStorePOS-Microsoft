using System.Globalization;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Products;

namespace RetailStorePOS.Data.Modules.Inventory;

public static class InventoryStockRules
{
    public const string OwnerModuleKey = "Inventory";

    public static decimal GetTotalQuantity(Product product)
    {
        return product.QuantityStore + product.QuantityWarehouse;
    }

    public static bool IsShelfOutOfStock(Product product)
    {
        return product.QuantityStore <= 0;
    }

    public static bool IsWarehouseOutOfStock(Product product)
    {
        return product.QuantityWarehouse <= 0;
    }

    public static bool HasShelfLowAlert(Product product)
    {
        return !IsShelfOutOfStock(product) && product.QuantityStore <= product.MinThresholdStore;
    }

    public static bool HasWarehouseLowAlert(Product product)
    {
        return !IsWarehouseOutOfStock(product) && product.QuantityWarehouse <= product.MinThresholdWarehouse;
    }

    public static bool HasLegacyStockAlert(Product product)
    {
        return GetTotalQuantity(product) > 0
            && product.PurchasedAt.HasValue
            && product.PurchasedAt.Value.ToUniversalTime() <= DateTime.UtcNow.AddYears(-1)
            && (!product.LastSaleAt.HasValue || product.LastSaleAt.Value.ToUniversalTime() <= DateTime.UtcNow.AddYears(-1));
    }

    public static void BindOwnedProductWriteParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("@quantity", (double)product.QuantityStore);
        command.Parameters.AddWithValue("@quantity_store", (double)product.QuantityStore);
        command.Parameters.AddWithValue("@quantity_warehouse", (double)product.QuantityWarehouse);
        command.Parameters.AddWithValue("@min_threshold_store", (double)product.MinThresholdStore);
        command.Parameters.AddWithValue("@min_threshold_warehouse", (double)product.MinThresholdWarehouse);
        command.Parameters.AddWithValue("@purchased_at", ToDbValue(product.PurchasedAt));
        command.Parameters.AddWithValue("@last_sale_at", ToDbValue(product.LastSaleAt));
    }

    public static void ApplyOwnedProductColumns(
        Product product,
        SqliteDataReader reader,
        int quantityStoreOrdinal,
        int quantityWarehouseOrdinal,
        int minThresholdStoreOrdinal,
        int minThresholdWarehouseOrdinal,
        int purchasedAtOrdinal,
        int lastSaleAtOrdinal)
    {
        product.QuantityStore = reader.IsDBNull(quantityStoreOrdinal) ? 0m : (decimal)reader.GetDouble(quantityStoreOrdinal);
        product.QuantityWarehouse = reader.IsDBNull(quantityWarehouseOrdinal) ? 0m : (decimal)reader.GetDouble(quantityWarehouseOrdinal);
        product.MinThresholdStore = reader.IsDBNull(minThresholdStoreOrdinal) ? 5m : (decimal)reader.GetDouble(minThresholdStoreOrdinal);
        product.MinThresholdWarehouse = reader.IsDBNull(minThresholdWarehouseOrdinal) ? 10m : (decimal)reader.GetDouble(minThresholdWarehouseOrdinal);
        product.PurchasedAt = ReadDateTime(reader, purchasedAtOrdinal);
        product.LastSaleAt = ReadDateTime(reader, lastSaleAtOrdinal);
    }

    private static object ToDbValue(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToUniversalTime().ToString("O")
            : DBNull.Value;
    }

    private static DateTime? ReadDateTime(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var text = reader.GetString(ordinal);
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback))
        {
            return fallback;
        }

        return DateTime.TryParse(text, out var looseParsed)
            ? looseParsed
            : null;
    }
}
