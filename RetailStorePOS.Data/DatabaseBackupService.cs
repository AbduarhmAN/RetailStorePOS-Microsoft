using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data;

/// <summary>
/// Periodic snapshots of <c>pos.db</c> plus integrity checking + best-effort
/// restore. Lives in <c>RetailStorePOS.Data</c> so it can reach the
/// <see cref="DatabaseEncryptionService"/> APIs and so test projects (which
/// already reference Data) can exercise it without pulling in the WinUI host.
///
/// Backup file naming: <c>pos_{yyyyMMddHHmmss}.db</c>. The lexical sort of
/// these names matches the chronological sort, so "latest" is just the
/// max-by-name file in the backup folder.
///
/// Why a file copy and not <c>VACUUM INTO</c>? With SQLCipher the encrypted
/// pages stay encrypted at the file level, so a byte-for-byte file copy is a
/// valid encrypted backup that opens with the same key. <c>VACUUM INTO</c>
/// would otherwise need an explicit <c>PRAGMA key</c> to be set on the new
/// file before the copy, complicating the path. We checkpoint the WAL before
/// copying so the snapshot is self-contained.
/// </summary>
public sealed class DatabaseBackupService
{
    private const string BackupFilePrefix = "pos_";
    private const string BackupFileExtension = ".db";
    private const string BackupTimestampFormat = "yyyyMMddHHmmss";

    public string BackupFolder { get; }

    public DatabaseBackupService(string backupFolder)
    {
        if (string.IsNullOrWhiteSpace(backupFolder))
        {
            throw new ArgumentException("Backup folder is required.", nameof(backupFolder));
        }

        BackupFolder = backupFolder;
        Directory.CreateDirectory(BackupFolder);
    }

    /// <summary>
    /// Runs <c>PRAGMA integrity_check</c> against <paramref name="databasePath"/>.
    /// Returns <see cref="IntegrityCheckResult.Ok"/> only when SQLite reports the
    /// canonical "ok" string. Any other value (or an exception) is treated as
    /// corruption so the recovery path takes over.
    /// </summary>
    public IntegrityCheckResult VerifyIntegrity(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            return new IntegrityCheckResult(false, "database file does not exist");
        }

        try
        {
            using var connection = DatabaseEncryptionService.OpenCompatibleConnection(
                databasePath,
                SqliteOpenMode.ReadWriteCreate);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new IntegrityCheckResult(false, "integrity_check returned no rows");
            }

