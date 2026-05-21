using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Modules.Products;

public sealed class ProductImportService
{
    private const int BatchSize = 500;
    private const int ReportEvery = 50;  // increased from 5 to reduce UI thread and debugger pressure
    private const int MaxStoredErrors = 50;
    private static readonly string[] NameHeaderAliases = ["Name", "ProductName", "ItemName", "Description", "Title"];
    private static readonly string[] PriceHeaderAliases = ["Price", "SellPrice", "SellingPrice", "RetailPrice", "SalePrice", "UnitPrice"];
    private static readonly string[] BarcodeHeaderAliases = ["Barcode", "BarCode", "EAN", "UPC", "Code"];
    private static readonly string[] UnitHeaderAliases = ["Unit", "UOM", "MeasureUnit"];
    private static readonly string[] SkuHeaderAliases = ["Sku", "SKU", "ItemCode", "ProductCode"];
    private static readonly string[] CostPriceHeaderAliases = ["CostPrice", "Cost", "BuyPrice", "PurchasePrice", "UnitCost"];
    private static readonly string[] QuantityStoreHeaderAliases = ["QuantityStore", "StoreQty", "StoreQuantity", "StoreStock", "Quantity"];
    private static readonly string[] QuantityWarehouseHeaderAliases = ["QuantityWarehouse", "WarehouseQty", "WarehouseQuantity", "WarehouseStock", "Stock"];
    private static readonly string[] TaxGroupHeaderAliases = ["TaxGroupId", "TaxGroup", "TaxId"];
    private static readonly string[] ImagePathHeaderAliases = ["ImagePath", "Image", "ImageFile", "Photo", "Picture"];
    private static readonly string[] ProductDnaHeaderAliases = ["ProductDna", "DNA", "ProductKey", "ProductIdentity"];

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
        return ImportFromCsv(filePath, progress, cancellationToken, resolveImportedImagePath: null);
    }

    public ImportResult ImportFromCsv(
        string filePath,
        IProgress<ProductImportProgress>? progress,
        CancellationToken cancellationToken,
        Func<ProductCsvRow, string?>? resolveImportedImagePath)
    {
        return ImportFromCsv(filePath, progress, cancellationToken, resolveImportedImagePath, mapping: null);
    }

    /// <summary>
    /// Reads only the header row from <paramref name="filePath"/> and returns
    /// the raw column names exactly as they appear in the file (in order,
    /// duplicates removed, empty entries skipped).
    /// </summary>
    public static IReadOnlyList<string> PreviewHeaders(string filePath)
    {
        return PreviewSample(filePath, sampleRows: 0).Headers;
    }

    /// <summary>
    /// Reads the header row plus up to <paramref name="sampleRows"/> data
    /// rows from <paramref name="filePath"/>. The dialog uses the sample
    /// rows to show the user the actual values behind each CSV column so
    /// they can confirm the mapping visually.
    /// </summary>
    public static CsvPreview PreviewSample(string filePath, int sampleRows = 3)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found.", filePath);
        }
        if (sampleRows < 0) sampleRows = 0;

        var config = CreateCsvConfiguration();
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        if (!csv.Read() || !csv.ReadHeader())
        {
            return new CsvPreview(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string?>>());
        }

        var rawHeaders = csv.HeaderRecord ?? Array.Empty<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var headers = new List<string>(rawHeaders.Length);
        foreach (var raw in rawHeaders)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            if (seen.Add(trimmed))
            {
                headers.Add(trimmed);
            }
        }

        var rows = new List<IReadOnlyDictionary<string, string?>>(sampleRows);
        var read = 0;
        while (read < sampleRows && csv.Read())
        {
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                csv.TryGetField(header, out string? value);
                dict[header] = string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
            }
            rows.Add(dict);
            read++;
        }

        return new CsvPreview(headers, rows);
    }

    /// <summary>
    /// Auto-suggests a column mapping for <paramref name="headers"/> using
    /// the existing alias tables. Any field that doesn't match an alias is
    /// returned as null so the dialog leaves it on "(skip)".
    /// </summary>
    public static ProductImportMapping SuggestMapping(IReadOnlyList<string> headers)
    {
        return new ProductImportMapping
        {
            Name              = MatchHeader(headers, NameHeaderAliases),
            Price             = MatchHeader(headers, PriceHeaderAliases),
            Barcode           = MatchHeader(headers, BarcodeHeaderAliases),
            Unit              = MatchHeader(headers, UnitHeaderAliases),
            Sku               = MatchHeader(headers, SkuHeaderAliases),
            CostPrice         = MatchHeader(headers, CostPriceHeaderAliases),
            QuantityStore     = MatchHeader(headers, QuantityStoreHeaderAliases),
            QuantityWarehouse = MatchHeader(headers, QuantityWarehouseHeaderAliases),
            TaxGroupId        = MatchHeader(headers, TaxGroupHeaderAliases),
            ImagePath         = MatchHeader(headers, ImagePathHeaderAliases),
            ProductDna        = MatchHeader(headers, ProductDnaHeaderAliases),
        };
    }

    private static string? MatchHeader(IReadOnlyList<string> headers, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var normalizedAlias = NormalizeCsvHeader(alias);
            foreach (var header in headers)
            {
                if (NormalizeCsvHeader(header) == normalizedAlias)
                {
                    return header;
                }
            }
        }
        return null;
    }

    public ImportResult ImportFromCsv(
        string filePath,
        IProgress<ProductImportProgress>? progress,
        CancellationToken cancellationToken,
        Func<ProductCsvRow, string?>? resolveImportedImagePath,
        ProductImportMapping? mapping)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found.", filePath);
        }

        var config = CreateCsvConfiguration();
        var totalRows = CountRows(filePath, config, cancellationToken, mapping);
        var columnPresence = DetectColumnPresence(filePath, config, mapping);
        var importImageColumn = resolveImportedImagePath != null && columnPresence.HasImagePath;
        var hasDnaColumn = columnPresence.HasProductDna;
        progress?.Report(new ProductImportProgress { TotalRows = totalRows });

        var result = new ImportResult();
        var processedRows = 0;
        var rowsSinceLastCommit = 0;
        var pendingProducts = new List<Product>(BatchSize);
        var seenImportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var connection = _connectionFactory.OpenConnection();
        ConfigureImportConnection(connection);

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = CreateCsvReader(reader, config, mapping);

            foreach (var record in csv.GetRecords<ProductCsvRow>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedRows++;
                rowsSinceLastCommit++;

                if (TryCreateProduct(record, out var product, out var errorMessage, out var warningMessage))
                {
                    if (!string.IsNullOrWhiteSpace(warningMessage))
                    {
                        AddError(result, warningMessage!);
                    }

                    // Carry over the DNA from the CSV if present
                    if (hasDnaColumn && !string.IsNullOrWhiteSpace(record.ProductDna))
                    {
                        product!.ProductDna = record.ProductDna.Trim();
                    }

                    var importKey = BuildImportIdentityKey(record, product!);
                    if (!seenImportKeys.Add(importKey))
                    {
                        result.SkippedCount++;
                        AddError(result, $"Duplicate row skipped for '{product!.Name}' in the import file.");
                        continue;
                    }

                    if (importImageColumn)
                    {
                        ApplyImportedImage(record, product!, resolveImportedImagePath!, result);
                    }

                    pendingProducts.Add(product!);
                }
                else
                {
                    result.SkippedCount++;
                    AddError(result, errorMessage!);
                }

                // Report progress every ReportEvery rows so the UI bar moves smoothly
                if (processedRows % ReportEvery == 0)
                {
                    ReportProgress(progress, totalRows, processedRows, result);
                }

                if (rowsSinceLastCommit >= BatchSize)
                {
                    FlushBatch(connection, pendingProducts, result, columnPresence);
                    rowsSinceLastCommit = 0;
                }
            }

            FlushBatch(connection, pendingProducts, result, columnPresence);
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
            PrepareHeaderForMatch = args => NormalizeCsvHeader(args.Header),
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


    private static int CountRows(string filePath, CsvConfiguration config, CancellationToken cancellationToken, ProductImportMapping? mapping = null)
    {
        using var reader = new StreamReader(filePath);
        using var csv = CreateCsvReader(reader, config, mapping);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new InvalidDataException("The CSV file is empty or missing a valid header row.");
        }

        var headers = csv.HeaderRecord?.Select(NormalizeCsvHeader).ToList() ?? new List<string>();

        // When the caller supplies an explicit mapping, the header validation
        // is the user's responsibility — they have already chosen which CSV
        // column maps to which product field. Skip the alias-based check.
        if (mapping is null)
        {
            if (!HasAnyHeader(headers, NameHeaderAliases) && !HasAnyHeader(headers, BarcodeHeaderAliases))
            {
                throw new InvalidDataException("The file does not appear to be a valid Products CSV. Required columns like 'Name' or 'Barcode' are missing.");
            }
        }

        var totalRows = 0;
        try
        {
            foreach (var _ in csv.GetRecords<ProductCsvRow>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalRows++;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("The CSV file could not be parsed. Please ensure it matches the correct product template format.", ex);
        }

        return totalRows;
    }

    private static bool HasColumn(string filePath, CsvConfiguration config, string columnName, ProductImportMapping? mapping = null)
    {
        // When an explicit mapping is supplied, presence is decided directly
        // by what the user chose for that field. No alias lookup required.
        if (mapping is not null)
        {
            return columnName switch
            {
                nameof(ProductCsvRow.Name) => !string.IsNullOrWhiteSpace(mapping.Name),
                nameof(ProductCsvRow.Price) => !string.IsNullOrWhiteSpace(mapping.Price),
                nameof(ProductCsvRow.Barcode) => !string.IsNullOrWhiteSpace(mapping.Barcode),
                nameof(ProductCsvRow.Unit) => !string.IsNullOrWhiteSpace(mapping.Unit),
                nameof(ProductCsvRow.Sku) => !string.IsNullOrWhiteSpace(mapping.Sku),
                nameof(ProductCsvRow.CostPrice) => !string.IsNullOrWhiteSpace(mapping.CostPrice),
                nameof(ProductCsvRow.QuantityStore) => !string.IsNullOrWhiteSpace(mapping.QuantityStore),
                nameof(ProductCsvRow.QuantityWarehouse) => !string.IsNullOrWhiteSpace(mapping.QuantityWarehouse),
                nameof(ProductCsvRow.TaxGroupId) => !string.IsNullOrWhiteSpace(mapping.TaxGroupId),
                nameof(ProductCsvRow.ImagePath) => !string.IsNullOrWhiteSpace(mapping.ImagePath),
                nameof(ProductCsvRow.ProductDna) => !string.IsNullOrWhiteSpace(mapping.ProductDna),
                _ => false
            };
        }

        using var reader = new StreamReader(filePath);
        using var csv = CreateCsvReader(reader, config);

        if (!csv.Read())
        {
            return false;
        }

        csv.ReadHeader();
        var aliases = GetHeaderAliases(columnName);
        return csv.HeaderRecord is not null && HasAnyHeader(csv.HeaderRecord, aliases);
    }

    private static string NormalizeCsvHeader(string? header)
    {
        return string.IsNullOrWhiteSpace(header)
            ? string.Empty
            : new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static bool HasAnyHeader(IEnumerable<string> headers, IEnumerable<string> aliases)
    {
        var normalizedHeaders = headers
            .Select(NormalizeCsvHeader)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return aliases
            .Select(NormalizeCsvHeader)
            .Any(normalizedHeaders.Contains);
    }

    private static string[] GetHeaderAliases(string columnName)
    {
        return columnName switch
        {
            nameof(ProductCsvRow.Name) => NameHeaderAliases,
            nameof(ProductCsvRow.Price) => PriceHeaderAliases,
            nameof(ProductCsvRow.Barcode) => BarcodeHeaderAliases,
            nameof(ProductCsvRow.Unit) => UnitHeaderAliases,
            nameof(ProductCsvRow.Sku) => SkuHeaderAliases,
            nameof(ProductCsvRow.CostPrice) => CostPriceHeaderAliases,
            nameof(ProductCsvRow.QuantityStore) => QuantityStoreHeaderAliases,
            nameof(ProductCsvRow.QuantityWarehouse) => QuantityWarehouseHeaderAliases,
            nameof(ProductCsvRow.TaxGroupId) => TaxGroupHeaderAliases,
            nameof(ProductCsvRow.ImagePath) => ImagePathHeaderAliases,
            nameof(ProductCsvRow.ProductDna) => ProductDnaHeaderAliases,
            _ => [columnName]
        };
    }

    private static ImportColumnPresence DetectColumnPresence(string filePath, CsvConfiguration config, ProductImportMapping? mapping = null)
    {
        return new ImportColumnPresence(
            HasColumn(filePath, config, nameof(ProductCsvRow.Name), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.Price), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.Barcode), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.Unit), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.Sku), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.CostPrice), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.QuantityStore), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.QuantityWarehouse), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.TaxGroupId), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.ImagePath), mapping),
            HasColumn(filePath, config, nameof(ProductCsvRow.ProductDna), mapping));
    }

    private static void ApplyImportedImage(
        ProductCsvRow record,
        Product product,
        Func<ProductCsvRow, string?> resolveImportedImagePath,
        ImportResult result)
    {
        if (string.IsNullOrWhiteSpace(record.ImagePath))
        {
            // Do NOT set ThumbnailPath to null here — image preservation is handled in FlushBatch
            return;
        }

        try
        {
            product.ThumbnailPath = Clean(resolveImportedImagePath(record));
            if (string.IsNullOrWhiteSpace(product.ThumbnailPath))
            {
                AddError(result, $"Image skipped for '{product.Name}': '{record.ImagePath}'.");
            }
        }
        catch (Exception ex)
        {
            product.ThumbnailPath = null;
            AddError(result, $"Image skipped for '{product.Name}': {ex.Message}");
        }
    }

    private static CsvReader CreateCsvReader(TextReader reader, CsvConfiguration config)
    {
        return CreateCsvReader(reader, config, mapping: null);
    }

    private static CsvReader CreateCsvReader(TextReader reader, CsvConfiguration config, ProductImportMapping? mapping)
    {
        var csv = new CsvReader(reader, config);
        if (mapping is null)
        {
            csv.Context.RegisterClassMap<ProductCsvRowMap>();
        }
        else
        {
            csv.Context.RegisterClassMap(new ExplicitMappingProductCsvRowMap(mapping));
        }
        return csv;
    }

    private static bool TryCreateProduct(ProductCsvRow record, out Product? product, out string? errorMessage, out string? warningMessage)
    {
        var name = record.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            product = null;
            errorMessage = "Row skipped: missing product name.";
            warningMessage = null;
            return false;
        }

        var hasPrice = TryParseDecimal(record.Price, out var price);
        var hasCostPrice = TryParseDecimal(record.CostPrice, out var costPrice);

        warningMessage = null;
        if (!hasPrice)
        {
            if (hasCostPrice)
            {
                price = costPrice;
                warningMessage = $"Price missing for '{name}'. Cost price was used as the sale price.";
            }
            else
            {
                product = null;
                errorMessage = $"Row skipped: invalid price for '{name}'.";
                return false;
            }
        }

        if (price < 0)
        {
            product = null;
            errorMessage = $"Row skipped: price cannot be negative for '{name}'.";
            return false;
        }

        if (hasCostPrice && costPrice < 0)
        {
            costPrice = 0m;
            warningMessage = string.IsNullOrWhiteSpace(warningMessage)
                ? $"Negative cost price was ignored for '{name}'."
                : $"{warningMessage} Negative cost price was ignored.";
        }

        decimal.TryParse(record.QuantityStore, NumberStyles.Any, CultureInfo.InvariantCulture, out var qs);
        decimal.TryParse(record.QuantityWarehouse, NumberStyles.Any, CultureInfo.InvariantCulture, out var qw);
        long? taxGroupId = null;
        if (!string.IsNullOrWhiteSpace(record.TaxGroupId))
        {
            if (long.TryParse(record.TaxGroupId, out var tId))
            {
                taxGroupId = tId;
            }
            else
            {
                warningMessage = string.IsNullOrWhiteSpace(warningMessage)
                    ? $"Tax group was ignored for '{name}' because '{record.TaxGroupId}' is not a numeric tax group id."
                    : $"{warningMessage} Tax group value '{record.TaxGroupId}' was ignored.";
            }
        }

        product = new Product
        {
            Name = name,
            Price = price,
            CostPrice = costPrice,
            QuantityStore = qs,
            QuantityWarehouse = qw,
            TaxGroupId = taxGroupId,
            Barcode = Clean(record.Barcode),
            Unit = Clean(record.Unit),
            Sku = Clean(record.Sku)
        };
        errorMessage = null;
        return true;
    }

    private static void FlushBatch(
        SqliteConnection connection,
        List<Product> batch,
        ImportResult result,
        ImportColumnPresence columnPresence)
    {
        if (batch.Count == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();

        // TIER 1: Lookup by Product DNA
        using var lookupDnaCommand = connection.CreateCommand();
        lookupDnaCommand.Transaction = transaction;
        lookupDnaCommand.CommandText = @"
SELECT p.id,
       p.sku,
       p.name,
       p.barcode,
       p.unit,
       p.price_cents,
       p.cost_price_cents,
       p.tax_category_id,
       COALESCE(p.quantity_store, p.quantity, 0) as quantity_store,
       COALESCE(p.quantity_warehouse, 0) as quantity_warehouse,
       COALESCE(p.min_threshold_store, 5) as min_threshold_store,
       COALESCE(p.min_threshold_warehouse, 10) as min_threshold_warehouse,
       p.purchased_at,
       p.last_sale_at,
       p.cashier_name,
       p.thumbnail_path,
       COALESCE(tc.rate_percent, 0) as tax_rate,
       p.tax_group_id,
       p.product_dna
FROM products p
LEFT JOIN tax_categories tc ON p.tax_category_id = tc.id
WHERE p.product_dna = @dna
LIMIT 1;";
        var lookupDnaParameter = lookupDnaCommand.Parameters.Add("@dna", SqliteType.Text);
        lookupDnaCommand.Prepare();

        // TIER 2: Lookup by Barcode
        using var lookupCommand = connection.CreateCommand();
        lookupCommand.Transaction = transaction;
        lookupCommand.CommandText = @"
SELECT p.id,
       p.sku,
       p.name,
       p.barcode,
       p.unit,
       p.price_cents,
       p.cost_price_cents,
       p.tax_category_id,
       COALESCE(p.quantity_store, p.quantity, 0) as quantity_store,
       COALESCE(p.quantity_warehouse, 0) as quantity_warehouse,
       COALESCE(p.min_threshold_store, 5) as min_threshold_store,
       COALESCE(p.min_threshold_warehouse, 10) as min_threshold_warehouse,
       p.purchased_at,
       p.last_sale_at,
       p.cashier_name,
       p.thumbnail_path,
       COALESCE(tc.rate_percent, 0) as tax_rate,
       p.tax_group_id,
       p.product_dna
FROM products p
LEFT JOIN tax_categories tc ON p.tax_category_id = tc.id
WHERE p.barcode = @barcode
LIMIT 1;";
        var lookupBarcodeParameter = lookupCommand.Parameters.Add("@barcode", SqliteType.Text);
        lookupCommand.Prepare();

        // TIER 3: Lookup by Name
        using var lookupNameCommand = connection.CreateCommand();
        lookupNameCommand.Transaction = transaction;
        lookupNameCommand.CommandText = @"
SELECT p.id,
       p.sku,
       p.name,
       p.barcode,
       p.unit,
       p.price_cents,
       p.cost_price_cents,
       p.tax_category_id,
       COALESCE(p.quantity_store, p.quantity, 0) as quantity_store,
       COALESCE(p.quantity_warehouse, 0) as quantity_warehouse,
       COALESCE(p.min_threshold_store, 5) as min_threshold_store,
       COALESCE(p.min_threshold_warehouse, 10) as min_threshold_warehouse,
       p.purchased_at,
       p.last_sale_at,
       p.cashier_name,
       p.thumbnail_path,
       COALESCE(tc.rate_percent, 0) as tax_rate,
       p.tax_group_id,
       p.product_dna
FROM products p
LEFT JOIN tax_categories tc ON p.tax_category_id = tc.id
WHERE p.name = @name
LIMIT 1;";
        var lookupNameParameter = lookupNameCommand.Parameters.Add("@name", SqliteType.Text);
        lookupNameCommand.Prepare();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = @"
INSERT INTO products (sku, name, barcode, unit, price_cents, cost_price_cents, tax_category_id, tax_group_id, quantity, quantity_store, quantity_warehouse, thumbnail_path, product_dna, created_at, updated_at)
VALUES (@sku, @name, @barcode, @unit, @price_cents, @cost_price_cents, @tax_category_id, @tax_group_id, @quantity_store, @quantity_store, @quantity_warehouse, @thumbnail_path, @product_dna, @created_at, @updated_at);";
        var insertSkuParameter = insertCommand.Parameters.Add("@sku", SqliteType.Text);
        var insertNameParameter = insertCommand.Parameters.Add("@name", SqliteType.Text);
        var insertBarcodeParameter = insertCommand.Parameters.Add("@barcode", SqliteType.Text);
        var insertUnitParameter = insertCommand.Parameters.Add("@unit", SqliteType.Text);
        var insertPriceParameter = insertCommand.Parameters.Add("@price_cents", SqliteType.Integer);
        var insertCostPriceParameter = insertCommand.Parameters.Add("@cost_price_cents", SqliteType.Integer);
        var insertTaxCategoryParameter = insertCommand.Parameters.Add("@tax_category_id", SqliteType.Integer);
        var insertTaxGroupParameter = insertCommand.Parameters.Add("@tax_group_id", SqliteType.Integer);
        var insertQuantityStoreParameter = insertCommand.Parameters.Add("@quantity_store", SqliteType.Real);
        var insertQuantityWarehouseParameter = insertCommand.Parameters.Add("@quantity_warehouse", SqliteType.Real);
        var insertThumbnailPathParameter = insertCommand.Parameters.Add("@thumbnail_path", SqliteType.Text);
        var insertDnaParameter = insertCommand.Parameters.Add("@product_dna", SqliteType.Text);
        var insertCreatedAtParameter = insertCommand.Parameters.Add("@created_at", SqliteType.Text);
        var insertUpdatedAtParameter = insertCommand.Parameters.Add("@updated_at", SqliteType.Text);
        insertCommand.Prepare();

        // Update command: uses CASE to preserve image when CSV has no image
        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = @"
UPDATE products
SET sku = @sku,
    name = @name,
    barcode = @barcode,
    unit = @unit,
    price_cents = @price_cents,
    cost_price_cents = @cost_price_cents,
    tax_category_id = @tax_category_id,
    tax_group_id = @tax_group_id,
    quantity = @quantity_store,
    quantity_store = @quantity_store,
    quantity_warehouse = @quantity_warehouse,
    thumbnail_path = CASE WHEN @preserve_image = 1 THEN thumbnail_path ELSE @thumbnail_path END,
    product_dna = COALESCE(@product_dna, product_dna),
    updated_at = @updated_at
WHERE id = @id;";
        var updateIdParameter = updateCommand.Parameters.Add("@id", SqliteType.Integer);
        var updateSkuParameter = updateCommand.Parameters.Add("@sku", SqliteType.Text);
        var updateNameParameter = updateCommand.Parameters.Add("@name", SqliteType.Text);
        var updateBarcodeParameter = updateCommand.Parameters.Add("@barcode", SqliteType.Text);
        var updateUnitParameter = updateCommand.Parameters.Add("@unit", SqliteType.Text);
        var updatePriceParameter = updateCommand.Parameters.Add("@price_cents", SqliteType.Integer);
        var updateCostPriceParameter = updateCommand.Parameters.Add("@cost_price_cents", SqliteType.Integer);
        var updateTaxCategoryParameter = updateCommand.Parameters.Add("@tax_category_id", SqliteType.Integer);
        var updateTaxGroupParameter = updateCommand.Parameters.Add("@tax_group_id", SqliteType.Integer);
        var updateQuantityStoreParameter = updateCommand.Parameters.Add("@quantity_store", SqliteType.Real);
        var updateQuantityWarehouseParameter = updateCommand.Parameters.Add("@quantity_warehouse", SqliteType.Real);
        var updateThumbnailPathParameter = updateCommand.Parameters.Add("@thumbnail_path", SqliteType.Text);
        var updatePreserveImageParameter = updateCommand.Parameters.Add("@preserve_image", SqliteType.Integer);
        var updateDnaParameter = updateCommand.Parameters.Add("@product_dna", SqliteType.Text);
        var updateUpdatedAtParameter = updateCommand.Parameters.Add("@updated_at", SqliteType.Text);
        updateCommand.Prepare();

        var validTaxGroupIds = new HashSet<long>();
        long? defaultTaxGroupId = null;
        using (var taxGroupCommand = connection.CreateCommand())
        {
            taxGroupCommand.Transaction = transaction;
            taxGroupCommand.CommandText = @"
SELECT id, is_default
FROM tax_groups
ORDER BY is_default DESC, id ASC;";

            using var taxGroupReader = taxGroupCommand.ExecuteReader();
            while (taxGroupReader.Read())
            {
                var id = taxGroupReader.GetInt64(0);
                validTaxGroupIds.Add(id);
                if (!defaultTaxGroupId.HasValue && taxGroupReader.GetInt32(1) == 1)
                {
                    defaultTaxGroupId = id;
                }
            }
        }

        if (!defaultTaxGroupId.HasValue && validTaxGroupIds.Count > 0)
        {
            defaultTaxGroupId = validTaxGroupIds.Min();
        }

        foreach (var product in batch)
        {
            Product? existingProduct = null;

            // TIER 1: Search by Product DNA (highest priority)
            if (!string.IsNullOrWhiteSpace(product.ProductDna))
            {
                lookupDnaParameter.Value = product.ProductDna;
                using var dnaReader = lookupDnaCommand.ExecuteReader();
                if (dnaReader.Read())
                {
                    existingProduct = ReadExistingProduct(dnaReader);
                }
            }

            // TIER 2: Search by Barcode (fallback — runs even if DNA was present but not matched)
            if (existingProduct is null && !string.IsNullOrWhiteSpace(product.Barcode))
            {
                lookupBarcodeParameter.Value = product.Barcode;
                using var barcodeReader = lookupCommand.ExecuteReader();
                if (barcodeReader.Read())
                {
                    existingProduct = ReadExistingProduct(barcodeReader);
                }
            }

            // TIER 3: Search by exact Name (last resort)
            if (existingProduct is null && !string.IsNullOrWhiteSpace(product.Name))
            {
                lookupNameParameter.Value = product.Name;
                using var nameReader = lookupNameCommand.ExecuteReader();
                if (nameReader.Read())
                {
                    existingProduct = ReadExistingProduct(nameReader);
                }
            }

            var effectiveProduct = existingProduct is null
                ? product
                : MergeImportedIntoExisting(existingProduct, product, columnPresence);
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var skuValue = (object?)effectiveProduct.Sku ?? DBNull.Value;
            var barcodeValue = (object?)effectiveProduct.Barcode ?? DBNull.Value;
            var unitValue = (object?)effectiveProduct.Unit ?? DBNull.Value;
            var priceInCents = MoneyUtils.ToCents(effectiveProduct.Price);
            var costPriceInCents = MoneyUtils.ToCents(effectiveProduct.CostPrice);
            var effectiveTaxGroupId = ResolveEffectiveTaxGroupId(effectiveProduct, validTaxGroupIds, defaultTaxGroupId, result);

            // Image preservation: keep old image if CSV has no image and existing product has one
            var csvHasImage = !string.IsNullOrWhiteSpace(effectiveProduct.ThumbnailPath);
            var shouldPreserveImage = !csvHasImage && !string.IsNullOrWhiteSpace(existingProduct?.ThumbnailPath);

            try
            {
                if (existingProduct is not null)
                {
                    updateIdParameter.Value = existingProduct.Id;
                    updateSkuParameter.Value = skuValue;
                    updateNameParameter.Value = effectiveProduct.Name;
                    updateBarcodeParameter.Value = barcodeValue;
                    updateUnitParameter.Value = unitValue;
                    updatePriceParameter.Value = priceInCents;
                    updateCostPriceParameter.Value = costPriceInCents;
                    updateTaxCategoryParameter.Value = effectiveProduct.TaxCategoryId;
                    updateTaxGroupParameter.Value = (object?)effectiveTaxGroupId ?? DBNull.Value;
                    updateQuantityStoreParameter.Value = effectiveProduct.QuantityStore;
                    updateQuantityWarehouseParameter.Value = effectiveProduct.QuantityWarehouse;
                    updateThumbnailPathParameter.Value = (object?)effectiveProduct.ThumbnailPath ?? DBNull.Value;
                    updatePreserveImageParameter.Value = shouldPreserveImage ? 1 : 0;
                    updateDnaParameter.Value = (object?)effectiveProduct.ProductDna ?? DBNull.Value;
                    updateUpdatedAtParameter.Value = now;
                    updateCommand.ExecuteNonQuery();
                    result.UpdatedCount++;
                    continue;
                }

                // Generate DNA for new products if not provided by CSV
                var dna = !string.IsNullOrWhiteSpace(effectiveProduct.ProductDna)
                    ? effectiveProduct.ProductDna
                    : ProductDnaGenerator.Generate(effectiveProduct.Name);

                insertSkuParameter.Value = skuValue;
                insertNameParameter.Value = effectiveProduct.Name;
                insertBarcodeParameter.Value = barcodeValue;
                insertUnitParameter.Value = unitValue;
                insertPriceParameter.Value = priceInCents;
                insertCostPriceParameter.Value = costPriceInCents;
                insertTaxCategoryParameter.Value = effectiveProduct.TaxCategoryId;
                insertTaxGroupParameter.Value = (object?)effectiveTaxGroupId ?? DBNull.Value;
                insertQuantityStoreParameter.Value = effectiveProduct.QuantityStore;
                insertQuantityWarehouseParameter.Value = effectiveProduct.QuantityWarehouse;
                insertThumbnailPathParameter.Value = (object?)effectiveProduct.ThumbnailPath ?? DBNull.Value;
                insertDnaParameter.Value = dna;
                insertCreatedAtParameter.Value = now;
                insertUpdatedAtParameter.Value = now;
                insertCommand.ExecuteNonQuery();
                result.CreatedCount++;
            }
            catch (SqliteException ex)
            {
                result.SkippedCount++;
                AddError(result, $"Database error for '{effectiveProduct.Name}': {ex.Message}");
            }
        }

        transaction.Commit();
        batch.Clear();
    }

    private static long? ResolveEffectiveTaxGroupId(
        Product product,
        HashSet<long> validTaxGroupIds,
        long? defaultTaxGroupId,
        ImportResult result)
    {
        if (!product.TaxGroupId.HasValue)
        {
            return null;
        }

        if (validTaxGroupIds.Contains(product.TaxGroupId.Value))
        {
            return product.TaxGroupId.Value;
        }

        if (defaultTaxGroupId.HasValue)
        {
            AddError(result, $"Tax group {product.TaxGroupId.Value} was not found for '{product.Name}'. The default tax group was used instead.");
            return defaultTaxGroupId.Value;
        }

        AddError(result, $"Tax group {product.TaxGroupId.Value} was not found for '{product.Name}'. The product was imported without a tax group.");
        return null;
    }

    private static Product ReadExistingProduct(SqliteDataReader reader)
    {
        return new Product
        {
            Id = reader.GetInt64(0),
            Sku = reader.IsDBNull(1) ? null : reader.GetString(1),
            Name = reader.GetString(2),
            Barcode = reader.IsDBNull(3) ? null : reader.GetString(3),
            Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
            Price = MoneyUtils.FromCents(reader.GetInt64(5)),
            CostPrice = MoneyUtils.FromCents(reader.GetInt64(6)),
            TaxCategoryId = reader.IsDBNull(7) ? 1 : reader.GetInt64(7),
            QuantityStore = reader.IsDBNull(8) ? 0m : (decimal)reader.GetDouble(8),
            QuantityWarehouse = reader.IsDBNull(9) ? 0m : (decimal)reader.GetDouble(9),
            MinThresholdStore = reader.IsDBNull(10) ? 5m : (decimal)reader.GetDouble(10),
            MinThresholdWarehouse = reader.IsDBNull(11) ? 10m : (decimal)reader.GetDouble(11),
            PurchasedAt = reader.IsDBNull(12) ? null : DateTime.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LastSaleAt = reader.IsDBNull(13) ? null : DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            CashierName = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
            ThumbnailPath = reader.IsDBNull(15) ? null : reader.GetString(15),
            TaxRatePercent = reader.IsDBNull(16) ? 0m : (decimal)reader.GetDouble(16),
            TaxGroupId = reader.IsDBNull(17) ? 1 : reader.GetInt64(17),
            ProductDna = reader.IsDBNull(18) ? null : reader.GetString(18)
        };
    }

    private static Product MergeImportedIntoExisting(
        Product existingProduct,
        Product importedProduct,
        ImportColumnPresence columnPresence)
    {
        return new Product
        {
            Id = existingProduct.Id,
            ProductDna = columnPresence.HasProductDna && !string.IsNullOrWhiteSpace(importedProduct.ProductDna)
                ? importedProduct.ProductDna
                : existingProduct.ProductDna,
            Name = importedProduct.Name,
            Barcode = columnPresence.HasBarcode ? importedProduct.Barcode : existingProduct.Barcode,
            Sku = columnPresence.HasSku ? importedProduct.Sku : existingProduct.Sku,
            Unit = columnPresence.HasUnit ? importedProduct.Unit : existingProduct.Unit,
            Price = importedProduct.Price,
            CostPrice = columnPresence.HasCostPrice ? importedProduct.CostPrice : existingProduct.CostPrice,
            TaxCategoryId = existingProduct.TaxCategoryId,
            TaxGroupId = columnPresence.HasTaxGroupId ? importedProduct.TaxGroupId : existingProduct.TaxGroupId,
            TaxRatePercent = existingProduct.TaxRatePercent,
            QuantityStore = columnPresence.HasQuantityStore ? importedProduct.QuantityStore : existingProduct.QuantityStore,
            QuantityWarehouse = columnPresence.HasQuantityWarehouse ? importedProduct.QuantityWarehouse : existingProduct.QuantityWarehouse,
            MinThresholdStore = existingProduct.MinThresholdStore,
            MinThresholdWarehouse = existingProduct.MinThresholdWarehouse,
            PurchasedAt = existingProduct.PurchasedAt,
            LastSaleAt = existingProduct.LastSaleAt,
            ThumbnailPath = ResolveMergedThumbnailPath(existingProduct, importedProduct, columnPresence),
            CashierName = existingProduct.CashierName
        };
    }

    private static string? ResolveMergedThumbnailPath(
        Product existingProduct,
        Product importedProduct,
        ImportColumnPresence columnPresence)
    {
        if (!columnPresence.HasImagePath)
        {
            return existingProduct.ThumbnailPath;
        }

        return string.IsNullOrWhiteSpace(importedProduct.ThumbnailPath)
            ? existingProduct.ThumbnailPath
            : importedProduct.ThumbnailPath;
    }

    private static string BuildImportIdentityKey(ProductCsvRow record, Product product)
    {
        var dna = Clean(record.ProductDna);
        if (!string.IsNullOrWhiteSpace(dna))
        {
            return $"dna:{dna}";
        }

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            return $"barcode:{product.Barcode}";
        }

        return $"name:{product.Name}";
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

    private sealed class ProductCsvRowMap : ClassMap<ProductCsvRow>
    {
        public ProductCsvRowMap()
        {
            Map(m => m.Name).Name(NameHeaderAliases).Optional();
            Map(m => m.Price).Name(PriceHeaderAliases).Optional();
            Map(m => m.Barcode).Name(BarcodeHeaderAliases).Optional();
            Map(m => m.Unit).Name(UnitHeaderAliases).Optional();
            Map(m => m.Sku).Name(SkuHeaderAliases).Optional();
            Map(m => m.CostPrice).Name(CostPriceHeaderAliases).Optional();
            Map(m => m.QuantityStore).Name(QuantityStoreHeaderAliases).Optional();
            Map(m => m.QuantityWarehouse).Name(QuantityWarehouseHeaderAliases).Optional();
            Map(m => m.TaxGroupId).Name(TaxGroupHeaderAliases).Optional();
            Map(m => m.ImagePath).Name(ImagePathHeaderAliases).Optional();
            Map(m => m.ProductDna).Name(ProductDnaHeaderAliases).Optional();
        }
    }

    /// <summary>
    /// Class map driven by an explicit <see cref="ProductImportMapping"/> from
    /// the column-mapping dialog. Each product field reads from the single
    /// CSV column the user picked; null/empty entries are skipped.
    /// </summary>
    private sealed class ExplicitMappingProductCsvRowMap : ClassMap<ProductCsvRow>
    {
        public ExplicitMappingProductCsvRowMap(ProductImportMapping mapping)
        {
            ConfigureField(Map(m => m.Name), mapping.Name);
            ConfigureField(Map(m => m.Price), mapping.Price);
            ConfigureField(Map(m => m.Barcode), mapping.Barcode);
            ConfigureField(Map(m => m.Unit), mapping.Unit);
            ConfigureField(Map(m => m.Sku), mapping.Sku);
            ConfigureField(Map(m => m.CostPrice), mapping.CostPrice);
            ConfigureField(Map(m => m.QuantityStore), mapping.QuantityStore);
            ConfigureField(Map(m => m.QuantityWarehouse), mapping.QuantityWarehouse);
            ConfigureField(Map(m => m.TaxGroupId), mapping.TaxGroupId);
            ConfigureField(Map(m => m.ImagePath), mapping.ImagePath);
            ConfigureField(Map(m => m.ProductDna), mapping.ProductDna);
        }

        private static void ConfigureField(MemberMap<ProductCsvRow, string?> field, string? csvHeader)
        {
            if (!string.IsNullOrWhiteSpace(csvHeader))
            {
                field.Name(csvHeader).Optional();
            }
            else
            {
                // No CSV column chosen — make sure CsvHelper does not try to
                // bind this property to anything in the file.
                field.Ignore();
            }
        }
    }

    private sealed record ImportColumnPresence(
        bool HasName,
        bool HasPrice,
        bool HasBarcode,
        bool HasUnit,
        bool HasSku,
        bool HasCostPrice,
        bool HasQuantityStore,
        bool HasQuantityWarehouse,
        bool HasTaxGroupId,
        bool HasImagePath,
        bool HasProductDna);
}
