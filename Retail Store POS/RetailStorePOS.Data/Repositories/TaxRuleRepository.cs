using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class TaxRuleRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TaxRuleRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public List<TaxRule> GetAll()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, tax_authority_id, calc_type, scope, rate_value, is_inclusive, 
       applies_after_discount, sequence_order, min_tax_amount_cents, max_tax_amount_cents,
       threshold_min_cents, threshold_max_cents, threshold_scope, effective_from, 
       effective_until, is_active, is_default, created_at, updated_at
FROM tax_rules
ORDER BY name;";

        using var reader = command.ExecuteReader();
        var results = new List<TaxRule>();
        while (reader.Read())
        {
            results.Add(MapTaxRule(reader));
        }

        return results;
    }

    public long Create(TaxRule rule, long? currentUserId = null)
    {
        using var connection = _factory.OpenConnection();
        
        // Start transaction for dual-write
        using var transaction = connection.BeginTransaction();
        
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO tax_rules (
    name, tax_authority_id, calc_type, scope, rate_value, is_inclusive, 
    applies_after_discount, sequence_order, min_tax_amount_cents, max_tax_amount_cents,
    threshold_min_cents, threshold_max_cents, threshold_scope, effective_from, 
    effective_until, is_active, is_default, created_at, updated_at
)
VALUES (
    @name, @auth_id, @type, @scope, @rate, @inc, 
    @dis, @seq, @min_amt, @max_amt,
    @th_min, @th_max, @th_scope, @eff_from, 
    @eff_until, @active, @def, @now, @now
);
SELECT last_insert_rowid();";

            AddRuleParameters(command, rule);
            string now = DateTime.UtcNow.ToString("O");
            command.Parameters.AddWithValue("@now", now);

            rule.Id = Convert.ToInt64(command.ExecuteScalar());
            rule.CreatedAt = now;
            rule.UpdatedAt = now;

            // Dual-write: Log initial rate to history
            using var historyCmd = connection.CreateCommand();
            historyCmd.Transaction = transaction;
            historyCmd.CommandText = @"
INSERT INTO tax_rule_rate_history (tax_rule_id, old_rate_value, new_rate_value, changed_at, changed_by_user_id)
VALUES (@id, 0, @rate, @now, @uid);";
            historyCmd.Parameters.AddWithValue("@id", rule.Id);
            historyCmd.Parameters.AddWithValue("@rate", rule.RateValue);
            historyCmd.Parameters.AddWithValue("@now", now);
            historyCmd.Parameters.AddWithValue("@uid", (object?)currentUserId ?? DBNull.Value);
            historyCmd.ExecuteNonQuery();

            transaction.Commit();
            return rule.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Update(TaxRule rule, long? currentUserId = null)
    {
        using var connection = _factory.OpenConnection();
        
        // Need to check the old rate to see if we need a history entry
        double oldRate = 0;
        using (var getOldCmd = connection.CreateCommand())
        {
            getOldCmd.CommandText = "SELECT rate_value FROM tax_rules WHERE id = @id";
            getOldCmd.Parameters.AddWithValue("@id", rule.Id);
            var res = getOldCmd.ExecuteScalar();
            if (res != null) oldRate = Convert.ToDouble(res);
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            string now = DateTime.UtcNow.ToString("O");
            
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
UPDATE tax_rules
SET name = @name,
    tax_authority_id = @auth_id,
    calc_type = @type,
    scope = @scope,
    rate_value = @rate,
    is_inclusive = @inc,
    applies_after_discount = @dis,
    sequence_order = @seq,
    min_tax_amount_cents = @min_amt,
    max_tax_amount_cents = @max_amt,
    threshold_min_cents = @th_min,
    threshold_max_cents = @th_max,
    threshold_scope = @th_scope,
    effective_from = @eff_from,
    effective_until = @eff_until,
    is_active = @active,
    is_default = @def,
    updated_at = @now
WHERE id = @id;";
            
            command.Parameters.AddWithValue("@id", rule.Id);
            AddRuleParameters(command, rule);
            command.Parameters.AddWithValue("@now", now);
            command.ExecuteNonQuery();

            // Dual-write: only log if rate actually changed
            if (Math.Abs(oldRate - (double)rule.RateValue) > 0.0001)
            {
                using var historyCmd = connection.CreateCommand();
                historyCmd.Transaction = transaction;
                historyCmd.CommandText = @"
INSERT INTO tax_rule_rate_history (tax_rule_id, old_rate_value, new_rate_value, changed_at, changed_by_user_id)
VALUES (@id, @old, @new, @now, @uid);";
                historyCmd.Parameters.AddWithValue("@id", rule.Id);
                historyCmd.Parameters.AddWithValue("@old", oldRate);
                historyCmd.Parameters.AddWithValue("@new", rule.RateValue);
                historyCmd.Parameters.AddWithValue("@now", now);
                historyCmd.Parameters.AddWithValue("@uid", (object?)currentUserId ?? DBNull.Value);
                historyCmd.ExecuteNonQuery();
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
            // Clear dual-write rate history which has no ON DELETE CASCADE
            using var cmdHistory = connection.CreateCommand();
            cmdHistory.Transaction = transaction;
            cmdHistory.CommandText = "DELETE FROM tax_rule_rate_history WHERE tax_rule_id = @id;";
            cmdHistory.Parameters.AddWithValue("@id", id);
            cmdHistory.ExecuteNonQuery();

            // Attempt to delete the rule. 
            // If it is bound to a TaxGroup in tax_group_rules, this will intentionally throw a FK exception 
            // which the UI catches to say "Cannot delete: currently in use by a group."
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM tax_rules WHERE id = @id;";
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

    private static void AddRuleParameters(SqliteCommand command, TaxRule rule)
    {
        command.Parameters.AddWithValue("@name", rule.Name);
        command.Parameters.AddWithValue("@auth_id", (object?)rule.TaxAuthorityId ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", rule.CalcType);
        command.Parameters.AddWithValue("@scope", rule.Scope);
        command.Parameters.AddWithValue("@rate", rule.RateValue);
        command.Parameters.AddWithValue("@inc", rule.IsInclusive ? 1 : 0);
        command.Parameters.AddWithValue("@dis", rule.AppliesAfterDiscount ? 1 : 0);
        command.Parameters.AddWithValue("@seq", rule.SequenceOrder);
        command.Parameters.AddWithValue("@min_amt", (object?)rule.MinTaxAmountCents ?? DBNull.Value);
        command.Parameters.AddWithValue("@max_amt", (object?)rule.MaxTaxAmountCents ?? DBNull.Value);
        command.Parameters.AddWithValue("@th_min", (object?)rule.ThresholdMinCents ?? DBNull.Value);
        command.Parameters.AddWithValue("@th_max", (object?)rule.ThresholdMaxCents ?? DBNull.Value);
        command.Parameters.AddWithValue("@th_scope", (object?)rule.ThresholdScope ?? DBNull.Value);
        command.Parameters.AddWithValue("@eff_from", (object?)rule.EffectiveFrom ?? DBNull.Value);
        command.Parameters.AddWithValue("@eff_until", (object?)rule.EffectiveUntil ?? DBNull.Value);
        command.Parameters.AddWithValue("@active", rule.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@def", rule.IsDefault ? 1 : 0);
    }

    private static TaxRule MapTaxRule(SqliteDataReader reader)
    {
        return new TaxRule
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            TaxAuthorityId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
            CalcType = reader.GetString(3),
            Scope = reader.GetString(4),
            RateValue = (decimal)reader.GetDouble(5),
            IsInclusive = reader.GetInt32(6) == 1,
            AppliesAfterDiscount = reader.GetInt32(7) == 1,
            SequenceOrder = reader.GetInt32(8),
            MinTaxAmountCents = reader.IsDBNull(9) ? null : reader.GetInt64(9),
            MaxTaxAmountCents = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            ThresholdMinCents = reader.IsDBNull(11) ? null : reader.GetInt64(11),
            ThresholdMaxCents = reader.IsDBNull(12) ? null : reader.GetInt64(12),
            ThresholdScope = reader.IsDBNull(13) ? null : reader.GetString(13),
            EffectiveFrom = reader.IsDBNull(14) ? null : reader.GetString(14),
            EffectiveUntil = reader.IsDBNull(15) ? null : reader.GetString(15),
            IsActive = reader.GetInt32(16) == 1,
            IsDefault = reader.GetInt32(17) == 1,
            CreatedAt = reader.GetString(18),
            UpdatedAt = reader.GetString(19)
        };
    }
}