            var firstRow = reader.GetString(0);
            return string.Equals(firstRow, "ok", StringComparison.Ordinal)
                ? IntegrityCheckResult.Ok
                : new IntegrityCheckResult(false, firstRow);
        }
        catch (Exception ex)
        {
            return new IntegrityCheckResult(false, $"integrity_check threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a timestamped copy of <paramref name="databasePath"/> in
    /// <see cref="BackupFolder"/>. Returns the new file path. Throws when the
    /// source does not exist, or when the file copy fails.
    /// </summary>
    public string CreateBackup(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("Cannot back up: database file does not exist.", databasePath);
        }

        // Checkpoint the WAL into the main DB file so the copy contains the
        // committed state. TRUNCATE empties the WAL and shrinks the .wal file
        // to zero bytes which is what we want before a copy.
        using (var conn = DatabaseEncryptionService.OpenCompatibleConnection(
            databasePath,
            SqliteOpenMode.ReadWriteCreate))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }

        // Drop pooled connections so the next File.Copy is not blocked by the
        // Win32 share-lock on the .db / .db-shm / .db-wal files.
        SqliteConnection.ClearAllPools();

        var timestamp = DateTime.UtcNow.ToString(BackupTimestampFormat, CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(BackupFolder, $"{BackupFilePrefix}{timestamp}{BackupFileExtension}");

        // If a backup with the same second-resolution timestamp already exists
        // (the operator clicked "Force backup" twice), append a uniqueness
        // suffix rather than overwrite — backups are append-only.
        if (File.Exists(backupPath))
        {
            backupPath = Path.Combine(
                BackupFolder,
                $"{BackupFilePrefix}{timestamp}_{Guid.NewGuid():N}{BackupFileExtension}");
        }

        File.Copy(databasePath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>
    /// Returns the most recent backup file path, or null when no backups exist.
    /// Sort is lexical; the timestamp format guarantees lexical = chronological.
    /// </summary>
    public string? FindLatestBackup()
    {
        if (!Directory.Exists(BackupFolder))
        {
            return null;
        }

        var files = Directory.GetFiles(BackupFolder, $"{BackupFilePrefix}*{BackupFileExtension}");
        if (files.Length == 0)
        {
            return null;
        }

        Array.Sort(files, StringComparer.Ordinal);
        return files[^1];
    }

    /// <summary>
    /// Returns the timestamp encoded in the most recent backup's file name, or
    /// null when none exist. Used to decide whether a fresh daily backup is due.
    /// </summary>
    public DateTime? GetLatestBackupTimestampUtc()
    {
        var latest = FindLatestBackup();
        if (latest is null)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(latest);
        if (!name.StartsWith(BackupFilePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var timestampPortion = name[BackupFilePrefix.Length..];
        // Strip any uniqueness suffix (e.g. _abcdef) added when two backups
        // collided in the same second.
        var underscoreIndex = timestampPortion.IndexOf('_');
        if (underscoreIndex >= 0)
        {
            timestampPortion = timestampPortion[..underscoreIndex];
        }

        if (DateTime.TryParseExact(
            timestampPortion,
            BackupTimestampFormat,
            CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }

    /// <summary>
    /// Best-effort restore from the most recent backup. Returns a result
    /// describing what happened. Does NOT throw — corruption recovery should
    /// fall through to a "no backup available" UI rather than crash startup.
    /// </summary>
    public RestoreResult TryRestoreFromLatestBackup(string databasePath)
    {
        var backup = FindLatestBackup();
        if (backup is null)
        {
            return RestoreResult.NoBackupAvailable;
        }

        try
        {
            // Drop pooled connections so we can replace the file.
            SqliteConnection.ClearAllPools();

            // Move the corrupt file out of the way before overwriting so an
            // operator can hand it to support if needed. Best-effort — if it
            // fails we still attempt the restore.
            try
            {
                if (File.Exists(databasePath))
                {
                    var corruptName = $"{databasePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
                    File.Move(databasePath, corruptName);
                }
            }
            catch (IOException) { /* best-effort */ }
            catch (UnauthorizedAccessException) { /* best-effort */ }

            File.Copy(backup, databasePath, overwrite: true);

            // Also remove leftover -wal / -shm files from the corrupt main DB
            // so the restored copy is opened fresh.
            TryDelete(databasePath + "-wal");
            TryDelete(databasePath + "-shm");

            return new RestoreResult(true, backup, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new RestoreResult(false, backup, ex.Message);
        }
    }

    /// <summary>
    /// Keeps only the <paramref name="keepCount"/> most recent backups; deletes
    /// older ones. Best-effort — file-locking errors are swallowed.
    /// </summary>
    public void TrimBackups(int keepCount)
    {
        if (keepCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keepCount), "Must keep at least one backup.");
        }

        if (!Directory.Exists(BackupFolder))
        {
            return;
        }

        var files = Directory.GetFiles(BackupFolder, $"{BackupFilePrefix}*{BackupFileExtension}");
        if (files.Length <= keepCount)
        {
            return;
        }

        Array.Sort(files, StringComparer.Ordinal);
        var toDelete = files.Length - keepCount;
        for (var i = 0; i < toDelete; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch (IOException) { /* best-effort */ }
            catch (UnauthorizedAccessException) { /* best-effort */ }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}

/// <summary>Outcome of <see cref="DatabaseBackupService.VerifyIntegrity"/>.</summary>
public readonly record struct IntegrityCheckResult(bool IsOk, string Detail)
{
    public static IntegrityCheckResult Ok => new(true, "ok");
}

/// <summary>Outcome of <see cref="DatabaseBackupService.TryRestoreFromLatestBackup"/>.</summary>
public sealed record RestoreResult(bool Succeeded, string? BackupPath, string? ErrorMessage)
{
    public static RestoreResult NoBackupAvailable { get; } =
        new(false, null, "no backup available");
}
