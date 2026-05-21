using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Sales;

namespace RetailStorePOS.Data.Modules.Inventory;

public static class InventorySaleWriter
{
    public static void ApplySaleDecrement(SqliteConnection connection, SqliteTransaction transaction, SaleItem item, DateTime createdAt)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
UPDATE products
SET quantity_store = MAX(0, quantity_store - @qty),
    quantity = MAX(0, quantity - @qty),
    last_sale_at = @saleAt,
    updated_at = @saleAt
WHERE id = @productId;";
        cmd.Parameters.AddWithValue("@qty", item.Quantity);
        cmd.Parameters.AddWithValue("@productId", item.ProductId);
        cmd.Parameters.AddWithValue("@saleAt", createdAt.ToUniversalTime().ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
