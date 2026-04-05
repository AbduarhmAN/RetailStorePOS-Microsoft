using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class BackupRunRepository
{
    private readonly SqliteConnectionFactory _factory;

    public BackupRunRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    // Scaffolding methods to be implemented
    public void RecordBackup(BackupRun run)
    {
        // To be implemented
    }

    public List<BackupRun> GetRecentBackups(int limit = 10)
    {
        // To be implemented
        return new List<BackupRun>();
    }
}
