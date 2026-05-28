using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Read-only aggregations powering the Advanced Analytics pages. Lives in the
/// Reporting module per the modular monolith ownership map; queries the Sales
/// module's tables directly because Reporting is read-only and Sales owns the
/// schema.
///
/// All methods are synchronous and short — call sites should wrap them in
/// <c>Task.Run</c> when they originate on the UI thread.
/// </summary>
public sealed class AdvancedReportsQueryService
{
    private readonly SqliteConnectionFactory _factory;

    public AdvancedReportsQueryService(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Returns one row per calendar day (UTC) in the range with revenue,
    /// transaction count, and gross profit. Days with no sales are not
    /// emitted; callers fill gaps if they want a continuous series.
    /// </summary>
    public List<DailyRevenuePoint> GetRevenueByDay(DateTime startUtc, DateTime endUtc)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();

        // The cost component is computed in a correlated subquery against
        // sale_items so the per-day grouping stays driven by sales.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT
    date(s.created_at)                                       AS day,
    SUM(s.subtotal_cents)                                    AS revenue_cents,
    COALESCE((
        SELECT SUM(si.item_cost_cents * si.quantity)
        FROM sale_items si
        WHERE si.sale_id = s.id
    ), 0)                                                    AS cost_cents,
    COUNT(*)                                                 AS invoice_count
FROM sales s
WHERE s.created_at >= @start AND s.created_at < @end
GROUP BY date(s.created_at)
ORDER BY day ASC;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);

        var points = new List<DailyRevenuePoint>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var day = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var revenue = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
            var cost = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
            var invoices = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

