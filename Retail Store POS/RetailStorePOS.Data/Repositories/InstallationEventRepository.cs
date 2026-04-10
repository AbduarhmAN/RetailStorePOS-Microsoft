namespace RetailStorePOS.Data.Repositories;

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
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR IGNORE INTO installation_events
    (id, install_id, event_type, occurred_at, payload_json, created_at)
VALUES
    (@id, @install_id, @event_type, @occurred_at, @payload_json, @created_at);";

        command.Parameters.AddWithValue("@id", eventId);
        command.Parameters.AddWithValue("@install_id", installId);
        command.Parameters.AddWithValue("@event_type", eventType);
        command.Parameters.AddWithValue("@occurred_at", occurredAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@payload_json", payloadJson);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }
}
