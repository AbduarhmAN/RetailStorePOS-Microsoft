using System.Globalization;

namespace RetailStorePOS.Data.Repositories;

public sealed record TelemetryOutboxRecord
{
    public long Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public bool MergeDuplicates { get; init; }
    public int AttemptCount { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string? LastError { get; init; }
    public DateTime? SentAt { get; init; }
}

public sealed class TelemetryOutboxRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TelemetryOutboxRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public long Enqueue(string endpoint, string payloadJson, bool mergeDuplicates)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO telemetry_outbox (created_at, endpoint, payload_json, merge_duplicates, attempt_count)
VALUES (@created_at, @endpoint, @payload_json, @merge_duplicates, 0);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@endpoint", endpoint);
        command.Parameters.AddWithValue("@payload_json", payloadJson);
        command.Parameters.AddWithValue("@merge_duplicates", mergeDuplicates ? 1 : 0);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public IReadOnlyList<TelemetryOutboxRecord> GetPending(int limit)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, created_at, endpoint, payload_json, merge_duplicates, attempt_count, last_attempt_at, last_error, sent_at
FROM telemetry_outbox
WHERE sent_at IS NULL
ORDER BY created_at ASC, id ASC
LIMIT @limit;";
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<TelemetryOutboxRecord>();

        while (reader.Read())
        {
            results.Add(new TelemetryOutboxRecord
            {
                Id = reader.GetInt64(0),
                CreatedAt = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind),
                Endpoint = reader.GetString(2),
                PayloadJson = reader.GetString(3),
                MergeDuplicates = reader.GetInt64(4) != 0,
                AttemptCount = reader.GetInt32(5),
                LastAttemptAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind),
                LastError = reader.IsDBNull(7) ? null : reader.GetString(7),
                SentAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8), null, DateTimeStyles.RoundtripKind)
            });
        }

        return results;
    }

    public void MarkSent(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE telemetry_outbox
SET sent_at = @sent_at,
    last_attempt_at = @sent_at,
    last_error = NULL
WHERE id = @id;";
        command.Parameters.AddWithValue("@sent_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void MarkFailed(long id, string errorMessage)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE telemetry_outbox
SET attempt_count = attempt_count + 1,
    last_attempt_at = @last_attempt_at,
    last_error = @last_error
WHERE id = @id;";
        command.Parameters.AddWithValue("@last_attempt_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@last_error", errorMessage);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void CleanupSentOlderThan(DateTime cutoffUtc)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
DELETE FROM telemetry_outbox
WHERE sent_at IS NOT NULL AND sent_at < @cutoff;";
        command.Parameters.AddWithValue("@cutoff", cutoffUtc.ToString("O"));
        command.ExecuteNonQuery();
    }
}
