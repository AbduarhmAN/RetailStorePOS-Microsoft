using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Products;

namespace RetailStorePOS.Data.Modules.Inventory;

public static class InventoryStockRules
{
    public static decimal GetTotalQuantity(Product product)
        => product.QuantityStore + product.QuantityWarehouse;

    public static bool IsShelfOutOfStock(Product product)
        => product.QuantityStore <= 0;

    public static bool IsWarehouseOutOfStock(Product product)
        => product.QuantityWarehouse <= 0;

    public static bool HasShelfLowAlert(Product product)
        => product.QuantityStore > 0 && product.QuantityStore <= product.MinThresholdStore;

    public static bool HasWarehouseLowAlert(Product product)
        => product.QuantityWarehouse > 0 && product.QuantityWarehouse <= product.MinThresholdWarehouse;

    public static bool HasLegacyStockAlert(Product product)
        => GetTotalQuantity(product) > 0 && GetTotalQuantity(product) <= product.MinThresholdStore;

    public static void BindOwnedProductWriteParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("@quantity_store", product.QuantityStore);
        command.Parameters.AddWithValue("@quantity_warehouse", product.QuantityWarehouse);
        command.Parameters.AddWithValue("@min_threshold_store", product.MinThresholdStore);
        command.Parameters.AddWithValue("@min_threshold_warehouse", product.MinThresholdWarehouse);
    }

    public static void ApplyOwnedProductColumns(Product product, SqliteDataReader reader, int qtyStoreIdx, int qtyWarehouseIdx, int minStoreIdx, int minWarehouseIdx, int purchasedAtIdx, int lastSaleAtIdx)
    {
        product.QuantityStore = reader.IsDBNull(qtyStoreIdx) ? 0 : (decimal)Convert.ToDouble(reader.GetValue(qtyStoreIdx));
        product.QuantityWarehouse = reader.IsDBNull(qtyWarehouseIdx) ? 0 : (decimal)Convert.ToDouble(reader.GetValue(qtyWarehouseIdx));
        product.MinThresholdStore = reader.IsDBNull(minStoreIdx) ? 5 : (decimal)Convert.ToDouble(reader.GetValue(minStoreIdx));
        product.MinThresholdWarehouse = reader.IsDBNull(minWarehouseIdx) ? 10 : (decimal)Convert.ToDouble(reader.GetValue(minWarehouseIdx));
        product.PurchasedAt = reader.IsDBNull(purchasedAtIdx) ? null : DateTime.Parse(reader.GetString(purchasedAtIdx));
        product.LastSaleAt = reader.IsDBNull(lastSaleAtIdx) ? null : DateTime.Parse(reader.GetString(lastSaleAtIdx));
    }
}
