namespace RetailStorePOS.Data.Modules.Reporting;

public sealed class ReadinessRun
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public string StartedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string? CompletedAt { get; set; }
    public int TotalScenarios { get; set; }
    public int PassedScenarios { get; set; }
    public int FailedScenarios { get; set; }
    public int BlockingFailureCount { get; set; }
    public int ObservationCount { get; set; }
    public string OverallStatus { get; set; } = "unknown";
    public string? DatasetProfileKey { get; set; }
    public string ReportPath { get; set; } = AppDataPaths.Combine("Readiness", "Reports", $"readiness_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
}