            points.Add(new DailyRevenuePoint(
                day,
                MoneyUtils.FromCents(revenue),
                MoneyUtils.FromCents(revenue - cost),
                invoices));
        }

        return points;
    }

    /// <summary>
    /// Returns the top <paramref name="limit"/> products by revenue in the
    /// period. Excludes synthetic discount rows (<c>product_id = 0</c>).
    /// </summary>
    public List<TopProductRow> GetTopProducts(DateTime startUtc, DateTime endUtc, int limit = 5)
    {
        if (limit <= 0)
        {
            return new List<TopProductRow>();
        }

        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        // Per-line net is computed by allocating the sale's NET subtotal (always
        // tax-excluded at the header level) to each line proportionally to its
        // share of the line-total sum. This is correct under both inclusive
        // tax mode (where line totals include tax) and exclusive tax mode
        // (where line totals already exclude tax). It also handles basket
        // discounts, which are stored as synthetic lines with product_id = 0
        // and negative total_cents — they shrink the denominator and reduce
        // every product's allocated net by their share, matching retail
        // accounting expectations.
        cmd.CommandText = @"
SELECT
    si.product_id,
    si.name,
    SUM(
        (si.total_cents * 1.0 * s.subtotal_cents)
        / NULLIF((SELECT SUM(si2.total_cents) FROM sale_items si2 WHERE si2.sale_id = si.sale_id), 0)
    )                                                                                       AS revenue_cents,
    SUM(si.quantity)                                                                        AS units,
    SUM(
        ((si.total_cents * 1.0 * s.subtotal_cents)
            / NULLIF((SELECT SUM(si2.total_cents) FROM sale_items si2 WHERE si2.sale_id = si.sale_id), 0))
        - (si.item_cost_cents * si.quantity)
    )                                                                                       AS profit_cents
FROM sale_items si
INNER JOIN sales s ON si.sale_id = s.id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND si.product_id <> 0
  AND si.total_cents > 0
GROUP BY si.product_id, si.name
ORDER BY revenue_cents DESC
LIMIT @limit;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);
        cmd.Parameters.AddWithValue("@limit", limit);

        var rows = new List<TopProductRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var productId = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            // revenue_cents and profit_cents come back as REAL because of the
            // proportional allocation. Round to integer cents on the way out.
            var revenueCents = reader.IsDBNull(2)
                ? 0L
                : (long)Math.Round(reader.GetDouble(2));
            var units = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
            var profitCents = reader.IsDBNull(4)
                ? 0L
                : (long)Math.Round(reader.GetDouble(4));

            rows.Add(new TopProductRow(
                productId,
                name,
                MoneyUtils.FromCents(revenueCents),
                units,
                MoneyUtils.FromCents(profitCents)));
        }

        return rows;
    }

    /// <summary>
    /// Returns the busiest hour-of-day and busiest day-of-week within the
    /// period (by transaction count). Hour is 0..23 in local-naive UTC;
    /// day-of-week is SQLite's strftime('%w', ...) — 0=Sunday..6=Saturday.
    /// Returns nulls when the period has no sales.
    /// </summary>
    public BusiestPeriodSnapshot GetBusiestHourAndDay(DateTime startUtc, DateTime endUtc)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        int? hour = null;
        int hourCount = 0;
        int? dow = null;
        int dowCount = 0;

        using var connection = _factory.OpenConnection();

        using (var hourCmd = connection.CreateCommand())
        {
            hourCmd.CommandText = @"
SELECT CAST(strftime('%H', created_at) AS INTEGER) AS hour, COUNT(*) AS cnt
FROM sales
WHERE created_at >= @start AND created_at < @end
GROUP BY hour
ORDER BY cnt DESC
LIMIT 1;";
            hourCmd.Parameters.AddWithValue("@start", startString);
            hourCmd.Parameters.AddWithValue("@end", endString);

            using var reader = hourCmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                hour = reader.GetInt32(0);
                hourCount = reader.GetInt32(1);
            }
        }

        using (var dowCmd = connection.CreateCommand())
        {
            dowCmd.CommandText = @"
SELECT CAST(strftime('%w', created_at) AS INTEGER) AS dow, COUNT(*) AS cnt
FROM sales
WHERE created_at >= @start AND created_at < @end
GROUP BY dow
ORDER BY cnt DESC
LIMIT 1;";
            dowCmd.Parameters.AddWithValue("@start", startString);
            dowCmd.Parameters.AddWithValue("@end", endString);

            using var reader = dowCmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                dow = reader.GetInt32(0);
                dowCount = reader.GetInt32(1);
            }
        }

        return new BusiestPeriodSnapshot(hour, hourCount, dow, dowCount);
    }

    /// <summary>
    /// Period-aggregate KPIs: total revenue, gross profit, transaction count,
    /// average transaction value, and profit margin %. Returned values are
    /// zero when the period has no sales.
    /// </summary>
    public KpiSnapshot GetKpiSnapshot(DateTime startUtc, DateTime endUtc)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        decimal revenue = 0m;
        decimal cost = 0m;
        int transactions = 0;

        using var connection = _factory.OpenConnection();

        using (var headerCmd = connection.CreateCommand())
        {
            headerCmd.CommandText = @"
SELECT SUM(subtotal_cents), COUNT(*)
FROM sales
WHERE created_at >= @start AND created_at < @end;";
            headerCmd.Parameters.AddWithValue("@start", startString);
            headerCmd.Parameters.AddWithValue("@end", endString);

            using var reader = headerCmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                revenue = MoneyUtils.FromCents(reader.GetInt64(0));
                transactions = reader.GetInt32(1);
            }
        }

        using (var costCmd = connection.CreateCommand())
        {
            costCmd.CommandText = @"
SELECT COALESCE(SUM(si.item_cost_cents * si.quantity), 0)
FROM sale_items si
INNER JOIN sales s ON si.sale_id = s.id
WHERE s.created_at >= @start AND s.created_at < @end;";
            costCmd.Parameters.AddWithValue("@start", startString);
            costCmd.Parameters.AddWithValue("@end", endString);

            var raw = costCmd.ExecuteScalar();
            if (raw is not null && raw is not DBNull)
            {
                cost = MoneyUtils.FromCents(Convert.ToInt64(raw));
            }
        }

        var profit = revenue - cost;
        var avgTransaction = transactions > 0 ? revenue / transactions : 0m;
        var marginPercent = revenue > 0m ? Math.Round((profit / revenue) * 100m, 2) : 0m;

        return new KpiSnapshot(revenue, profit, transactions, avgTransaction, marginPercent);
    }

    /// <summary>
    /// Returns one row per priced product with its period statistics:
    /// units sold, net revenue, profit, last-sold date, current store stock.
    /// Products with no sales in the period are still returned (with zeros)
    /// so the UI can split them into Top Sellers / Slow Movers / Dead Stock.
    ///
    /// Net revenue and profit use the same proportional allocation as
    /// <see cref="GetTopProducts"/> so the numbers are consistent in both
    /// inclusive and exclusive tax modes.
    /// </summary>
    public List<ProductPerformanceRow> GetProductPerformance(DateTime startUtc, DateTime endUtc)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT
    p.id                                                                                AS product_id,
    p.name                                                                              AS name,
    COALESCE(p.barcode, '')                                                             AS barcode,
    COALESCE(stats.units, 0)                                                            AS units_sold,
    COALESCE(stats.revenue_cents, 0)                                                    AS revenue_cents,
    COALESCE(stats.profit_cents, 0)                                                     AS profit_cents,
    p.last_sale_at                                                                      AS last_sale_at,
    COALESCE(p.quantity_store, 0)                                                       AS quantity_store
