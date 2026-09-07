using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class AuditLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AuditLogRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Create(AuditLog log)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();

        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = @"
INSERT INTO audit_logs (user_id, action, details, created_at)
VALUES (@user_id, @action, @details, @created_at);";

        command.Parameters.AddWithValue("@user_id", (object?)log.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@action", log.Action);
        command.Parameters.AddWithValue("@details", (object?)log.Details ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", now);

        command.ExecuteNonQuery();
    }

    public List<AuditLog> GetAll()
    {
        var logs = new List<AuditLog>();
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, user_id, action, details, created_at FROM audit_logs ORDER BY id ASC;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new AuditLog
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                UserId = reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1)),
                Action = reader.GetString(2),
                Details = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.IsDBNull(4) ? default : DateTime.Parse(reader.GetString(4)).ToUniversalTime()
            });
        }
        return logs;
    }
}
