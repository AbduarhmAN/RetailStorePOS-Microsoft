namespace RetailStorePOS.Data.Modules.Reporting;

using System.Text.Json;

public class DashboardSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void SaveSnapshot(DashboardSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
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
        return JsonSerializer.Deserialize<DashboardSnapshot>(json);
    }
}
