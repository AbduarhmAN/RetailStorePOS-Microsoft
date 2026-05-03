namespace RetailStorePOS.Data;

public static class AppDataPaths
{
    private const string AppFolderName = "RetailStorePOS";
    private const string LegacyAppFolderName = "RetailStorePos";
    private static readonly Lazy<string> RootFolder = new(ResolveRootFolder, true);

    public static string GetRootFolder()
    {
        return RootFolder.Value;
    }

    public static string Combine(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var parts = new List<string>(segments.Length + 1) { GetRootFolder() };
        parts.AddRange(segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        return Path.Combine(parts.ToArray());
    }

    public static string GetLegacyRootFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyAppFolderName);
    }

    public static string CombineLegacy(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var parts = new List<string>(segments.Length + 1) { GetLegacyRootFolder() };
        parts.AddRange(segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        return Path.Combine(parts.ToArray());
    }

    private static string ResolveRootFolder()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath(),
            AppContext.BaseDirectory
        };

        foreach (var basePath in candidates)
        {
            var candidate = Path.Combine(basePath, AppFolderName);
            if (TryPrepare(candidate))
            {
                return candidate;
            }
        }

        var fallback = Path.Combine(Path.GetTempPath(), AppFolderName);
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool TryPrepare(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
