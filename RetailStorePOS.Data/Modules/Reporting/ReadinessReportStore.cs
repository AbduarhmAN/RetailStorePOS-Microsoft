namespace RetailStorePOS.Data.Modules.Reporting;

using System.Text.Json;

public class ReadinessReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void SaveReport(ReadinessRun run, List<ScenarioResult> results)
    {
        var reportData = new ReadinessReportData
        {
            Metadata = run,
            Results = results
        };

        var json = JsonSerializer.Serialize(reportData, JsonOptions);
        var path = run.ReportPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, json);
    }

    public ReadinessReportData? LoadReport(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ReadinessReportData>(json);
    }
}

public class ReadinessReportData
{
    public ReadinessRun Metadata { get; set; } = new();
    public List<ScenarioResult> Results { get; set; } = new();
}
