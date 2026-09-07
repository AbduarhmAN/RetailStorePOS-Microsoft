namespace RetailStorePOS.Data.Modules.Reporting;

public class DashboardSnapshotStore
{
    public void SaveSnapshot(DashboardSnapshot snapshot)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            snapshot,
            ReportingFileJsonContext.Default.DashboardSnapshot);
        var path = ReadinessPaths.GetSnapshotPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, json);
    }

    public DashboardSnapshot? LoadSnapshot()
    {
        var path = ReadinessPaths.GetSnapshotPath();
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize(
            json,
            ReportingFileJsonContext.Default.DashboardSnapshot);
    }
}