FROM products p
LEFT JOIN (
    SELECT
        si.product_id,
        SUM(si.quantity) AS units,
        SUM(
            (si.total_cents * 1.0 * s.subtotal_cents)
            / NULLIF((SELECT SUM(si2.total_cents) FROM sale_items si2 WHERE si2.sale_id = si.sale_id), 0)
        ) AS revenue_cents,
        SUM(
            ((si.total_cents * 1.0 * s.subtotal_cents)
                / NULLIF((SELECT SUM(si2.total_cents) FROM sale_items si2 WHERE si2.sale_id = si.sale_id), 0))
            - (si.item_cost_cents * si.quantity)
        ) AS profit_cents
    FROM sale_items si
    INNER JOIN sales s ON si.sale_id = s.id
    WHERE s.created_at >= @start
      AND s.created_at <  @end
      AND si.product_id <> 0
    GROUP BY si.product_id
) stats ON stats.product_id = p.id
WHERE p.price_cents > 0
ORDER BY p.name;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);

        var rows = new List<ProductPerformanceRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var productId = reader.GetInt64(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var barcode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var unitsSold = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
            var revenueRaw = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);
            var profitRaw = reader.IsDBNull(5) ? 0d : reader.GetDouble(5);
            DateTime? lastSale = null;
            if (!reader.IsDBNull(6))
            {
                if (DateTime.TryParse(reader.GetString(6), null,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                {
                    lastSale = parsed;
                }
            }
            var stock = reader.IsDBNull(7) ? 0d : reader.GetDouble(7);

            var revenueCents = (long)Math.Round(revenueRaw);
            var profitCents = (long)Math.Round(profitRaw);
            var revenue = MoneyUtils.FromCents(revenueCents);
            var profit = MoneyUtils.FromCents(profitCents);
            var marginPct = revenue > 0m ? Math.Round((profit / revenue) * 100m, 2) : 0m;

            rows.Add(new ProductPerformanceRow(
                productId,
                name,
                string.IsNullOrEmpty(barcode) ? null : barcode,
                unitsSold,
                revenue,
                profit,
                marginPct,
                lastSale,
                (decimal)stock));
        }

        return rows;
    }

    /// <summary>
    /// Returns one entry per (day-of-week, hour) cell with sales count and
    /// net revenue. Drives the Operations page heatmap. Cells with no sales
    /// are not emitted; callers fill the 7x24 grid with zeros.
    ///
    /// Day-of-week is SQLite's strftime('%w', ...) -- 0=Sunday..6=Saturday.
    /// Hour is 0..23 (UTC, since timestamps are UTC).
    /// </summary>
    public List<HourDayCell> GetHourDayHeatmap(DateTime startUtc, DateTime endUtc, string? cashierName = null)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");
        var cashierFilter = NormalizeCashierFilter(cashierName);

        var cells = new List<HourDayCell>();
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT
    CAST(strftime('%w', created_at) AS INTEGER) AS dow,
    CAST(strftime('%H', created_at) AS INTEGER) AS hour,
    COUNT(*)                                    AS tx_count,
    SUM(subtotal_cents)                         AS revenue_cents
