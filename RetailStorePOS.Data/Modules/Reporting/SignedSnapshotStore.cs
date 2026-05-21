using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Generic signed-snapshot store shared by the advanced-analytics pages.
///
/// Implementation notes:
///   - Atomic write: temp file -&gt; replace, so a crash mid-write cannot
///     leave a half-written snapshot.
///   - HMAC-SHA256 signing via <see cref="SnapshotSigningService"/>.
///   - Schema versioning: snapshots written by an older app version are
///     rejected (forces fresh capture instead of misinterpreting fields).
///   - Max-age check: <see cref="LoadSnapshotIfFresh"/> filters out
///     snapshots older than a caller-supplied window.
///
/// File layout on disk (under <c>{AppData}/readiness/snapshots/</c>):
///
///     {prefix}{periodKey}.json   - JSON body
///     {prefix}{periodKey}.sig    - 32-byte HMAC of the JSON body
/// </summary>
public abstract class SignedSnapshotStore<TSnapshot>
    where TSnapshot : class, ISignedSnapshot, new()
{
    private const string JsonExtension = ".json";
    private const string SignatureExtension = ".sig";

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SnapshotSigningService _signing;

    protected SignedSnapshotStore(SnapshotSigningService signing)
    {
        _signing = signing ?? throw new ArgumentNullException(nameof(signing));
    }

    /// <summary>File-name prefix that identifies this store's payload kind.</summary>
    protected abstract string FilePrefix { get; }

    /// <summary>The schema version supported by the current app build.</summary>
    protected abstract int CurrentSchemaVersion { get; }

    /// <summary>
    /// Persists a snapshot atomically and writes its detached HMAC signature.
    /// Old snapshot for the same period is replaced.
    /// </summary>
    public void SaveSnapshot(TSnapshot snapshot)
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
    public TSnapshot? LoadSnapshot(string periodKey)
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

            var snapshot = JsonSerializer.Deserialize<TSnapshot>(jsonBytes, JsonOptions);
            if (snapshot is null) return null;
            if (snapshot.SchemaVersion != CurrentSchemaVersion) return null;
            return snapshot;
        }
        catch (Exception)
        {
            // Any deserialization / IO problem -> treat as missing so the
            // caller falls back to a live query and overwrites with a clean
            // snapshot.
            return null;
        }
    }

    /// <summary>
    /// Same as <see cref="LoadSnapshot"/> but returns null when the snapshot
    /// is older than <paramref name="maxAge"/>.
    /// </summary>
    public TSnapshot? LoadSnapshotIfFresh(string periodKey, TimeSpan maxAge)
    {
        var snapshot = LoadSnapshot(periodKey);
        if (snapshot is null) return null;

        var age = DateTime.UtcNow - snapshot.CapturedAtUtc;
        if (age < TimeSpan.Zero || age > maxAge) return null;
        return snapshot;
    }

    /// <summary>
    /// Removes the snapshot files for <paramref name="periodKey"/>.
    /// </summary>
    public void Invalidate(string periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey)) return;

        var safeKey = SanitizeKey(periodKey);
        TryDelete(GetJsonPath(safeKey));
        TryDelete(GetSignaturePath(safeKey));
    }

    /// <summary>
    /// Removes all snapshot files for this store's <see cref="FilePrefix"/>.
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

    private string GetJsonPath(string safeKey)
    {
        return Path.Combine(ReadinessPaths.GetSnapshotsRoot(), FilePrefix + safeKey + JsonExtension);
    }

    private string GetSignaturePath(string safeKey)
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
            // Best effort.
        }
    }

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
