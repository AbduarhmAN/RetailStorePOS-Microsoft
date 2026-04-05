using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Services;

public sealed class ProductImportService
{
    private const int BatchSize = 500;
    private const int MaxStoredErrors = 50;

    private readonly SqliteConnectionFactory _connectionFactory;

    public ProductImportService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public ImportResult ImportFromCsv(string filePath)
    {
        return ImportFromCsv(filePath, progress: null, CancellationToken.None);
    }

    public ImportResult ImportFromCsv(
        string filePath,
        IProgress<ProductImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found.", filePath);
        }

        var config = CreateCsvConfiguration();
        var totalRows = CountRows(filePath, config, cancellationToken);
        progress?.Report(new ProductImportProgress { TotalRows = totalRows });

        var result = new ImportResult();
        var processedRows = 0;
        var rowsSinceLastCommit = 0;
        var pendingProducts = new List<Product>(BatchSize);

        using var connection = _connectionFactory.OpenConnection();
        ConfigureImportConnection(connection);

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);

            foreach (var record in csv.GetRecords<ProductCsvRow>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedRows++;
                rowsSinceLastCommit++;

                if (TryCreateProduct(record, out var product, out var errorMessage))
                {
                    pendingProducts.Add(product!);
                }
                else
                {
                    result.SkippedCount++;
                    AddError(result, errorMessage!);
                }

                if (rowsSinceLastCommit >= BatchSize)
                {
                    FlushBatch(connection, pendingProducts, result);
                    rowsSinceLastCommit = 0;
                    ReportProgress(progress, totalRows, processedRows, result);
                }
            }

            FlushBatch(connection, pendingProducts, result);
            ReportProgress(progress, totalRows, processedRows, result);
            return result;
        }
        catch (HeaderValidationException ex)
        {
            throw new InvalidDataException($"CSV header validation failed: {ex.Message}", ex);
        }
        catch (ReaderException ex)
        {
            throw new InvalidDataException($"CSV parse failed: {ex.Message}", ex);
        }
    }

    private static CsvConfiguration CreateCsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null
        };
    }

    private static void ConfigureImportConnection(SqliteConnection connection)
    {
        using var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode=WAL;";
        walCommand.ExecuteNonQuery();

        using var synchronousCommand = connection.CreateCommand();
        synchronousCommand.CommandText = "PRAGMA synchronous=NORMAL;";
        synchronousCommand.ExecuteNonQuery();
    }

    private static int CountRows(string filePath, CsvConfiguration config, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        var totalRows = 0;
        foreach (var _ in csv.GetRecords<ProductCsvRow>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalRows++;
        }

        return totalRows;
    }

    private static bool TryCreateProduct(ProductCsvRow record, out Product? product, out string? errorMessage)
    {
        var name = record.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            product = null;
            errorMessage = "Row skipped: missing product name.";
            return false;
        }

        if (!TryParseDecimal(record.Price, out var price) || price < 0)
        {
            product = null;
            errorMessage = $"Row skipped: invalid price for '{name}'.";
            return false;
        }

        product = new Product
        {
            Name = name,
            Price = price,
            Barcode = Clean(record.Barcode),
            Unit = Clean(record.Unit),
            Sku = Clean(record.Sku)
        };
        errorMessage = null;
        return true;
    }

    private static void FlushBatch(SqliteConnection connection, List<Product> batch, ImportResult result)
    {
        if (batch.Count == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();

        using var lookupCommand = connection.CreateCommand();
        lookupCommand.Transaction = transaction;
        lookupCommand.CommandText = "SELECT id FROM products WHERE barcode = @barcode LIMIT 1;";
        var lookupBarcodeParameter = lookupCommand.Parameters.Add("@barcode", SqliteType.Text);
        lookupCommand.Prepare();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = @"
INSERT INTO products (sku, name, barcode, unit, price_cents, tax_category_id, created_at, updated_at)
VALUES (@sku, @name, @barcode, @unit, @price_cents, @tax_category_id, @created_at, @updated_at);";
        var insertSkuParameter = insertCommand.Parameters.Add("@sku", SqliteType.Text);
        var insertNameParameter = insertCommand.Parameters.Add("@name", SqliteType.Text);
        var insertBarcodeParameter = insertCommand.Parameters.Add("@barcode", SqliteType.Text);
        var insertUnitParameter = insertCommand.Parameters.Add("@unit", SqliteType.Text);
        var insertPriceParameter = insertCommand.Parameters.Add("@price_cents", SqliteType.Integer);
        var insertTaxCategoryParameter = insertCommand.Parameters.Add("@tax_category_id", SqliteType.Integer);
        var insertCreatedAtParameter = insertCommand.Parameters.Add("@created_at", SqliteType.Text);
        var insertUpdatedAtParameter = insertCommand.Parameters.Add("@updated_at", SqliteType.Text);
        insertCommand.Prepare();

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = @"
UPDATE products
SET sku = @sku,
    name = @name,
    barcode = @barcode,
    unit = @unit,
    price_cents = @price_cents,
    tax_category_id = @tax_category_id,
    updated_at = @updated_at
WHERE id = @id;";
        var updateIdParameter = updateCommand.Parameters.Add("@id", SqliteType.Integer);
        var updateSkuParameter = updateCommand.Parameters.Add("@sku", SqliteType.Text);
        var updateNameParameter = updateCommand.Parameters.Add("@name", SqliteType.Text);
        var updateBarcodeParameter = updateCommand.Parameters.Add("@barcode", SqliteType.Text);
        var updateUnitParameter = updateCommand.Parameters.Add("@unit", SqliteType.Text);
        var updatePriceParameter = updateCommand.Parameters.Add("@price_cents", SqliteType.Integer);
        var updateTaxCategoryParameter = updateCommand.Parameters.Add("@tax_category_id", SqliteType.Integer);
        var updateUpdatedAtParameter = updateCommand.Parameters.Add("@updated_at", SqliteType.Text);
        updateCommand.Prepare();

        foreach (var product in batch)
        {
            long? existingId = null;
            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                lookupBarcodeParameter.Value = product.Barcode;
                var lookupResult = lookupCommand.ExecuteScalar();
                if (lookupResult != null && lookupResult != DBNull.Value)
                {
                    existingId = Convert.ToInt64(lookupResult, CultureInfo.InvariantCulture);
                }
            }

            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var skuValue = (object?)product.Sku ?? DBNull.Value;
            var barcodeValue = (object?)product.Barcode ?? DBNull.Value;
            var unitValue = (object?)product.Unit ?? DBNull.Value;
            var priceInCents = MoneyUtils.ToCents(product.Price);

            if (existingId.HasValue)
            {
                updateIdParameter.Value = existingId.Value;
                updateSkuParameter.Value = skuValue;
                updateNameParameter.Value = product.Name;
                updateBarcodeParameter.Value = barcodeValue;
                updateUnitParameter.Value = unitValue;
                updatePriceParameter.Value = priceInCents;
                updateTaxCategoryParameter.Value = product.TaxCategoryId;
                updateUpdatedAtParameter.Value = now;
                updateCommand.ExecuteNonQuery();
                result.UpdatedCount++;
                continue;
            }

            insertSkuParameter.Value = skuValue;
            insertNameParameter.Value = product.Name;
            insertBarcodeParameter.Value = barcodeValue;
            insertUnitParameter.Value = unitValue;
            insertPriceParameter.Value = priceInCents;
            insertTaxCategoryParameter.Value = product.TaxCategoryId;
            insertCreatedAtParameter.Value = now;
            insertUpdatedAtParameter.Value = now;
            insertCommand.ExecuteNonQuery();
            result.CreatedCount++;
        }

        transaction.Commit();
        batch.Clear();
    }

    private static void ReportProgress(
        IProgress<ProductImportProgress>? progress,
        int totalRows,
        int processedRows,
        ImportResult result)
    {
        progress?.Report(new ProductImportProgress
        {
            TotalRows = totalRows,
            ProcessedRows = processedRows,
            CreatedCount = result.CreatedCount,
            UpdatedCount = result.UpdatedCount,
            SkippedCount = result.SkippedCount
        });
    }

    private static void AddError(ImportResult result, string message)
    {
        if (result.Errors.Count < MaxStoredErrors)
        {
            result.Errors.Add(message);
        }
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }

    private sealed class ProductCsvRow
    {
        public string? Name { get; set; }
        public string? Price { get; set; }
        public string? Barcode { get; set; }
        public string? Unit { get; set; }
        public string? Sku { get; set; }
    }
}