FROM sales
WHERE created_at >= @start AND created_at < @end
  AND (@cashier IS NULL OR COALESCE(NULLIF(TRIM(cashier_name), ''), '(unspecified)') = @cashier)
GROUP BY dow, hour;";
        cmd.Parameters.AddWithValue("@start", startString);
        cmd.Parameters.AddWithValue("@end", endString);
        cmd.Parameters.AddWithValue("@cashier", (object?)cashierFilter ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dow = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var hour = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            var txCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var revenueCents = reader.IsDBNull(3) ? 0L : reader.GetInt64(3);
            cells.Add(new HourDayCell(dow, hour, txCount, MoneyUtils.FromCents(revenueCents)));
        }
        return cells;
    }

    /// <summary>
    /// Returns per-cashier performance for the period: sale count, net
    /// revenue, average ticket, items sold.
    ///
    /// Uses two queries (sales-level + items-level) joined client-side
    /// for SQL clarity.
    /// </summary>
    public List<CashierPerformanceRow> GetCashierPerformance(DateTime startUtc, DateTime endUtc, string? cashierName = null)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");
        var cashierFilter = NormalizeCashierFilter(cashierName);

        var byName = new Dictionary<string, (int Sales, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);
        var itemsByName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        using var connection = _factory.OpenConnection();

        using (var salesCmd = connection.CreateCommand())
        {
            salesCmd.CommandText = @"
SELECT
    COALESCE(NULLIF(TRIM(cashier_name), ''), '(unspecified)') AS cashier,
    COUNT(*)                                                  AS sales_count,
    SUM(subtotal_cents)                                       AS revenue_cents
FROM sales
WHERE created_at >= @start AND created_at < @end
  AND (@cashier IS NULL OR COALESCE(NULLIF(TRIM(cashier_name), ''), '(unspecified)') = @cashier)
GROUP BY cashier
ORDER BY revenue_cents DESC;";
            salesCmd.Parameters.AddWithValue("@start", startString);
            salesCmd.Parameters.AddWithValue("@end", endString);
            salesCmd.Parameters.AddWithValue("@cashier", (object?)cashierFilter ?? DBNull.Value);

            using var reader = salesCmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var count = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                var revenueCents = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
                byName[name] = (count, MoneyUtils.FromCents(revenueCents));
            }
        }

        using (var itemsCmd = connection.CreateCommand())
        {
            itemsCmd.CommandText = @"
SELECT
    COALESCE(NULLIF(TRIM(s.cashier_name), ''), '(unspecified)') AS cashier,
    SUM(si.quantity)                                            AS units_sold
FROM sale_items si
INNER JOIN sales s ON si.sale_id = s.id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND (@cashier IS NULL OR COALESCE(NULLIF(TRIM(s.cashier_name), ''), '(unspecified)') = @cashier)
  AND si.product_id <> 0
GROUP BY cashier;";
            itemsCmd.Parameters.AddWithValue("@start", startString);
            itemsCmd.Parameters.AddWithValue("@end", endString);
            itemsCmd.Parameters.AddWithValue("@cashier", (object?)cashierFilter ?? DBNull.Value);

            using var reader = itemsCmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var units = reader.IsDBNull(1) ? 0d : reader.GetDouble(1);
                itemsByName[name] = units;
            }
        }

        var rows = new List<CashierPerformanceRow>(byName.Count);
        foreach (var (name, stats) in byName)
        {
            itemsByName.TryGetValue(name, out var units);
            var avgTicket = stats.Sales > 0 ? stats.Revenue / stats.Sales : 0m;
            var itemsPerTicket = stats.Sales > 0 ? units / stats.Sales : 0d;
            rows.Add(new CashierPerformanceRow(
                name,
                stats.Sales,
                stats.Revenue,
                avgTicket,
                units,
                itemsPerTicket));
        }
        rows.Sort((a, b) => b.Revenue.CompareTo(a.Revenue));
        return rows;
    }

    private static string? NormalizeCashierFilter(string? cashierName)
    {
        return string.IsNullOrWhiteSpace(cashierName) ? null : cashierName.Trim();
    }

    /// <summary>
    /// Classifies priced products into ABC (revenue share) and XYZ (demand
    /// stability) buckets over the supplied period.
    ///
    /// ABC: rank by revenue desc, take cumulative share. A = ≤80%, B = ≤95%,
    /// C = the rest. XYZ: weekly units' coefficient of variation (stddev/mean)
    /// across full ISO weeks in the window. X &lt; 0.5, Y &lt; 1.0, Z &gt;= 1.0
    /// (or "intermittent" – sold on fewer than 3 distinct weeks).
    ///
    /// Products with zero sales in the window are returned with class "Z" so
    /// callers can flag them as candidates for delisting alongside the
    /// regular Dead Stock view.
    /// </summary>
    public List<AbcXyzRow> GetAbcXyzClassification(DateTime startUtc, DateTime endUtc)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        // 1. Per-product revenue + units in the window.
        var products = new Dictionary<long, AbcXyzAccumulator>();
        using var connection = _factory.OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT
    p.id,
    p.name,
    COALESCE(p.barcode, '') AS barcode,
    COALESCE(stats.units, 0) AS units_sold,
    COALESCE(stats.revenue_cents, 0) AS revenue_cents
