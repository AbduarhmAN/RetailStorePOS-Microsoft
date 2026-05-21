using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Inventory;

namespace RetailStorePOS.Data.Modules.Products;

public sealed class ProductRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ProductRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public List<Product> GetAll(int limit = 500)
    {
        return Search(string.Empty, limit);
    }

    public List<Product> GetAllUnbounded()
    {
        return Search(string.Empty, null);
    }

    public List<Product> Search(string query, int? limit = 200)
    {
        query ??= string.Empty;

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
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
WHERE @query = '' OR p.name LIKE @like OR p.barcode = @query
ORDER BY p.name" + (limit.HasValue ? "\nLIMIT @limit;" : ";");

        command.Parameters.AddWithValue("@query", query);
        command.Parameters.AddWithValue("@like", $"%{query}%");
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("@limit", limit.Value);
        }

        using var reader = command.ExecuteReader();
        var results = new List<Product>();
        while (reader.Read())
        {
            results.Add(MapProduct(reader));
        }

        return results;
    }

    public Product? GetByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
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
        command.Parameters.AddWithValue("@barcode", barcode);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapProduct(reader);
    }

    public List<Product> GetByIds(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return new List<Product>();
        }

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();

        var placeholders = string.Join(",", idList.Select((_, i) => $"@id{i}"));
        command.CommandText = $@"
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
WHERE p.id IN ({placeholders});";

        for (int i = 0; i < idList.Count; i++)
        {
            command.Parameters.AddWithValue($"@id{i}", idList[i]);
        }

        using var reader = command.ExecuteReader();
        var results = new List<Product>();
        while (reader.Read())
        {
            results.Add(MapProduct(reader));
        }

        return results;
    }

    public Product? GetById(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
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
WHERE p.id = @id
LIMIT 1;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapProduct(reader);
    }

    public long Create(Product product)
    {
        NormalizeOwnedWrite(product);
        var now = DateTime.UtcNow.ToString("O");

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO products (sku, name, barcode, unit, price_cents, cost_price_cents, tax_category_id, tax_group_id, quantity, quantity_store, quantity_warehouse, min_threshold_store, min_threshold_warehouse, purchased_at, last_sale_at, cashier_name, thumbnail_path, product_dna, created_at, updated_at)
VALUES (@sku, @name, @barcode, @unit, @price_cents, @cost_price_cents, @tax_category_id, @tax_group_id, @quantity, @quantity_store, @quantity_warehouse, @min_threshold_store, @min_threshold_warehouse, @purchased_at, @last_sale_at, @cashier_name, @thumbnail_path, @product_dna, @created_at, @updated_at);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@sku", (object?)product.Sku ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", product.Name);
        command.Parameters.AddWithValue("@barcode", (object?)product.Barcode ?? DBNull.Value);
        command.Parameters.AddWithValue("@unit", (object?)product.Unit ?? DBNull.Value);
        command.Parameters.AddWithValue("@price_cents", MoneyUtils.ToCents(product.Price));
        command.Parameters.AddWithValue("@cost_price_cents", MoneyUtils.ToCents(product.CostPrice));
        command.Parameters.AddWithValue("@tax_category_id", product.TaxCategoryId);
        command.Parameters.AddWithValue("@tax_group_id", product.TaxGroupId ?? 1);
        InventoryStockRules.BindOwnedProductWriteParameters(command, product);
        command.Parameters.AddWithValue("@quantity", product.Quantity);
        command.Parameters.AddWithValue(
            "@purchased_at",
            (object?)(product.PurchasedAt?.ToUniversalTime().ToString("O")) ?? now);
        command.Parameters.AddWithValue(
            "@last_sale_at",
            (object?)(product.LastSaleAt?.ToUniversalTime().ToString("O")) ?? DBNull.Value);
        command.Parameters.AddWithValue("@cashier_name", product.CashierName ?? string.Empty);
        command.Parameters.AddWithValue("@thumbnail_path", (object?)product.ThumbnailPath ?? DBNull.Value);
        // Auto-generate DNA if not already set
        if (string.IsNullOrWhiteSpace(product.ProductDna))
        {
            product.ProductDna = ProductDnaGenerator.Generate(product.Name);
        }
        command.Parameters.AddWithValue("@product_dna", product.ProductDna);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);

        var result = command.ExecuteScalar();
        product.Id = Convert.ToInt64(result);
        return product.Id;
    }

    public void Update(Product product)
    {
        NormalizeOwnedWrite(product);
        var now = DateTime.UtcNow.ToString("O");

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE products
SET sku = @sku,
    name = @name,
    barcode = @barcode,
    unit = @unit,
    price_cents = @price_cents,
    cost_price_cents = @cost_price_cents,
    tax_category_id = @tax_category_id,
    tax_group_id = @tax_group_id,
    quantity = @quantity,
    quantity_store = @quantity_store,
    quantity_warehouse = @quantity_warehouse,
    min_threshold_store = @min_threshold_store,
    min_threshold_warehouse = @min_threshold_warehouse,
    purchased_at = @purchased_at,
    last_sale_at = @last_sale_at,
    thumbnail_path = @thumbnail_path,
    product_dna = @product_dna,
    updated_at = @updated_at
WHERE id = @id;";

        command.Parameters.AddWithValue("@id", product.Id);
        command.Parameters.AddWithValue("@sku", (object?)product.Sku ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", product.Name);
        command.Parameters.AddWithValue("@barcode", (object?)product.Barcode ?? DBNull.Value);
        command.Parameters.AddWithValue("@unit", (object?)product.Unit ?? DBNull.Value);
        command.Parameters.AddWithValue("@price_cents", MoneyUtils.ToCents(product.Price));
        command.Parameters.AddWithValue("@cost_price_cents", MoneyUtils.ToCents(product.CostPrice));
        command.Parameters.AddWithValue("@tax_category_id", product.TaxCategoryId);
        command.Parameters.AddWithValue("@tax_group_id", product.TaxGroupId ?? 1);
        InventoryStockRules.BindOwnedProductWriteParameters(command, product);
        command.Parameters.AddWithValue("@quantity", product.Quantity);
        command.Parameters.AddWithValue(
            "@purchased_at",
            (object?)(product.PurchasedAt?.ToUniversalTime().ToString("O")) ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@last_sale_at",
            (object?)(product.LastSaleAt?.ToUniversalTime().ToString("O")) ?? DBNull.Value);
        command.Parameters.AddWithValue("@thumbnail_path", (object?)product.ThumbnailPath ?? DBNull.Value);
        // Preserve existing DNA; generate if missing (legacy product)
        if (string.IsNullOrWhiteSpace(product.ProductDna))
        {
            product.ProductDna = ProductDnaGenerator.Generate(product.Name);
        }
        command.Parameters.AddWithValue("@product_dna", product.ProductDna);
        command.Parameters.AddWithValue("@updated_at", now);

        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM products WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private static Product MapProduct(SqliteDataReader reader)
    {
        var product = new Product
        {
            Id = reader.GetInt64(0),
            Sku = reader.IsDBNull(1) ? null : reader.GetString(1),
            Name = reader.GetString(2),
            Barcode = reader.IsDBNull(3) ? null : reader.GetString(3),
            Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
            Price = MoneyUtils.FromCents(reader.GetInt64(5)),
            CostPrice = MoneyUtils.FromCents(reader.GetInt64(6)),
            TaxCategoryId = reader.IsDBNull(7) ? 1 : reader.GetInt64(7),
            CashierName = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
            ThumbnailPath = reader.IsDBNull(15) ? null : reader.GetString(15),
            TaxRatePercent = reader.IsDBNull(16) ? 0m : (decimal)reader.GetDouble(16),
            TaxGroupId = reader.IsDBNull(17) ? 1 : reader.GetInt64(17),
            ProductDna = reader.IsDBNull(18) ? null : reader.GetString(18)
        };

        InventoryStockRules.ApplyOwnedProductColumns(product, reader, 8, 9, 10, 11, 12, 13);
        return product;
    }

    private static void NormalizeOwnedWrite(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            throw new InvalidOperationException("Products owner path requires a non-empty product name.");
        }

        product.Name = product.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(product.Sku) ? null : product.Sku.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        product.Unit = string.IsNullOrWhiteSpace(product.Unit) ? null : product.Unit.Trim();
    }
}
