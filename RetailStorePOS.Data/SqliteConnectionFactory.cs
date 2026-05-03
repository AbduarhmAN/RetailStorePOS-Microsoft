using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _databasePath;
    public string DatabasePath => _databasePath;

    public SqliteConnectionFactory(string databasePath)
    {
        _databasePath = DatabasePaths.NormalizeLocalPath(databasePath);
        EnsureLocalStartupPath();
    }

    public SqliteConnection OpenConnection()
    {
        EnsureLocalStartupPath();
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

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
