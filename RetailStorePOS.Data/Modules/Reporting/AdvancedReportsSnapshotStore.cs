using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Reads and writes <see cref="AdvancedReportsSnapshot"/> files for the Pro
/// Revenue Dashboard. Differs from the regular <see cref="DashboardSnapshotStore"/>:
///
///   - Atomic write: temp file -&gt; replace, so a crash mid-write cannot
///     leave a half-written snapshot.
///   - HMAC-SHA256 signing: tamper detection. A snapshot whose signature
///     fails to verify is treated as missing.
///   - Schema versioning: snapshots written by an older app version are
///     rejected (forces fresh capture instead of misinterpreting fields).
///   - Max-age check: a separate <see cref="LoadSnapshotIfFresh"/> overload
///     filters out snapshots older than a caller-supplied window.
///
/// File layout on disk (under <c>{AppData}/readiness/snapshots/</c>):
///
///     advanced-reports-{periodKey}.json   - JSON body
///     advanced-reports-{periodKey}.sig    - 32-byte HMAC of the JSON body
/// </summary>
public sealed class AdvancedReportsSnapshotStore
{
    private const string FilePrefix = "advanced-reports-";
    private const string JsonExtension = ".json";
    private const string SignatureExtension = ".sig";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SnapshotSigningService _signing;

    public AdvancedReportsSnapshotStore(SnapshotSigningService signing)
    {
        _signing = signing ?? throw new ArgumentNullException(nameof(signing));
    }

    /// <summary>
    /// Persists a snapshot atomically and writes its detached HMAC signature.
    /// Old snapshot for the same period is replaced.
    /// </summary>
    public void SaveSnapshot(AdvancedReportsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var safeKey = SanitizeKey(snapshot.PeriodKey);
        var jsonPath = GetJsonPath(safeKey);
        var sigPath = GetSignaturePath(safeKey);
        EnsureDirectory(jsonPath);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var signature = _signing.Sign(jsonBytes);

        WriteAtomic(jsonPath, jsonBytes);
        WriteAtomic(sigPath, signature);
    }

    /// <summary>
    /// Loads a snapshot for <paramref name="periodKey"/> if its signature
    /// verifies and its schema version is current. Returns null otherwise.
    /// </summary>
    public AdvancedReportsSnapshot? LoadSnapshot(string periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey)) return null;

        var safeKey = SanitizeKey(periodKey);
        var jsonPath = GetJsonPath(safeKey);
        var sigPath = GetSignaturePath(safeKey);

        if (!File.Exists(jsonPath) || !File.Exists(sigPath))
        {
            return null;
        }

        try
        {
            var jsonBytes = File.ReadAllBytes(jsonPath);
            var signature = File.ReadAllBytes(sigPath);

            if (!_signing.Verify(jsonBytes, signature))
            {
                // Tampered or signed by a different install. Treat as missing.
                return null;
            }

            var snapshot = JsonSerializer.Deserialize<AdvancedReportsSnapshot>(jsonBytes, JsonOptions);
            if (snapshot is null) return null;
            if (snapshot.SchemaVersion != AdvancedReportsSnapshot.CurrentSchemaVersion) return null;
            return snapshot;
        }
        catch (Exception)
        {
            // Any deserialization / IO problem - treat as missing so the caller
            // falls back to a live query and overwrites with a clean snapshot.
            return null;
        }
    }

    /// <summary>
    /// Same as <see cref="LoadSnapshot"/> but returns null when the snapshot
    /// is older than <paramref name="maxAge"/>. Use to enforce a "stale-after"
    /// policy from the caller.
    /// </summary>
    public AdvancedReportsSnapshot? LoadSnapshotIfFresh(string periodKey, TimeSpan maxAge)
    {
        var snapshot = LoadSnapshot(periodKey);
        if (snapshot is null) return null;

        var age = DateTime.UtcNow - snapshot.CapturedAtUtc;
        if (age < TimeSpan.Zero || age > maxAge) return null;
        return snapshot;
    }

    /// <summary>
    /// Removes the snapshot files for <paramref name="periodKey"/>. Useful
    /// after a sale completes (invalidate-on-write).
    /// </summary>
    public void Invalidate(string periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey)) return;

        var safeKey = SanitizeKey(periodKey);
        TryDelete(GetJsonPath(safeKey));
        TryDelete(GetSignaturePath(safeKey));
    }

    /// <summary>
    /// Removes all advanced-reports snapshot files (any period).
    /// </summary>
    public void InvalidateAll()
    {
        var root = ReadinessPaths.GetSnapshotsRoot();
        if (!Directory.Exists(root)) return;

        foreach (var path in Directory.EnumerateFiles(root, FilePrefix + "*"))
        {
            TryDelete(path);
        }
    }

    private static string GetJsonPath(string safeKey)
    {
        return Path.Combine(ReadinessPaths.GetSnapshotsRoot(), FilePrefix + safeKey + JsonExtension);
    }

    private static string GetSignaturePath(string safeKey)
    {
        return Path.Combine(ReadinessPaths.GetSnapshotsRoot(), FilePrefix + safeKey + SignatureExtension);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Atomic write: writes to a temp file, then replaces the destination.
    /// Avoids leaving a half-written snapshot if the process is killed.
    /// </summary>
    private static void WriteAtomic(string path, byte[] content)
    {
        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, content);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort. A surviving stale file will fail signature
            // verification next time and be overwritten.
        }
    }

    /// <summary>
    /// Strips characters that are invalid in file names. Period keys come
    /// from the UI (Today, Last7Days, Custom) so this is a defensive step.
    /// </summary>
    private static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "_";
        var sb = new StringBuilder(key.Length);
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in key)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        return sb.ToString();
    }
}
