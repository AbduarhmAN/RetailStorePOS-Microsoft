using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Modules.Sales;

namespace RetailStorePOS.Data.Modules.Inventory;

public static class InventorySaleWriter
{
    public const string OwnerModuleKey = "Inventory";
    private static readonly ModuleContract OfflineInventoryAdjustmentContract = InventoryWorkflowContract.InventoryStockAdjustmentCommand;

    public static void ApplySaleDecrement(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SaleItem item,
        DateTime occurredAtUtc)
    {
        EnsureOfflineInventoryAdjustment();

        if (item.ProductId <= 0 || item.Quantity <= 0m)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE products
SET quantity_store = COALESCE(quantity_store, quantity, 0) - @quantity,
    quantity = COALESCE(quantity, quantity_store, 0) - @quantity,
    last_sale_at = @last_sale_at,
    updated_at = @updated_at
WHERE id = @product_id;";
        command.Parameters.AddWithValue("@product_id", item.ProductId);
        command.Parameters.AddWithValue("@quantity", (double)item.Quantity);
        command.Parameters.AddWithValue("@last_sale_at", occurredAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("@updated_at", occurredAtUtc.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void EnsureOfflineInventoryAdjustment()
    {
        if (!OfflineInventoryAdjustmentContract.OfflineAllowed)
        {
            throw new InvalidOperationException($"Inventory contract '{OfflineInventoryAdjustmentContract.ContractKey}' must remain offline-safe.");
        }
    }
}
