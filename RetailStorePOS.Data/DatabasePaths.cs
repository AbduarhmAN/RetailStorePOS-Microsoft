namespace RetailStorePOS.Data;

public static class DatabasePaths
{
    private const string DatabaseFolderName = "Database";
    private const string DatabaseFileName = "pos.db";

    public static string GetDatabasePath(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("App name is required.", nameof(appName));
        }

        var normalizedAppName = NormalizePathSegment(appName, nameof(appName));
        return NormalizeLocalPath(AppDataPaths.Combine(DatabaseFolderName, normalizedAppName, DatabaseFileName));
    }

    public static string NormalizeLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Local database path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database path must remain local to preserve one-dataset desktop operation.");
        }

        return fullPath;
    }

    private static string NormalizePathSegment(string value, string paramName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Path segment is required.", paramName);
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("App name contains invalid path characters.", paramName);
        }

        return trimmed;
    }
}
