using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data.Modules.Tax;

public sealed class TaxAuthorityRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TaxAuthorityRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public List<TaxAuthority> GetAll()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, authority_code, registration_number, created_at
FROM tax_authorities
ORDER BY name;";

        using var reader = command.ExecuteReader();
        var results = new List<TaxAuthority>();
        while (reader.Read())
        {
            results.Add(MapTaxAuthority(reader));
        }

        return results;
    }

    public long Create(TaxAuthority authority)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO tax_authorities (name, authority_code, registration_number, created_at)
VALUES (@name, @code, @reg, @created_at);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@name", authority.Name);
        command.Parameters.AddWithValue("@code", authority.AuthorityCode);
        command.Parameters.AddWithValue("@reg", (object?)authority.RegistrationNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));

        var result = command.ExecuteScalar();
        authority.Id = Convert.ToInt64(result);
        authority.CreatedAt = command.Parameters["@created_at"].Value?.ToString() ?? "";
        return authority.Id;
    }

    public void Update(TaxAuthority authority)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE tax_authorities
SET name = @name,
    authority_code = @code,
    registration_number = @reg
WHERE id = @id;";

        command.Parameters.AddWithValue("@id", authority.Id);
        command.Parameters.AddWithValue("@name", authority.Name);
        command.Parameters.AddWithValue("@code", authority.AuthorityCode);
        command.Parameters.AddWithValue("@reg", (object?)authority.RegistrationNumber ?? DBNull.Value);

        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tax_authorities WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private static TaxAuthority MapTaxAuthority(SqliteDataReader reader)
    {
        return new TaxAuthority
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            AuthorityCode = reader.GetString(2),
            RegistrationNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
            CreatedAt = reader.GetString(4)
        };
    }
}