FROM products p
LEFT JOIN (
    SELECT
        si.product_id,
        SUM(si.quantity) AS units,
        SUM(
            (si.total_cents * 1.0 * s.subtotal_cents)
            / NULLIF((SELECT SUM(si2.total_cents) FROM sale_items si2 WHERE si2.sale_id = si.sale_id), 0)
        ) AS revenue_cents
    FROM sale_items si
    INNER JOIN sales s ON si.sale_id = s.id
    WHERE s.created_at >= @start
      AND s.created_at <  @end
      AND si.product_id <> 0
    GROUP BY si.product_id
) stats ON stats.product_id = p.id
WHERE p.price_cents > 0
ORDER BY revenue_cents DESC;";
            cmd.Parameters.AddWithValue("@start", startString);
            cmd.Parameters.AddWithValue("@end", endString);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var barcode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var units = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
                var revenueRaw = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);
                var revenueCents = (long)Math.Round(revenueRaw);
                products[id] = new AbcXyzAccumulator
                {
                    Id = id,
                    Name = name,
                    Barcode = string.IsNullOrEmpty(barcode) ? null : barcode,
                    UnitsSold = units,
                    Revenue = MoneyUtils.FromCents(revenueCents)
                };
            }
        }

        // 2. Weekly demand for variability (only for products that sold).
        var soldIds = products.Where(kv => kv.Value.UnitsSold > 0).Select(kv => kv.Key).ToList();
        if (soldIds.Count > 0)
        {
            using var weeklyCmd = connection.CreateCommand();
            weeklyCmd.CommandText = @"
SELECT
    si.product_id,
    strftime('%Y-%W', s.created_at) AS yearweek,
    SUM(si.quantity) AS units
FROM sale_items si
INNER JOIN sales s ON si.sale_id = s.id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND si.product_id <> 0
GROUP BY si.product_id, yearweek;";
            weeklyCmd.Parameters.AddWithValue("@start", startString);
            weeklyCmd.Parameters.AddWithValue("@end", endString);

            using var reader = weeklyCmd.ExecuteReader();
            while (reader.Read())
            {
                var pid = reader.GetInt64(0);
                if (!products.TryGetValue(pid, out var acc)) continue;
                var units = reader.IsDBNull(2) ? 0d : reader.GetDouble(2);
                acc.WeeklyUnits.Add(units);
            }
        }

        // 3. Compute ABC and XYZ classes.
        var totalRevenue = products.Values.Sum(p => p.Revenue);
        var ordered = products.Values.OrderByDescending(p => p.Revenue).ToList();

        decimal cumulative = 0m;
        var rows = new List<AbcXyzRow>(ordered.Count);
        foreach (var acc in ordered)
        {
            decimal share = totalRevenue > 0m ? acc.Revenue / totalRevenue : 0m;
            cumulative += share;

            string abc;
            if (totalRevenue <= 0m || acc.Revenue <= 0m) abc = "C";
            else if (cumulative <= 0.80m) abc = "A";
            else if (cumulative <= 0.95m) abc = "B";
            else abc = "C";

            // XYZ: coefficient of variation on weekly demand.
            string xyz;
            decimal cv = 0m;
            if (acc.WeeklyUnits.Count < 3)
            {
                xyz = "Z"; // intermittent or no sales
            }
            else
            {
                var mean = acc.WeeklyUnits.Average();
                if (mean <= 0d)
                {
                    xyz = "Z";
                }
                else
                {
                    var variance = acc.WeeklyUnits.Sum(u => (u - mean) * (u - mean)) / acc.WeeklyUnits.Count;
                    var stddev = Math.Sqrt(variance);
                    cv = (decimal)Math.Round(stddev / mean, 3);
                    xyz = cv < 0.5m ? "X" : cv < 1.0m ? "Y" : "Z";
                }
            }

            rows.Add(new AbcXyzRow(
                acc.Id,
                acc.Name,
                acc.Barcode,
                acc.UnitsSold,
                acc.Revenue,
                Math.Round(share * 100m, 2),
                cv,
                abc,
                xyz));
        }

        return rows;
    }

    /// <summary>
    /// Returns frequently-bought-together product pairs over the period with
    /// support, confidence and lift. Only considers baskets containing 2+
    /// distinct products. Synthetic discount lines (product_id = 0) are
    /// excluded.
    ///
    /// Definitions used (standard market-basket terminology):
    ///   support(X)        = txCount(X) / totalTxCount
    ///   confidence(A→B)   = txCount(A ∧ B) / txCount(A)
    ///   lift(A,B)         = support(A ∧ B) / (support(A) * support(B))
    ///
    /// <paramref name="minPairCount"/> filters out rare combinations to keep
    /// the table actionable. <paramref name="topN"/> returns at most that
    /// many rows ordered by lift desc, then pair count desc.
    /// </summary>
    public List<BasketAffinityRow> GetBasketAffinity(
        DateTime startUtc,
        DateTime endUtc,
        int minPairCount = 2,
        int topN = 100)
    {
        var startString = startUtc.ToString("O");
        var endString = endUtc.ToString("O");

        using var connection = _factory.OpenConnection();

        // 1. Total multi-line basket count (only baskets with 2+ products
        //    contribute to affinity; baskets with a single product can't form
        //    pairs, but we still count them for support's denominator).
        long totalTxCount = 0;
        using (var totalCmd = connection.CreateCommand())
        {
            totalCmd.CommandText = @"
SELECT COUNT(DISTINCT s.id)
FROM sales s
INNER JOIN sale_items si ON si.sale_id = s.id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND si.product_id <> 0
  AND si.quantity   >  0;";
            totalCmd.Parameters.AddWithValue("@start", startString);
            totalCmd.Parameters.AddWithValue("@end", endString);
            var raw = totalCmd.ExecuteScalar();
            if (raw is not null && raw is not DBNull)
            {
                totalTxCount = Convert.ToInt64(raw);
            }
        }

        if (totalTxCount == 0)
        {
            return new List<BasketAffinityRow>();
        }

        // 2. Per-product transaction counts (for support and lift).
        var productTxCount = new Dictionary<long, long>();
        var productName = new Dictionary<long, string>();
        using (var productCmd = connection.CreateCommand())
        {
            productCmd.CommandText = @"
SELECT
    si.product_id,
    MIN(si.name)              AS name,
    COUNT(DISTINCT si.sale_id) AS tx_count
FROM sale_items si
INNER JOIN sales s ON s.id = si.sale_id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND si.product_id <> 0
  AND si.quantity   >  0
GROUP BY si.product_id;";
            productCmd.Parameters.AddWithValue("@start", startString);
            productCmd.Parameters.AddWithValue("@end", endString);
            using var reader = productCmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var count = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
                productTxCount[id] = count;
                productName[id] = name;
            }
        }

        // 3. Distinct ordered pair counts via a self-join on sale_items
        //    (a.product_id < b.product_id ensures each unordered pair is
        //    counted exactly once and removes (X,X) self-pairs).
        var pairs = new List<BasketAffinityRow>();
        using (var pairCmd = connection.CreateCommand())
        {
            pairCmd.CommandText = @"
SELECT
    a.product_id AS a_id,
    b.product_id AS b_id,
    COUNT(DISTINCT a.sale_id) AS pair_count
FROM sale_items a
INNER JOIN sale_items b ON b.sale_id = a.sale_id
                       AND a.product_id < b.product_id
INNER JOIN sales s        ON s.id     = a.sale_id
WHERE s.created_at >= @start
  AND s.created_at <  @end
  AND a.product_id <> 0 AND b.product_id <> 0
  AND a.quantity   >  0 AND b.quantity   >  0
GROUP BY a.product_id, b.product_id
HAVING pair_count >= @minPair;";
            pairCmd.Parameters.AddWithValue("@start", startString);
            pairCmd.Parameters.AddWithValue("@end", endString);
            pairCmd.Parameters.AddWithValue("@minPair", Math.Max(1, minPairCount));

            using var reader = pairCmd.ExecuteReader();
            while (reader.Read())
            {
                var aId = reader.GetInt64(0);
                var bId = reader.GetInt64(1);
                var pairCount = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
                if (pairCount <= 0) continue;

                productTxCount.TryGetValue(aId, out var aCount);
                productTxCount.TryGetValue(bId, out var bCount);
                if (aCount == 0 || bCount == 0) continue;

                var supportA = (decimal)aCount / totalTxCount;
                var supportB = (decimal)bCount / totalTxCount;
                var supportAB = (decimal)pairCount / totalTxCount;
                var confidenceAtoB = (decimal)pairCount / aCount;
                var lift = (supportA == 0m || supportB == 0m)
                    ? 0m
                    : Math.Round(supportAB / (supportA * supportB), 3);

                productName.TryGetValue(aId, out var aName);
                productName.TryGetValue(bId, out var bName);

                pairs.Add(new BasketAffinityRow(
                    aId,
                    aName ?? string.Empty,
                    bId,
                    bName ?? string.Empty,
                    pairCount,
                    Math.Round(supportA * 100m, 2),
                    Math.Round(supportB * 100m, 2),
                    Math.Round(confidenceAtoB * 100m, 2),
                    lift));
            }
        }

        // 4. Sort by lift desc then pair count desc, take top N.
        pairs.Sort((x, y) =>
        {
            var c = y.Lift.CompareTo(x.Lift);
            return c != 0 ? c : y.PairCount.CompareTo(x.PairCount);
        });
        if (topN > 0 && pairs.Count > topN)
        {
            pairs.RemoveRange(topN, pairs.Count - topN);
        }
        return pairs;
    }
}

