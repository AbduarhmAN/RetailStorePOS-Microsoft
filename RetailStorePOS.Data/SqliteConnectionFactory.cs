using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _databasePath;
    private readonly Lazy<EncryptionPreparationResult> _preparedMode;
    public string DatabasePath => _databasePath;

    public SqliteConnectionFactory(string databasePath)
    {
        _databasePath = DatabasePaths.NormalizeLocalPath(databasePath);
        EnsureLocalStartupPath();
        _preparedMode = new Lazy<EncryptionPreparationResult>(
            () => DatabaseEncryptionService.PrepareDatabaseAccess(_databasePath),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public SqliteConnection OpenConnection()
    {
        EnsureLocalStartupPath();
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = DatabaseEncryptionService.OpenPreparedConnection(_databasePath, _preparedMode.Value);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();

        return connection;
    }

    private void EnsureLocalStartupPath()
    {
        _ = DatabasePaths.NormalizeLocalPath(_databasePath);
    }
}
