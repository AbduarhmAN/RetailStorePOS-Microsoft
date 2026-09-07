using System.Globalization;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data;

namespace RetailStorePOS.Data.Modules.Reporting;

public sealed class XReportRepository
{
    private const string LocalDateFormat = "yyyy-MM-dd";
    private readonly SqliteConnectionFactory _factory;

    public XReportRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public XReportRun CreateRun(DateTime periodStartLocal, DateTime periodEndLocal, string presetCode, long? generatedByUserId)
    {
        var (startLocal, endLocal) = NormalizeLocalWindow(periodStartLocal, periodEndLocal);
        var startUtc = startLocal.ToUniversalTime().ToString("O");
        var endUtc = endLocal.AddDays(1).ToUniversalTime().ToString("O");
        var generatedAtUtc = DateTime.UtcNow.ToString("O");

        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var insertRun = connection.CreateCommand())
        {
            insertRun.Transaction = transaction;
            insertRun.CommandText = @"
INSERT INTO x_report_runs (
    preset_code,
    period_start_local,
    period_end_local,
    generated_at_utc,
    generated_by_user_id
)
VALUES (
    @preset_code,
    @period_start_local,
    @period_end_local,
    @generated_at_utc,
    @generated_by_user_id
);";
            insertRun.Parameters.AddWithValue("@preset_code", string.IsNullOrWhiteSpace(presetCode) ? "Custom" : presetCode.Trim());
            insertRun.Parameters.AddWithValue("@period_start_local", startLocal.ToString(LocalDateFormat, CultureInfo.InvariantCulture));
            insertRun.Parameters.AddWithValue("@period_end_local", endLocal.ToString(LocalDateFormat, CultureInfo.InvariantCulture));
            insertRun.Parameters.AddWithValue("@generated_at_utc", generatedAtUtc);
            insertRun.Parameters.AddWithValue("@generated_by_user_id", (object?)generatedByUserId ?? DBNull.Value);
            insertRun.ExecuteNonQuery();
        }

        long runId;
        using (var idCommand = connection.CreateCommand())
        {
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            runId = Convert.ToInt64(idCommand.ExecuteScalar());
        }

        using (var insertSales = connection.CreateCommand())
        {
            insertSales.Transaction = transaction;
            insertSales.CommandText = @"
INSERT INTO x_report_run_sales (run_id, sale_id)
SELECT @run_id, id
FROM sales
WHERE created_at >= @period_start_utc
  AND created_at < @period_end_utc
ORDER BY created_at, id;";
            insertSales.Parameters.AddWithValue("@run_id", runId);
            insertSales.Parameters.AddWithValue("@period_start_utc", startUtc);
            insertSales.Parameters.AddWithValue("@period_end_utc", endUtc);
            insertSales.ExecuteNonQuery();
        }

        transaction.Commit();
        return GetRun(runId) ?? throw new InvalidOperationException("The X report run could not be loaded after creation.");
    }

    public IReadOnlyList<XReportRun> GetRuns(DateTime periodStartLocal, DateTime periodEndLocal)
    {
        var (startLocal, endLocal) = NormalizeLocalWindow(periodStartLocal, periodEndLocal);
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BuildRunSummaryQuery(@"
WHERE r.period_end_local >= @period_start_local
  AND r.period_start_local <= @period_end_local
");
        command.Parameters.AddWithValue("@period_start_local", startLocal.ToString(LocalDateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@period_end_local", endLocal.ToString(LocalDateFormat, CultureInfo.InvariantCulture));

        using var reader = command.ExecuteReader();
        var runs = new List<XReportRun>();
        while (reader.Read())
        {
            runs.Add(ReadRun(reader));
        }

        return runs;
    }

    public XReportRun? GetRun(long runId)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BuildRunSummaryQuery("WHERE r.id = @run_id");
        command.Parameters.AddWithValue("@run_id", runId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var run = ReadRun(reader);
        reader.Close();

        using var saleCommand = connection.CreateCommand();
        saleCommand.CommandText = @"
SELECT sale_id
FROM x_report_run_sales
WHERE run_id = @run_id
ORDER BY sale_id;";
        saleCommand.Parameters.AddWithValue("@run_id", runId);

        using var saleReader = saleCommand.ExecuteReader();
        while (saleReader.Read())
        {
            run.SaleIds.Add(saleReader.GetInt64(0));
        }

        return run;
    }

    private static string BuildRunSummaryQuery(string whereClause)
    {
        return $@"
SELECT
    r.id,
    r.preset_code,
    r.period_start_local,
    r.period_end_local,
    r.generated_at_utc,
    r.generated_by_user_id,
    COUNT(s.id) AS transaction_count,
    COALESCE(SUM(s.total_cents), 0) AS gross_sales_cents,
    COALESCE(SUM(s.subtotal_cents), 0) AS net_sales_cents,
    COALESCE(SUM(s.tax_cents), 0) AS tax_cents,
    COALESCE(SUM(CASE WHEN UPPER(s.payment_type) = 'CASH' THEN s.total_cents ELSE 0 END), 0) AS cash_sales_cents,
    COALESCE(SUM(CASE WHEN UPPER(s.payment_type) IN ('CARD', 'CREDIT', 'DEBIT') THEN s.total_cents ELSE 0 END), 0) AS card_sales_cents
FROM x_report_runs r
LEFT JOIN x_report_run_sales xrs ON xrs.run_id = r.id
LEFT JOIN sales s ON s.id = xrs.sale_id
{whereClause}
GROUP BY
    r.id,
    r.preset_code,
    r.period_start_local,
    r.period_end_local,
    r.generated_at_utc,
    r.generated_by_user_id
ORDER BY r.generated_at_utc DESC, r.id DESC;";
    }

    private static XReportRun ReadRun(SqliteDataReader reader)
    {
        return new XReportRun
        {
            Id = Convert.ToInt64(reader.GetValue(0)),
            PresetCode = reader.GetString(1),
            PeriodStartLocal = ParseLocalDate(reader.GetString(2)),
            PeriodEndLocal = ParseLocalDate(reader.GetString(3)),
            GeneratedAtUtc = reader.GetString(4),
            GeneratedByUserId = reader.IsDBNull(5) ? null : Convert.ToInt64(reader.GetValue(5)),
            TransactionCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(6))),
            GrossSalesCents = Convert.ToInt64(reader.GetValue(7)),
            NetSalesCents = Convert.ToInt64(reader.GetValue(8)),
            TaxCents = Convert.ToInt64(reader.GetValue(9)),
            CashSalesCents = Convert.ToInt64(reader.GetValue(10)),
            CardSalesCents = Convert.ToInt64(reader.GetValue(11))
        };
    }

    private static DateTime ParseLocalDate(string value)
    {
        return DateTime.ParseExact(value, LocalDateFormat, CultureInfo.InvariantCulture);
    }

    private static (DateTime StartLocal, DateTime EndLocal) NormalizeLocalWindow(DateTime startLocal, DateTime endLocal)
    {
        var start = startLocal.Date;
        var end = endLocal.Date;
        return end < start ? (end, start) : (start, end);
    }
}
