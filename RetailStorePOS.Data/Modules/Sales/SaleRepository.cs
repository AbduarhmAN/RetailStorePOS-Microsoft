using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Modules.Inventory;

namespace RetailStorePOS.Data.Modules.Sales;

public sealed class SaleRepository
{
    private static readonly ModuleContract OfflineSalePersistenceContract = CheckoutWorkflowContract.SaleCompletionCommand;
    private static readonly ModuleContract OfflineInventoryAdjustmentContract = InventoryWorkflowContract.InventoryStockAdjustmentCommand;
    private readonly SqliteConnectionFactory _factory;

    public SaleRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public long CreateSale(Sale sale)
    {
        EnsureOfflineSalePersistenceBoundary();

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
        var hasRegisterSessionIdCol = ColumnExists(connection, "sales", "register_session_id");
        var hasCashierUserIdCol = ColumnExists(connection, "sales", "cashier_user_id");

        if (!hasCostCol)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.Transaction = transaction;
            alterCommand.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var saleColumns = new List<string>
        {
            "receipt_number",
            "subtotal_cents",
            "tax_cents",
            "total_cents",
            "tendered_cents",
            "change_cents",
            "payment_type",
            "cashier_name",
            "created_at"
        };
        var saleValues = new List<string>
        {
            "@receipt_number",
            "@subtotal_cents",
            "@tax_cents",
            "@total_cents",
            "@tendered_cents",
            "@change_cents",
            "@payment_type",
            "@cashier_name",
            "@created_at"
        };
        if (hasRegisterSessionIdCol)
        {
            saleColumns.Add("register_session_id");
            saleValues.Add("@register_session_id");
        }

        if (hasCashierUserIdCol)
        {
            saleColumns.Add("cashier_user_id");
            saleValues.Add("@cashier_user_id");
        }

        command.CommandText = $@"
INSERT INTO sales ({string.Join(", ", saleColumns)})
VALUES ({string.Join(", ", saleValues)});
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
        if (hasRegisterSessionIdCol)
        {
            command.Parameters.AddWithValue("@register_session_id", (object?)sale.RegisterSessionId ?? DBNull.Value);
        }

        if (hasCashierUserIdCol)
        {
            command.Parameters.AddWithValue("@cashier_user_id", (object?)sale.CashierUserId ?? DBNull.Value);
        }

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

            InventorySaleWriter.ApplySaleDecrement(connection, transaction, item, createdAt);
        }