/// <summary>Accumulator used while building the ABC-XYZ classification.</summary>
file sealed class AbcXyzAccumulator
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public double UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public List<double> WeeklyUnits { get; } = new();
}


/// <summary>
/// One day's revenue rollup. <see cref="Day"/> is the SQLite
/// <c>date(...)</c> string (YYYY-MM-DD).
/// </summary>
public sealed record DailyRevenuePoint(
    string Day,
    decimal Revenue,
    decimal Profit,
    int InvoiceCount);

/// <summary>
/// One row of the top-products ranking. <see cref="Units"/> is a
/// <c>double</c> because <c>sale_items.quantity</c> is REAL in SQLite
/// (allows fractional quantities).
/// </summary>
public sealed record TopProductRow(
    long ProductId,
    string Name,
    decimal Revenue,
    double Units,
    decimal Profit);

/// <summary>
/// Busiest hour-of-day (0..23) and day-of-week (0=Sun..6=Sat) for the period,
/// plus the transaction count at each peak. Both fields are null when the
/// period has no sales.
/// </summary>
public sealed record BusiestPeriodSnapshot(
    int? PeakHour,
    int PeakHourTransactionCount,
    int? PeakDayOfWeek,
    int PeakDayOfWeekTransactionCount);

/// <summary>
/// Period KPI summary used by the Revenue Dashboard tile row.
/// </summary>
public sealed record KpiSnapshot(
    decimal TotalRevenue,
    decimal GrossProfit,
    int TransactionCount,
    decimal AverageTransactionValue,
    decimal ProfitMarginPercent);

