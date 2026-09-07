using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data.Modules.Tax;

public sealed class TaxCategoryRepository
{
    private const string OwnedTableName = "tax_categories";
    private readonly SqliteConnectionFactory _factory;

    public TaxCategoryRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public List<TaxCategory> GetAll()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, rate_percent, is_default
FROM tax_categories
ORDER BY is_default DESC, name;";

        using var reader = command.ExecuteReader();
        var results = new List<TaxCategory>();
        while (reader.Read())
        {
            results.Add(MapTaxCategory(reader));
        }

        return results;
    }

    public TaxCategory? GetById(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, rate_percent, is_default
FROM tax_categories
WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapTaxCategory(reader);
    }

    public TaxCategory? GetDefault()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, rate_percent, is_default
FROM tax_categories
WHERE is_default = 1
LIMIT 1;";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapTaxCategory(reader);
    }

    public long Create(TaxCategory category)
    {
        NormalizeOwnedWrite(category);
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO tax_categories (name, rate_percent, is_default)
VALUES (@name, @rate_percent, @is_default);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@rate_percent", category.RatePercent);
        command.Parameters.AddWithValue("@is_default", category.IsDefault ? 1 : 0);

        var result = command.ExecuteScalar();
        category.Id = Convert.ToInt64(result);
        return category.Id;
    }

    public void Update(TaxCategory category)
    {
        NormalizeOwnedWrite(category);
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE tax_categories
SET name = @name,
    rate_percent = @rate_percent,
    is_default = @is_default
WHERE id = @id;";

        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@rate_percent", category.RatePercent);
        command.Parameters.AddWithValue("@is_default", category.IsDefault ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tax_categories WHERE id = @id AND is_default = 0;";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private static TaxCategory MapTaxCategory(SqliteDataReader reader)
    {
        return new TaxCategory
        {
            Id = Convert.ToInt64(reader.GetValue(0)),
            Name = reader.GetString(1),
            RatePercent = (decimal)Convert.ToDouble(reader.GetValue(2)),
            IsDefault = Convert.ToInt32(reader.GetValue(3)) == 1
        };
    }

    private static void NormalizeOwnedWrite(TaxCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            throw new InvalidOperationException($"{OwnedTableName} writes require a non-empty category name.");
        }

        category.Name = category.Name.Trim();
    }
}
