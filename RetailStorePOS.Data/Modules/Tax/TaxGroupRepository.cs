using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data.Modules.Tax;

public sealed class TaxGroupRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TaxRuleRepository _ruleRepo;

    public TaxGroupRepository(SqliteConnectionFactory factory, TaxRuleRepository ruleRepo)
    {
        _factory = factory;
        _ruleRepo = ruleRepo;
    }

    public List<TaxGroup> GetAllWithRules()
    {
        var allRules = _ruleRepo.GetAll().ToDictionary(r => r.Id);

        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, is_default, is_active, created_at, is_auto_managed
FROM tax_groups
ORDER BY is_default DESC, name;";

        using var reader = command.ExecuteReader();
        var results = new List<TaxGroup>();
        while (reader.Read())
        {
            results.Add(MapTaxGroup(reader));
        }
        reader.Close();

        // Hydrate rules per group via junction table
        using var juncCmd = connection.CreateCommand();
        juncCmd.CommandText = "SELECT group_id, rule_id, sequence_order FROM tax_group_rules;";
        using var juncReader = juncCmd.ExecuteReader();

        while (juncReader.Read())
        {
            long groupId = juncReader.GetInt64(0);
            long ruleId = juncReader.GetInt64(1);
            int seqOrder = juncReader.GetInt32(2);

            var group = results.FirstOrDefault(g => g.Id == groupId);
            if (group != null && allRules.TryGetValue(ruleId, out var rule))
            {
                // We could clone the rule to assign group-specific sequence_order, 
                // but since it's just display for settings, this is usually adequate.
                group.Rules.Add(rule);
            }
        }

        return results;
    }

    public long Create(TaxGroup group, IEnumerable<long> ruleIds)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO tax_groups (name, is_default, is_active, created_at)
VALUES (@name, @def, @act, @now);
SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@name", group.Name);
            command.Parameters.AddWithValue("@def", group.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("@act", group.IsActive ? 1 : 0);

            string now = DateTime.UtcNow.ToString("O");
            command.Parameters.AddWithValue("@now", now);

            group.Id = Convert.ToInt64(command.ExecuteScalar());
            group.CreatedAt = now;

            // Insert rules
            foreach (var ruleId in ruleIds)
            {
                using var ruleCmd = connection.CreateCommand();
                ruleCmd.Transaction = transaction;
                ruleCmd.CommandText = "INSERT INTO tax_group_rules (group_id, rule_id, sequence_order) VALUES (@g_id, @r_id, 1);";
                ruleCmd.Parameters.AddWithValue("@g_id", group.Id);
                ruleCmd.Parameters.AddWithValue("@r_id", ruleId);
                ruleCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return group.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Update(TaxGroup group, IEnumerable<long> ruleIds)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
UPDATE tax_groups
SET name = @name, is_default = @def, is_active = @act
WHERE id = @id;";
            command.Parameters.AddWithValue("@id", group.Id);
            command.Parameters.AddWithValue("@name", group.Name);
            command.Parameters.AddWithValue("@def", group.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("@act", group.IsActive ? 1 : 0);
            command.ExecuteNonQuery();

            // Replace all rule relations
            using var delCmd = connection.CreateCommand();
            delCmd.Transaction = transaction;
            delCmd.CommandText = "DELETE FROM tax_group_rules WHERE group_id = @id;";
            delCmd.Parameters.AddWithValue("@id", group.Id);
            delCmd.ExecuteNonQuery();

            foreach (var ruleId in ruleIds)
            {
                using var ruleCmd = connection.CreateCommand();
                ruleCmd.Transaction = transaction;
                ruleCmd.CommandText = "INSERT INTO tax_group_rules (group_id, rule_id, sequence_order) VALUES (@g_id, @r_id, 1);";
                ruleCmd.Parameters.AddWithValue("@g_id", group.Id);
                ruleCmd.Parameters.AddWithValue("@r_id", ruleId);
                ruleCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Delete(long id)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var cmdRules = connection.CreateCommand();
            cmdRules.Transaction = transaction;
            cmdRules.CommandText = "DELETE FROM tax_group_rules WHERE group_id = @id;";
            cmdRules.Parameters.AddWithValue("@id", id);
            cmdRules.ExecuteNonQuery();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM tax_groups WHERE id = @id AND is_default = 0;";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public long GetOrCreateAutoManagedProfile(long ruleId, string ruleName)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Step 1: Check if one already exists
            using var checkCmd = connection.CreateCommand();
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = @"
SELECT tg.id FROM tax_groups tg
INNER JOIN tax_group_rules tgr ON tg.id = tgr.group_id
WHERE tg.is_auto_managed = 1
  AND tgr.rule_id = @ruleId
  AND (SELECT COUNT(*) FROM tax_group_rules WHERE group_id = tg.id) = 1
LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@ruleId", ruleId);
            var existingId = checkCmd.ExecuteScalar();
            if (existingId is not null && existingId != DBNull.Value)
            {
                return Convert.ToInt64(existingId);
            }

            // Step 2: If not found, INSERT in a transaction
            using var insertGroupCmd = connection.CreateCommand();
            insertGroupCmd.Transaction = transaction;
            insertGroupCmd.CommandText = @"
INSERT INTO tax_groups (name, is_default, is_active, is_auto_managed, created_at)
VALUES (@name, 0, 1, 1, @now);
SELECT last_insert_rowid();";
            insertGroupCmd.Parameters.AddWithValue("@name", $"_auto_{ruleName}");
            insertGroupCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

            var newId = Convert.ToInt64(insertGroupCmd.ExecuteScalar());

            using var insertRuleCmd = connection.CreateCommand();
            insertRuleCmd.Transaction = transaction;
            insertRuleCmd.CommandText = @"
INSERT INTO tax_group_rules (group_id, rule_id, sequence_order)
VALUES (@groupId, @ruleId, 1);";
            insertRuleCmd.Parameters.AddWithValue("@groupId", newId);
            insertRuleCmd.Parameters.AddWithValue("@ruleId", ruleId);
            insertRuleCmd.ExecuteNonQuery();

            transaction.Commit();
            return newId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static TaxGroup MapTaxGroup(SqliteDataReader reader)
    {
        return new TaxGroup
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            IsDefault = reader.GetInt32(2) == 1,
            IsActive = reader.GetInt32(3) == 1,
            CreatedAt = reader.GetString(4),
            IsAutoManaged = reader.FieldCount > 5 && !reader.IsDBNull(5) && reader.GetInt32(5) == 1
        };
    }
}