/// <summary>
/// One product's performance over the selected period. Powers the
/// Product Performance page (Top Sellers / Slow Movers / Dead Stock tabs).
///
/// <see cref="UnitsSold"/> is a <c>double</c> because <c>sale_items.quantity</c>
/// is REAL in SQLite (allows fractional quantities, e.g. weight-priced items).
/// <see cref="LastSaleAt"/> is the product's overall last sale (not period-bounded);
/// useful for identifying truly dead stock.
/// </summary>
public sealed record ProductPerformanceRow(
    long ProductId,
    string Name,
    string? Barcode,
    double UnitsSold,
    decimal Revenue,
    decimal Profit,
    decimal MarginPercent,
    DateTime? LastSaleAt,
    decimal CurrentStock);

/// <summary>
/// One cell of the Operations heatmap: a single (day-of-week, hour) bucket.
/// <see cref="DayOfWeek"/> is 0=Sunday..6=Saturday (SQLite strftime('%w')).
/// <see cref="Hour"/> is 0..23.
/// </summary>
public sealed record HourDayCell(
    int DayOfWeek,
    int Hour,
    int TransactionCount,
    decimal Revenue);

/// <summary>
/// One cashier's aggregate performance over the selected period. Drives the
/// Operations page cashier table.
/// </summary>
public sealed record CashierPerformanceRow(
    string CashierName,
    int SalesCount,
    decimal Revenue,
    decimal AverageTicket,
    double UnitsSold,
    double ItemsPerTicket);

