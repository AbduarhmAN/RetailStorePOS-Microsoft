using System.Diagnostics;
using RetailStorePOS.Data;

namespace RetailStorePOS.App.Services;

public class TimeValidationService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public TimeValidationService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Checks if the current system time is valid by ensuring it is not older than the newest 
    /// timestamp recorded in the local database (sales, sessions, or telemetry).
    /// </summary>
    public async Task<bool> IsSystemTimeValidAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var currentUtc = DateTime.UtcNow;
                var maxDbTime = GetMaxDatabaseTimestamp();

                // Allow a small grace period (e.g., 5 seconds) to account for rapid checks
                if (maxDbTime.HasValue && currentUtc < maxDbTime.Value.AddSeconds(-5))
                {
                    Debug.WriteLine($"[TIME VALIDATION] Failed: Current time {currentUtc:O} is older than max DB time {maxDbTime.Value:O}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TIME VALIDATION] Error checking system time: {ex.Message}");
                // In case of a brand new DB or missing tables, assume time is valid to avoid soft-locking
                return true;
            }
        });
    }

    private DateTime? GetMaxDatabaseTimestamp()
    {
        using var connection = _connectionFactory.OpenConnection();

        DateTime? maxTime = null;

        // Check Sales
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(created_at) FROM sales;";
            var result = cmd.ExecuteScalar();
            if (result != DBNull.Value && result != null)
            {
                if (DateTime.TryParse(result.ToString(), out var parsedTime))
                {
                    maxTime = GetLaterTime(maxTime, parsedTime.ToUniversalTime());
                }
            }
        }

        // Check Register Sessions
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(opened_at), MAX(closed_at) FROM register_sessions;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                if (!reader.IsDBNull(0) && DateTime.TryParse(reader.GetString(0), out var openedAt))
                {
                    maxTime = GetLaterTime(maxTime, openedAt.ToUniversalTime());
                }
                if (!reader.IsDBNull(1) && DateTime.TryParse(reader.GetString(1), out var closedAt))
                {
                    maxTime = GetLaterTime(maxTime, closedAt.ToUniversalTime());
                }
            }
        }

        // Check Installation Events (Telemetry)
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(occurred_at) FROM installation_events;";
            var result = cmd.ExecuteScalar();
            if (result != DBNull.Value && result != null)
            {
                if (DateTime.TryParse(result.ToString(), out var parsedTime))
                {
                    maxTime = GetLaterTime(maxTime, parsedTime.ToUniversalTime());
                }
            }
        }

        return maxTime;
    }

    private DateTime? GetLaterTime(DateTime? t1, DateTime? t2)
    {
        if (!t1.HasValue) return t2;
        if (!t2.HasValue) return t1;
        return t1.Value > t2.Value ? t1 : t2;
    }
}
