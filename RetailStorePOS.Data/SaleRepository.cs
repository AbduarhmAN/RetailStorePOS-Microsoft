using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class SaleRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SaleRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public long CreateSale(Sale sale)
    {
        if (sale.Items.Count == 0)
        {
            throw new InvalidOperationException("Sale has no items.");
        }

        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var receiptNumber = GetNextReceiptNumber(connection, transaction);
        var createdAt = sale.CreatedAt == default ? DateTime.UtcNow : sale.CreatedAt;
        var createdAtText = createdAt.ToUniversalTime().ToString("O");
        sale.ReceiptNumber = receiptNumber;

        using var checkCommand = connection.CreateCommand();
        checkCommand.Transaction = transaction;
        checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.Transaction = transaction;
            alterCommand.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO sales (receipt_number, subtotal_cents, tax_cents, total_cents, tendered_cents, change_cents, payment_type, cashier_name, created_at)
VALUES (@receipt_number, @subtotal_cents, @tax_cents, @total_cents, @tendered_cents, @change_cents, @payment_type, @cashier_name, @created_at);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@receipt_number", receiptNumber);
        command.Parameters.AddWithValue("@subtotal_cents", MoneyUtils.ToCents(sale.Subtotal));
        command.Parameters.AddWithValue("@tax_cents", MoneyUtils.ToCents(sale.Tax));
        command.Parameters.AddWithValue("@total_cents", MoneyUtils.ToCents(sale.Total));
        command.Parameters.AddWithValue("@tendered_cents", MoneyUtils.ToCents(sale.Tendered));
        command.Parameters.AddWithValue("@change_cents", MoneyUtils.ToCents(sale.Change));
        command.Parameters.AddWithValue("@payment_type", sale.PaymentType);
        command.Parameters.AddWithValue("@cashier_name", sale.CashierName ?? string.Empty);
        command.Parameters.AddWithValue("@created_at", createdAtText);

        var saleId = Convert.ToInt64(command.ExecuteScalar());
        sale.Id = saleId;

        foreach (var item in sale.Items)
        {
            using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = @"
INSERT INTO sale_items (sale_id, product_id, name, barcode, price_cents, item_cost_cents, quantity, tax_rate_percent, tax_cents, total_cents, tax_snapshot)
VALUES (@sale_id, @product_id, @name, @barcode, @price_cents, @item_cost_cents, @quantity, @tax_rate_percent, @tax_cents, @total_cents, @tax_snapshot);";

            itemCommand.Parameters.AddWithValue("@sale_id", saleId);
            itemCommand.Parameters.AddWithValue("@product_id", item.ProductId);
            itemCommand.Parameters.AddWithValue("@name", item.Name);
            itemCommand.Parameters.AddWithValue("@barcode", (object?)item.Barcode ?? DBNull.Value);
            itemCommand.Parameters.AddWithValue("@price_cents", MoneyUtils.ToCents(item.Price));
            itemCommand.Parameters.AddWithValue("@item_cost_cents", MoneyUtils.ToCents(item.ItemCost));
            itemCommand.Parameters.AddWithValue("@quantity", (double)item.Quantity);
            itemCommand.Parameters.AddWithValue("@tax_rate_percent", (double)item.TaxRatePercent);
            itemCommand.Parameters.AddWithValue("@tax_cents", MoneyUtils.ToCents(item.TaxAmount));
            itemCommand.Parameters.AddWithValue("@total_cents", MoneyUtils.ToCents(item.LineTotal));
            itemCommand.Parameters.AddWithValue("@tax_snapshot", (object?)item.TaxSnapshot ?? DBNull.Value);

            itemCommand.ExecuteNonQuery();

            using var inventoryCommand = connection.CreateCommand();
            inventoryCommand.Transaction = transaction;
            inventoryCommand.CommandText = @"
UPDATE products
SET quantity_store = COALESCE(quantity_store, quantity, 0) - @quantity,
    quantity = COALESCE(quantity, quantity_store, 0) - @quantity,
    last_sale_at = @last_sale_at,
    updated_at = @updated_at
WHERE id = @product_id;";
            inventoryCommand.Parameters.AddWithValue("@product_id", item.ProductId);
            inventoryCommand.Parameters.AddWithValue("@quantity", (double)item.Quantity);
            inventoryCommand.Parameters.AddWithValue("@last_sale_at", createdAtText);
            inventoryCommand.Parameters.AddWithValue("@updated_at", createdAtText);
            inventoryCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return receiptNumber;
    }

    public IEnumerable<Sale> GetSalesByDate(DateTime date)
    {
        return GetSalesByDateRange(date, date);
    }

    public IEnumerable<Sale> FindGlobalSales(string query, int limit = 200)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();

        if (long.TryParse(query, out _))
        {
            // Numeric input: match receipts that START WITH the typed digits
            command.CommandText = @"
                SELECT id, receipt_number, subtotal_cents, tax_cents, total_cents, tendered_cents, change_cents, payment_type, cashier_name, created_at
                FROM sales
                WHERE CAST(receipt_number AS TEXT) LIKE @receiptPrefix
                ORDER BY created_at DESC
                LIMIT @limit;";
            command.Parameters.AddWithValue("@receiptPrefix", query + "%");
        }
        else
        {
            // Text input: match cashier name prefix OR exact payment type
            command.CommandText = @"
                SELECT id, receipt_number, subtotal_cents, tax_cents, total_cents, tendered_cents, change_cents, payment_type, cashier_name, created_at
                FROM sales
                WHERE cashier_name LIKE @prefix OR payment_type LIKE @prefix
                ORDER BY created_at DESC
                LIMIT @limit;";
        }

        if (!command.Parameters.Contains("@receiptPrefix"))
        {
            command.Parameters.AddWithValue("@prefix", query + "%");
        }
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = command.ExecuteReader();
        var sales = new List<Sale>();

        while (reader.Read())
        {
            sales.Add(new Sale
            {
                Id = reader.GetInt64(0),
                ReceiptNumber = reader.GetInt64(1),
                Subtotal = MoneyUtils.FromCents(reader.GetInt64(2)),
                Tax = MoneyUtils.FromCents(reader.GetInt64(3)),
                Total = MoneyUtils.FromCents(reader.GetInt64(4)),
                Tendered = MoneyUtils.FromCents(reader.GetInt64(5)),
                Change = MoneyUtils.FromCents(reader.GetInt64(6)),
                PaymentType = reader.GetString(7),
                CashierName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)).ToLocalTime()
            });
        }

        if (sales.Count == 0)
        {
            return sales;
        }

        var salesById = sales.ToDictionary(s => s.Id);
        var saleIds = sales.Select(s => s.Id).ToArray();
        var saleIdParameters = saleIds.Select((saleId, index) => $"@saleId{index}").ToArray();

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        using var itemCommand = connection.CreateCommand();
        itemCommand.CommandText = $@"
            SELECT sale_id, product_id, name, barcode, price_cents, quantity, tax_rate_percent, tax_cents, total_cents, tax_snapshot, item_cost_cents
            FROM sale_items
            WHERE sale_id IN ({string.Join(", ", saleIdParameters)})
            ORDER BY sale_id, id;";

        var saleIndex = 0;
        foreach (var saleId in saleIds)
        {
            itemCommand.Parameters.AddWithValue(saleIdParameters[saleIndex++], saleId);
        }

        using var itemReader = itemCommand.ExecuteReader();
        while (itemReader.Read())
        {
            var saleId = itemReader.GetInt64(0);
            if (!salesById.TryGetValue(saleId, out var sale))
            {
                continue;
            }

            sale.Items.Add(new SaleItem
            {
                SaleId = saleId,
                ProductId = itemReader.GetInt64(1),
                Name = itemReader.GetString(2),
                Barcode = itemReader.IsDBNull(3) ? null : itemReader.GetString(3),
                Price = MoneyUtils.FromCents(itemReader.GetInt64(4)),
                Quantity = (decimal)itemReader.GetDouble(5),
                TaxRatePercent = (decimal)itemReader.GetDouble(6),
                TaxAmount = MoneyUtils.FromCents(itemReader.GetInt64(7)),
                LineTotal = MoneyUtils.FromCents(itemReader.GetInt64(8)),
                TaxSnapshot = itemReader.IsDBNull(9) ? null : itemReader.GetString(9),
                ItemCost = MoneyUtils.FromCents(itemReader.GetInt64(10))
            });
        }

        foreach (var sale in sales)
        {
            if (sale.Items.Count == 0)
            {
                continue;
            }

            var itemTax = Math.Round(sale.Items.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
            if (itemTax > 0m && sale.Tax != itemTax)
            {
                sale.Tax = itemTax;
            }
        }

        return sales;
    }

    public IEnumerable<Sale> GetSalesByDateRange(DateTime fromDate, DateTime toDate)
    {
        // created_at is stored in UTC — convert local day boundaries to UTC
        var startUtc = fromDate.Date.ToUniversalTime();
        var endUtc = toDate.Date.AddDays(1).ToUniversalTime();

        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, receipt_number, subtotal_cents, tax_cents, total_cents, tendered_cents, change_cents, payment_type, cashier_name, created_at
            FROM sales
            WHERE created_at >= @start AND created_at < @end
            ORDER BY created_at DESC;";

        command.Parameters.AddWithValue("@start", startString);
        command.Parameters.AddWithValue("@end", endString);

        using var reader = command.ExecuteReader();
        var sales = new List<Sale>();

        while (reader.Read())
        {
            sales.Add(new Sale
            {
                Id = reader.GetInt64(0),
                ReceiptNumber = reader.GetInt64(1),
                Subtotal = MoneyUtils.FromCents(reader.GetInt64(2)),
                Tax = MoneyUtils.FromCents(reader.GetInt64(3)),
                Total = MoneyUtils.FromCents(reader.GetInt64(4)),
                Tendered = MoneyUtils.FromCents(reader.GetInt64(5)),
                Change = MoneyUtils.FromCents(reader.GetInt64(6)),
                PaymentType = reader.GetString(7),
                CashierName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)).ToLocalTime()
            });
        }

        if (sales.Count == 0)
        {
            return sales;
        }

        var salesById = sales.ToDictionary(s => s.Id);
        var saleIds = sales.Select(s => s.Id).ToArray();

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        // Dynamically unlock SQLite parameter thresholds based on the OS kernel
        var chunkSize = Environment.OSVersion.Version.Build >= 22000 ? 15000 : 900;

        foreach (var chunk in saleIds.Chunk(chunkSize))
        {
            var chunkParameters = chunk.Select((id, index) => $"@saleId{index}").ToArray();

            using var itemCommand = connection.CreateCommand();
            itemCommand.CommandText = $@"
                SELECT sale_id, product_id, name, barcode, price_cents, quantity, tax_rate_percent, tax_cents, total_cents, tax_snapshot, item_cost_cents
                FROM sale_items
                WHERE sale_id IN ({string.Join(", ", chunkParameters)})
                ORDER BY sale_id, id;";

            for (int i = 0; i < chunk.Length; i++)
            {
                itemCommand.Parameters.AddWithValue(chunkParameters[i], chunk[i]);
            }

            using var itemReader = itemCommand.ExecuteReader();
            while (itemReader.Read())
            {
                var saleId = itemReader.GetInt64(0);
                if (!salesById.TryGetValue(saleId, out var sale))
                {
                    continue;
                }

                sale.Items.Add(new SaleItem
                {
                    SaleId = saleId,
                    ProductId = itemReader.GetInt64(1),
                    Name = itemReader.GetString(2),
                    Barcode = itemReader.IsDBNull(3) ? null : itemReader.GetString(3),
                    Price = MoneyUtils.FromCents(itemReader.GetInt64(4)),
                    Quantity = (decimal)itemReader.GetDouble(5),
                    TaxRatePercent = (decimal)itemReader.GetDouble(6),
                    TaxAmount = MoneyUtils.FromCents(itemReader.GetInt64(7)),
                    LineTotal = MoneyUtils.FromCents(itemReader.GetInt64(8)),
                    TaxSnapshot = itemReader.IsDBNull(9) ? null : itemReader.GetString(9),
                    ItemCost = MoneyUtils.FromCents(itemReader.GetInt64(10))
                });
            }
        }

        foreach (var sale in sales)
        {
            if (sale.Items.Count == 0)
            {
                continue;
            }

            var itemTax = Math.Round(sale.Items.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
            if (itemTax > 0m && sale.Tax != itemTax)
            {
                sale.Tax = itemTax;
            }
        }

        return sales;
    }


    private static long GetNextReceiptNumber(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = "SELECT next_value FROM receipt_sequence WHERE id = 1;";
        var current = Convert.ToInt64(selectCommand.ExecuteScalar());

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = "UPDATE receipt_sequence SET next_value = @next WHERE id = 1;";
        updateCommand.Parameters.AddWithValue("@next", current + 1);
        updateCommand.ExecuteNonQuery();

        return current;
    }

    public DailySummary GetDailySummary(DateTime date)
    {
        var startOfDay = date.Date.ToString("O");
        var endOfDay = date.Date.AddDays(1).AddTicks(-1).ToString("O");

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(id) as transaction_count,
                SUM(total_cents) as total_revenue,
                SUM(tax_cents) as total_tax,
                SUM(CASE WHEN payment_type = 'Cash' THEN total_cents ELSE 0 END) as cash_total,
                SUM(CASE WHEN payment_type = 'Card' THEN total_cents ELSE 0 END) as card_total
            FROM sales
            WHERE created_at >= @start AND created_at <= @end;";

        command.Parameters.AddWithValue("@start", startOfDay);
        command.Parameters.AddWithValue("@end", endOfDay);

        using var reader = command.ExecuteReader();

        if (reader.Read() && !reader.IsDBNull(0))
        {
            return new DailySummary
            {
                Date = date.Date,
                TransactionCount = reader.GetInt32(0),
                TotalRevenue = MoneyUtils.FromCents(reader.IsDBNull(1) ? 0 : reader.GetInt64(1)),
                TotalTax = MoneyUtils.FromCents(reader.IsDBNull(2) ? 0 : reader.GetInt64(2)),
                CashTotal = MoneyUtils.FromCents(reader.IsDBNull(3) ? 0 : reader.GetInt64(3)),
                CardTotal = MoneyUtils.FromCents(reader.IsDBNull(4) ? 0 : reader.GetInt64(4))
            };
        }

        return new DailySummary { Date = date.Date };
    }
}

public class DailySummary
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
}