/// <summary>
/// One row of the ABC-XYZ classification. <see cref="AbcClass"/> is "A",
/// "B", or "C". <see cref="XyzClass"/> is "X", "Y", or "Z".
/// <see cref="DemandCv"/> is the coefficient of variation of weekly units
/// (rounded to 3 dp); zero when there are not enough weeks to compute it.
/// </summary>
public sealed record AbcXyzRow(
    long ProductId,
    string Name,
    string? Barcode,
    double UnitsSold,
    decimal Revenue,
    decimal RevenueSharePercent,
    decimal DemandCv,
    string AbcClass,
    string XyzClass);

/// <summary>
/// One row of the basket-affinity table. Pair is (A, B) with
/// <c>ProductIdA &lt; ProductIdB</c>.
/// <see cref="SupportAPercent"/> = % of multi-line baskets containing A.
/// <see cref="SupportBPercent"/> = % of multi-line baskets containing B.
/// <see cref="ConfidenceAtoBPercent"/> = % of A-baskets that also contain B.
/// <see cref="Lift"/> &gt; 1 = A and B occur together more often than chance.
/// </summary>
public sealed record BasketAffinityRow(
    long ProductIdA,
    string ProductNameA,
    long ProductIdB,
    string ProductNameB,
    long PairCount,
    decimal SupportAPercent,
    decimal SupportBPercent,
    decimal ConfidenceAtoBPercent,
    decimal Lift);
