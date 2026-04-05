namespace RetailStorePOS.Data;

public static class DatabasePaths
{
    public static string GetDatabasePath(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("App name is required.", nameof(appName));
        }

        return AppDataPaths.Combine("Database", appName, "pos.db");
    }
}
