namespace RetailStorePOS.Data.Modules.Telemetry;

public sealed class InstallationEventRepository
{
    private readonly SqliteConnectionFactory _factory;

    public InstallationEventRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public bool TryInsert(string eventId, string installId, string eventType, DateTime occurredAtUtc, string payloadJson)
    {
        using var connection = _factory.OpenConnection();
        return TryInsert(connection, null, eventId, installId, eventType, occurredAtUtc, payloadJson);
    }

    public bool TryInsert(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction? transaction, string eventId, string installId, string eventType, DateTime occurredAtUtc, string payloadJson, string? runId = null, DateTime? lastActivityAt = null, string? lastActivitySource = null)
    {
        using var command = connection.CreateCommand();
        if (transaction != null)
        {
            command.Transaction = transaction;
        }

        command.CommandText = @"
INSERT OR IGNORE INTO installation_events
    (id, install_id, event_type, occurred_at, payload_json, created_at, run_id, last_activity_at, last_activity_source)
VALUES
    (@id, @install_id, @event_type, @occurred_at, @payload_json, @created_at, @run_id, @last_activity_at, @last_activity_source);";

        command.Parameters.AddWithValue("@id", eventId);
        command.Parameters.AddWithValue("@install_id", installId);
        command.Parameters.AddWithValue("@event_type", eventType);
        command.Parameters.AddWithValue("@occurred_at", occurredAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@payload_json", payloadJson);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@run_id", (object?)runId ?? DBNull.Value);
        command.Parameters.AddWithValue("@last_activity_at", (object?)lastActivityAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@last_activity_source", (object?)lastActivitySource ?? DBNull.Value);

        return command.ExecuteNonQuery() > 0;
    }
}
