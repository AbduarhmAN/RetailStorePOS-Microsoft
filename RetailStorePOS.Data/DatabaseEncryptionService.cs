using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data;

public static class DatabaseEncryptionService
{
    public const string CurrentEncryptionVersion = "1";
    public const string LegacyPlaintextEncryptionVersion = "0";

    private const int KeyByteLength = 32;
    private const int FileCleanupRetryCount = 5;
    private const int FileCleanupRetryDelayMilliseconds = 75;
    private const string SecureFolderName = "secure";
    private const string DatabaseKeyFileName = "database-key.dat";
    private const string EntropyPurpose = "RetailStorePOS.Data.DatabaseEncryption.v1";

    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes(EntropyPurpose));
    private static readonly object KeySync = new();

    static DatabaseEncryptionService()
    {
        // Explicit init required because Microsoft.Data.Sqlite.Core does NOT auto-initialize
        // the native provider. Without this, the app may silently use a non-SQLCipher build
        // and write plaintext databases without any error.
        SQLitePCL.Batteries_V2.Init();
    }

    public static EncryptionPreparationResult PrepareDatabaseAccess(string databasePath)
    {
        var localDatabasePath = DatabasePaths.NormalizeLocalPath(databasePath);
        var directory = Path.GetDirectoryName(localDatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(localDatabasePath))
        {
            return EncryptionPreparationResult.Encrypted;
        }

        if (HasDatabaseKey() && CanOpenEncrypted(localDatabasePath))
        {
            return EncryptionPreparationResult.Encrypted;
        }

        if (CanOpenPlaintext(localDatabasePath))
        {
            return EncryptionPreparationResult.LegacyPlaintext;
        }

        if (!HasDatabaseKey())
        {
            throw new DatabaseEncryptionException(
                "The database appears encrypted, but the local database encryption key file is missing.");
        }

        throw new DatabaseEncryptionException(
            "The database is not readable with the current encryption key and is not readable as plaintext.");
    }

    public static SqliteConnection OpenPreparedConnection(
        string databasePath,
        EncryptionPreparationResult preparation,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        return preparation.UsesEncryption
            ? OpenEncryptedConnection(databasePath, mode)
            : OpenPlaintextConnection(databasePath, mode);
    }

    public static SqliteConnection OpenCompatibleConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var preparation = PrepareDatabaseAccess(databasePath);
        return OpenPreparedConnection(databasePath, preparation, mode);
    }

    public static SqliteConnection OpenPlaintextConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = CreatePlaintextConnection(databasePath, mode);
        connection.Open();
        return connection;
    }

    [Obsolete("Automatic plaintext-to-encrypted conversion is deferred. Use PrepareDatabaseAccess for startup decisions.")]
    public static EncryptionPreparationResult EnsureDatabaseEncrypted(string databasePath)
    {
        return PrepareDatabaseAccess(databasePath);
    }

    /// <remarks>
    /// The <paramref name="mode"/> parameter defaults to ReadWriteCreate.
    /// ReadOnly mode sets requireExistingKey=true but is not currently used by any caller.
    /// </remarks>
    public static SqliteConnection OpenEncryptedConnection(string databasePath, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = CreateEncryptedConnection(databasePath, mode, requireExistingKey: mode == SqliteOpenMode.ReadOnly);
        connection.Open();
        return connection;
    }

    public static SqliteConnection CreateEncryptedConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
        bool requireExistingKey = false,
        SqliteCacheMode cacheMode = SqliteCacheMode.Shared,
        bool pooling = true,
        bool useUriFilename = false)
    {
        var localDatabasePath = DatabasePaths.NormalizeLocalPath(databasePath);
        var builder = CreateBaseConnectionString(localDatabasePath, mode, cacheMode, pooling, useUriFilename);
        builder.Password = GetDatabasePassword(requireExistingKey || File.Exists(localDatabasePath));
        return new SqliteConnection(builder.ToString());
    }

    public static SqliteConnection CreatePlaintextConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWrite,
        SqliteCacheMode cacheMode = SqliteCacheMode.Shared,
        bool pooling = true,
        bool useUriFilename = false)
    {
        var builder = CreateBaseConnectionString(databasePath, mode, cacheMode, pooling, useUriFilename);
        return new SqliteConnection(builder.ToString());
    }

    private static SqliteConnectionStringBuilder CreateBaseConnectionString(
        string databasePath,
        SqliteOpenMode mode,
        SqliteCacheMode cacheMode,
        bool pooling,
        bool useUriFilename)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = useUriFilename
                ? ToSqliteUriFilename(databasePath)
                : DatabasePaths.NormalizeLocalPath(databasePath),
            Mode = mode,
            Cache = cacheMode,
            Pooling = pooling
        };
    }

    private static bool CanOpenEncrypted(string databasePath)
    {
        try
        {
            using var connection = CreateEncryptedConnection(
                databasePath,
                SqliteOpenMode.ReadWrite,
                requireExistingKey: true,
                cacheMode: SqliteCacheMode.Private,
                pooling: false,
                useUriFilename: true);
            connection.Open();
            ExecuteScalar(connection, "PRAGMA user_version;");
            return true;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static bool CanOpenPlaintext(string databasePath)
    {
        try
        {
            using var connection = CreatePlaintextConnection(
                databasePath,
                cacheMode: SqliteCacheMode.Private,
                pooling: false,
                useUriFilename: true);
            connection.Open();
            ExecuteScalar(connection, "PRAGMA user_version;");
            return true;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException)
        {
            return false;
        }
    }

    private static EncryptionPreparationResult EncryptPlaintextDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        EnsureCheckpointedPlaintextDatabase(databasePath);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = $"{databasePath}.unencrypted-pre-sqlcipher-{timestamp}.bak";
        var databaseDirectory = Path.GetDirectoryName(databasePath)
            ?? throw new DatabaseEncryptionException("Database path must include a directory.");
        var encryptedTempPath = Path.Combine(
            databaseDirectory,
            $"{Path.GetFileNameWithoutExtension(databasePath)}.sqlcipher-{timestamp}.db");

        File.Copy(databasePath, backupPath, overwrite: false);

        var password = GetDatabasePassword(requireExistingKey: false);
        var sourceUserVersion = 0;

        using (var sourceConnection = CreatePlaintextConnection(
                   databasePath,
                   cacheMode: SqliteCacheMode.Private,
                   pooling: false,
                   useUriFilename: true))
        {
            sourceConnection.Open();
            sourceUserVersion = Convert.ToInt32(ExecuteScalar(sourceConnection, "PRAGMA user_version;") ?? 0);

            try
            {
                AttachEncryptedDatabase(sourceConnection, encryptedTempPath, password);
                ExecuteNonQuery(sourceConnection, "SELECT sqlcipher_export('encrypted');");
                ExecuteNonQuery(sourceConnection, $"PRAGMA encrypted.user_version = {sourceUserVersion};");
                ExecuteNonQuery(sourceConnection, "PRAGMA encrypted.wal_checkpoint(TRUNCATE);");
                ExecuteNonQuery(sourceConnection, "PRAGMA encrypted.journal_mode = DELETE;");
            }
            finally
            {
                TryDetachEncryptedDatabase(sourceConnection);
            }
        }

        FinalizeEncryptedDatabase(encryptedTempPath);
        SqliteConnection.ClearAllPools();
        DeleteDatabaseSidecarFiles(databasePath);
        File.Copy(encryptedTempPath, databasePath, overwrite: true);
        SqliteConnection.ClearAllPools();

        // Clean up migration artifacts — the encrypted DB is validated and in place.
        // The plaintext backup and temp file are no longer needed and leaving plaintext
        // on disk undermines the purpose of encryption.
        DeleteDatabaseSidecarFiles(encryptedTempPath);
        EnsureDeleted(encryptedTempPath, "encrypted migration temp database");
        EnsureDeleted(backupPath, "plaintext migration backup");

        return new EncryptionPreparationResult(DatabaseStorageMode.Encrypted, true, backupPath, encryptedTempPath);
    }

    private static void AttachEncryptedDatabase(SqliteConnection sourceConnection, string encryptedTempPath, string password)
    {
        using var attach = sourceConnection.CreateCommand();
        attach.CommandText = "ATTACH DATABASE $path AS encrypted KEY $password;";
        attach.Parameters.AddWithValue("$path", ToSqliteUriFilename(encryptedTempPath));
        attach.Parameters.AddWithValue("$password", password);
        attach.ExecuteNonQuery();
    }

    private static void TryDetachEncryptedDatabase(SqliteConnection sourceConnection)
    {
        try
        {
            ExecuteNonQuery(sourceConnection, "DETACH DATABASE encrypted;");
        }
        catch (SqliteException)
        {
            // If attach failed, there is nothing to detach. Preserve the original failure.
        }
    }

    private static void EnsureCheckpointedPlaintextDatabase(string databasePath)
    {
        using var connection = CreatePlaintextConnection(
            databasePath,
            cacheMode: SqliteCacheMode.Private,
            pooling: false,
            useUriFilename: true);
        connection.Open();
        using var checkpoint = connection.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        checkpoint.ExecuteNonQuery();
    }

    private static void FinalizeEncryptedDatabase(string databasePath)
    {
        using var connection = CreateEncryptedConnection(
            databasePath,
            SqliteOpenMode.ReadWrite,
            requireExistingKey: true,
            cacheMode: SqliteCacheMode.Private,
            pooling: false,
            useUriFilename: true);
        connection.Open();
        ExecuteNonQuery(connection, "PRAGMA wal_checkpoint(TRUNCATE);");
        ExecuteNonQuery(connection, "PRAGMA journal_mode = DELETE;");
        var result = ExecuteScalar(connection, "PRAGMA quick_check;")?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new DatabaseEncryptionException($"Encrypted database validation failed: {result}");
        }
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private static object? ExecuteScalar(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return command.ExecuteScalar();
    }

    private static string ToSqliteUriFilename(string databasePath)
    {
        var normalizedPath = DatabasePaths.NormalizeLocalPath(databasePath);
        return new Uri(Path.GetFullPath(normalizedPath)).AbsoluteUri;
    }

    private static void DeleteDatabaseSidecarFiles(string databasePath)
    {
        EnsureDeleted($"{databasePath}-wal", "database WAL sidecar");
        EnsureDeleted($"{databasePath}-shm", "database shared-memory sidecar");
    }

    private static void EnsureDeleted(string filePath, string artifactDescription)
    {
        if (!TryDeleteFileWithRetries(filePath))
        {
            throw new DatabaseEncryptionException($"Unable to delete {artifactDescription}: {filePath}");
        }
    }

    private static bool TryDeleteFileWithRetries(string filePath)
    {
        for (var attempt = 0; attempt <= FileCleanupRetryCount; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return true;
                }

                File.Delete(filePath);
                return true;
            }
            catch (IOException) when (attempt < FileCleanupRetryCount)
            {
                PrepareForFileCleanupRetry();
            }
            catch (UnauthorizedAccessException) when (attempt < FileCleanupRetryCount)
            {
                PrepareForFileCleanupRetry();
            }
        }

        return !File.Exists(filePath);
    }

    private static void PrepareForFileCleanupRetry()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Thread.Sleep(FileCleanupRetryDelayMilliseconds);
    }

    private static bool HasDatabaseKey()
    {
        return File.Exists(GetDatabaseKeyPath());
    }

    private static string GetDatabasePassword(bool requireExistingKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Database encryption uses Windows DPAPI and requires Windows.");
        }

        lock (KeySync)
        {
            var keyPath = GetDatabaseKeyPath();
            if (File.Exists(keyPath))
            {
                return Convert.ToBase64String(UnprotectKey(File.ReadAllBytes(keyPath)));
            }

            if (requireExistingKey)
            {
                throw new DatabaseEncryptionException("The database encryption key file is missing.");
            }

            var key = RandomNumberGenerator.GetBytes(KeyByteLength);
            var protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);
            var directory = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // TODO: P2-14 hardening — set explicit file ACLs on the key file to restrict
            // access to the current user identity only. DPAPI CurrentUser scope already
            // ties decryption to this Windows login, but ACLs add defense-in-depth on
            // shared machines.
            File.WriteAllBytes(keyPath, protectedKey);
            return Convert.ToBase64String(key);
        }
    }

    private static byte[] UnprotectKey(byte[] protectedKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Database encryption uses Windows DPAPI and requires Windows.");
        }

        var key = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);
        if (key.Length != KeyByteLength)
        {
            throw new DatabaseEncryptionException("The database encryption key has an invalid length.");
        }

        return key;
    }

    private static string GetDatabaseKeyPath()
    {
        return AppDataPaths.Combine(SecureFolderName, DatabaseKeyFileName);
    }
}

public enum DatabaseStorageMode
{
    Encrypted,
    LegacyPlaintext
}

public sealed record EncryptionPreparationResult(
    DatabaseStorageMode StorageMode,
    bool WasMigratedToEncrypted,
    string? PlaintextBackupPath,
    string? EncryptedTempPath)
{
    public bool UsesEncryption => StorageMode == DatabaseStorageMode.Encrypted;

    public string MetadataEncryptionVersion => UsesEncryption
        ? DatabaseEncryptionService.CurrentEncryptionVersion
        : DatabaseEncryptionService.LegacyPlaintextEncryptionVersion;

    public static EncryptionPreparationResult Encrypted { get; } = new(DatabaseStorageMode.Encrypted, false, null, null);
    public static EncryptionPreparationResult LegacyPlaintext { get; } = new(DatabaseStorageMode.LegacyPlaintext, false, null, null);
}

public sealed class DatabaseEncryptionException : InvalidOperationException
{
    public DatabaseEncryptionException(string message)
        : base(message)
    {
    }
}