        transaction.Commit();
        return receiptNumber;
    }

    private void EnsureOfflineSalePersistenceBoundary()
    {
        if (!OfflineSalePersistenceContract.OfflineAllowed)
        {
            throw new InvalidOperationException($"Sale persistence contract '{OfflineSalePersistenceContract.ContractKey}' must remain offline-safe.");
        }

        if (!OfflineInventoryAdjustmentContract.OfflineAllowed)
        {
            throw new InvalidOperationException($"Inventory contract '{OfflineInventoryAdjustmentContract.ContractKey}' must remain offline-safe.");
        }

        _ = CheckoutWorkflowContract.ResolveSalesCoordinator(this);
    }

    public IEnumerable<Sale> GetSalesByDate(DateTime date)
    {
        return GetSalesByDateRange(date, date);
    }

    public IEnumerable<Sale> FindGlobalSales(string query, int limit = 200)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        var salesProjection = BuildSalesProjection(connection);

        if (long.TryParse(query, out _))
        {
            // Numeric input: match receipts that START WITH the typed digits
            command.CommandText = $@"
                SELECT {salesProjection}
                FROM sales
                WHERE CAST(receipt_number AS TEXT) LIKE @receiptPrefix
                ORDER BY created_at DESC
                LIMIT @limit;";
            command.Parameters.AddWithValue("@receiptPrefix", query + "%");
        }
        else
        {
            // Text input: match cashier name prefix OR exact payment type
            command.CommandText = $@"
                SELECT {salesProjection}
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
            sales.Add(ReadSale(reader));
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
        command.CommandText = $@"
            SELECT {BuildSalesProjection(connection)}
            FROM sales
            WHERE created_at >= @start AND created_at < @end
            ORDER BY created_at DESC;";

        command.Parameters.AddWithValue("@start", startString);
        command.Parameters.AddWithValue("@end", endString);

        using var reader = command.ExecuteReader();
        var sales = new List<Sale>();

        while (reader.Read())
        {
            sales.Add(ReadSale(reader));
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

    public DashboardMetrics GetDashboardMetrics(DateTime startUtc, DateTime endUtc)
    {
        var metrics = new DashboardMetrics();
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();

        // Ensure item_cost_cents column exists (legacy migration)
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        // 1. Sales Header aggregates
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                SUM(total_cents), 
                SUM(subtotal_cents),
                COUNT(*)
            FROM sales
            WHERE created_at >= @start AND created_at < @end;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);

        decimal subtotal = 0;
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read() && !reader.IsDBNull(0))
            {
                metrics.Revenue = MoneyUtils.FromCents(reader.GetInt64(0));
                subtotal = MoneyUtils.FromCents(reader.GetInt64(1));
                metrics.InvoiceCount = reader.GetInt32(2);
            }
        }

        // 2. Sale Items aggregates (COGS and Discounts)
        using var itemCmd = connection.CreateCommand();
        itemCmd.CommandText = @"
            SELECT 
                SUM(si.item_cost_cents * si.quantity),
                SUM(CASE WHEN si.product_id = 0 AND si.total_cents < 0 AND si.name LIKE 'Discount%' THEN ABS(si.total_cents) ELSE 0 END),
                SUM(CASE WHEN si.product_id = 0 AND si.total_cents < 0 AND si.name LIKE 'Discount%' THEN 1 ELSE 0 END)
            FROM sale_items si
            INNER JOIN sales s ON si.sale_id = s.id
            WHERE s.created_at >= @start AND s.created_at < @end;";
        itemCmd.Parameters.AddWithValue("@start", startString);
        itemCmd.Parameters.AddWithValue("@end", endString);

        using (var reader = itemCmd.ExecuteReader())
        {
            if (reader.Read() && !reader.IsDBNull(0))
            {
                var cogs = MoneyUtils.FromCents(reader.GetInt64(0));
                metrics.Profit = subtotal - cogs;
                metrics.DiscountAmount = MoneyUtils.FromCents(reader.IsDBNull(1) ? 0 : reader.GetInt64(1));
                metrics.DiscountCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            }
            else
            {
                metrics.Profit = subtotal;
            }
        }

        // 3. Median calculation
        using var medianCmd = connection.CreateCommand();
        medianCmd.CommandText = @"
            SELECT total_cents
            FROM sales
            WHERE created_at >= @start AND created_at < @end
            ORDER BY total_cents ASC;";
        medianCmd.Parameters.AddWithValue("@start", startString);
        medianCmd.Parameters.AddWithValue("@end", endString);

        var totals = new System.Collections.Generic.List<long>();
        using (var reader = medianCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    totals.Add(reader.GetInt64(0));
                }
            }
        }

        if (totals.Count > 0)
        {
            int mid = totals.Count / 2;
            long medianCents = (totals.Count % 2 != 0)
                ? totals[mid]
                : (totals[mid] + totals[mid - 1]) / 2;
            metrics.MedianInvoice = MoneyUtils.FromCents(medianCents);
        }

        return metrics;
    }

    public List<DailySparklineData> GetDailySparklines(DateTime startUtc, DateTime endUtc)
    {
        var result = new List<DailySparklineData>();
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();

        // Ensure item_cost_cents column exists (legacy migration)
        using var costColCheck = connection.CreateCommand();
        costColCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(costColCheck.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                date(s.created_at, 'localtime') as local_day,
                SUM(s.total_cents) as daily_revenue,
                SUM(s.subtotal_cents) as daily_subtotal,
                COUNT(*) as daily_invoice_count
            FROM sales s
            WHERE s.created_at >= @start AND s.created_at < @end
            GROUP BY local_day
            ORDER BY local_day ASC;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DailySparklineData
            {
                LocalDay = reader.GetString(0),
                Revenue = MoneyUtils.FromCents(reader.IsDBNull(1) ? 0 : reader.GetInt64(1)),
                Profit = MoneyUtils.FromCents(reader.IsDBNull(2) ? 0 : reader.GetInt64(2)), // Storing subtotal temporarily
                InvoiceCount = reader.GetInt32(3)
            });
        }

        // Apply COGS offset
        using var cogsCmd = connection.CreateCommand();
        cogsCmd.CommandText = @"
            SELECT 
                date(s.created_at, 'localtime') as local_day,
                SUM(si.item_cost_cents * si.quantity)
            FROM sale_items si
            INNER JOIN sales s ON si.sale_id = s.id
            WHERE s.created_at >= @start AND s.created_at < @end
            GROUP BY local_day;";
        cogsCmd.Parameters.AddWithValue("@start", startString);
        cogsCmd.Parameters.AddWithValue("@end", endString);

        using var cogsReader = cogsCmd.ExecuteReader();
        while (cogsReader.Read())
        {
            var day = cogsReader.GetString(0);
            var cogs = MoneyUtils.FromCents(cogsReader.IsDBNull(1) ? 0 : cogsReader.GetInt64(1));
            var target = result.FirstOrDefault(r => r.LocalDay == day);
            if (target != null)
            {
                target.Profit -= cogs; // subtotal - cogs
            }
        }

        return result;
    }

    public List<DailySparklineData> GetDashboardSparklines(
        DateTime startUtc,
        DateTime endUtc,
        DashboardSparklineBucket bucket)
    {
        var result = new List<DailySparklineData>();
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");
        var bucketExpression = bucket switch
        {
            DashboardSparklineBucket.Hour => "strftime('%Y-%m-%d %H:00', s.created_at, 'localtime')",
            DashboardSparklineBucket.Month => "strftime('%Y-%m', s.created_at, 'localtime')",
            DashboardSparklineBucket.Year => "strftime('%Y', s.created_at, 'localtime')",
            _ => "date(s.created_at, 'localtime')"
        };

        using var connection = _factory.OpenConnection();

        using var costColCheck = connection.CreateCommand();
        costColCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='item_cost_cents';";
        var hasCostCol = Convert.ToInt64(costColCheck.ExecuteScalar()) > 0;

        if (!hasCostCol)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE sale_items ADD COLUMN item_cost_cents INTEGER NOT NULL DEFAULT 0;";
            alterCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT
                {bucketExpression} as local_bucket,
                SUM(s.total_cents) as bucket_revenue,
                SUM(s.subtotal_cents) as bucket_subtotal,
                COUNT(*) as bucket_invoice_count
            FROM sales s
            WHERE s.created_at >= @start AND s.created_at < @end
            GROUP BY local_bucket
            ORDER BY local_bucket ASC;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(new DailySparklineData
                {
                    LocalDay = reader.GetString(0),
                    Revenue = MoneyUtils.FromCents(reader.IsDBNull(1) ? 0 : reader.GetInt64(1)),
                    Profit = MoneyUtils.FromCents(reader.IsDBNull(2) ? 0 : reader.GetInt64(2)),
                    InvoiceCount = reader.GetInt32(3)
                });
            }
        }

        using var cogsCmd = connection.CreateCommand();
        cogsCmd.CommandText = $@"
            SELECT
                {bucketExpression} as local_bucket,
                SUM(si.item_cost_cents * si.quantity)
            FROM sale_items si
            INNER JOIN sales s ON si.sale_id = s.id
            WHERE s.created_at >= @start AND s.created_at < @end
            GROUP BY local_bucket;";
        cogsCmd.Parameters.AddWithValue("@start", startString);
        cogsCmd.Parameters.AddWithValue("@end", endString);

        using var cogsReader = cogsCmd.ExecuteReader();
        while (cogsReader.Read())
        {
            var bucketKey = cogsReader.GetString(0);
            var cogs = MoneyUtils.FromCents(cogsReader.IsDBNull(1) ? 0 : cogsReader.GetInt64(1));
            var target = result.FirstOrDefault(r => r.LocalDay == bucketKey);
            if (target != null)
            {
                target.Profit -= cogs;
            }
        }

        return result;
    }

    public List<TopProductData> GetTopProducts(DateTime startUtc, DateTime endUtc, int limit = 5)
    {
        var result = new List<TopProductData>();
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                si.name,
                SUM(si.total_cents) as revenue,
                SUM(si.quantity) as qty
            FROM sale_items si
            INNER JOIN sales s ON si.sale_id = s.id
            WHERE s.created_at >= @start AND s.created_at < @end
              AND si.product_id != 0
              AND si.quantity > 0 AND si.total_cents > 0
              AND si.name IS NOT NULL AND si.name != ''
            GROUP BY si.product_id, si.name
            ORDER BY revenue DESC, qty DESC
            LIMIT @limit;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TopProductData
            {
                Name = reader.GetString(0),
                Revenue = MoneyUtils.FromCents(reader.GetInt64(1)),
                Quantity = (int)Math.Round(reader.GetDouble(2))
            });
        }
        return result;
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

    private static string BuildSalesProjection(SqliteConnection connection)
    {
        var columns = new List<string>
        {
            "id",
            "receipt_number",
            "subtotal_cents",
            "tax_cents",
            "total_cents",
            "tendered_cents",
            "change_cents",
            "payment_type",
            "cashier_name",
            "created_at"
        };

        if (ColumnExists(connection, "sales", "register_session_id"))
        {
            columns.Add("register_session_id");
        }

        if (ColumnExists(connection, "sales", "cashier_user_id"))
        {
            columns.Add("cashier_user_id");
        }

        return string.Join(", ", columns);
    }

    private static Sale ReadSale(SqliteDataReader reader)
    {
        var sale = new Sale
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            ReceiptNumber = reader.GetInt64(reader.GetOrdinal("receipt_number")),
            Subtotal = MoneyUtils.FromCents(reader.GetInt64(reader.GetOrdinal("subtotal_cents"))),
            Tax = MoneyUtils.FromCents(reader.GetInt64(reader.GetOrdinal("tax_cents"))),
            Total = MoneyUtils.FromCents(reader.GetInt64(reader.GetOrdinal("total_cents"))),
            Tendered = MoneyUtils.FromCents(reader.GetInt64(reader.GetOrdinal("tendered_cents"))),
            Change = MoneyUtils.FromCents(reader.GetInt64(reader.GetOrdinal("change_cents"))),
            PaymentType = reader.GetString(reader.GetOrdinal("payment_type")),
            CashierName = reader.IsDBNull(reader.GetOrdinal("cashier_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("cashier_name")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))).ToLocalTime()
        };

        var registerSessionIdOrdinal = TryGetOrdinal(reader, "register_session_id");
        if (registerSessionIdOrdinal >= 0 && !reader.IsDBNull(registerSessionIdOrdinal))
        {
            sale.RegisterSessionId = reader.GetInt64(registerSessionIdOrdinal);
        }

        var cashierUserIdOrdinal = TryGetOrdinal(reader, "cashier_user_id");
        if (cashierUserIdOrdinal >= 0 && !reader.IsDBNull(cashierUserIdOrdinal))
        {
            sale.CashierUserId = reader.GetInt64(cashierUserIdOrdinal);
        }

        return sale;
    }

    private static int TryGetOrdinal(SqliteDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

public class DashboardMetrics
{
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public int InvoiceCount { get; set; }
    public decimal DiscountAmount { get; set; }
    public int DiscountCount { get; set; }
    public decimal MedianInvoice { get; set; }
}

public enum DashboardSparklineBucket
{
    Hour,
    Day,
    Month,
    Year
}

public class DailySparklineData
{
    public string LocalDay { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public int InvoiceCount { get; set; }
}

public class TopProductData
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Quantity { get; set; }
}
