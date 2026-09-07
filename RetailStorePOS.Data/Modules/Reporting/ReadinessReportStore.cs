namespace RetailStorePOS.Data.Modules.Reporting;

public class ReadinessReportStore
{
    public void SaveReport(ReadinessRun run, List<ScenarioResult> results)
    {
        var reportData = new ReadinessReportData
        {
            Metadata = run,
            Results = results
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            reportData,
            ReportingFileJsonContext.Default.ReadinessReportData);
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
        return System.Text.Json.JsonSerializer.Deserialize(
            json,
            ReportingFileJsonContext.Default.ReadinessReportData);
    }
}

public class ReadinessReportData
{
    public ReadinessRun Metadata { get; set; } = new();
    public List<ScenarioResult> Results { get; set; } = new();
}
