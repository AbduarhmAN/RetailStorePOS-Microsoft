namespace RetailStorePOS.Data.Modules.Reporting;

public class ReadinessRun
{
    public string RunId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string DatasetProfileKey { get; set; } = string.Empty;
    public ReadinessTriggerSource TriggerSource { get; set; }
    public ReadinessOverallStatus OverallStatus { get; set; }
    public int BlockingFailureCount { get; set; }
    public int ObservationCount { get; set; }
    public string ReportPath { get; set; } = string.Empty;
}

public enum ReadinessTriggerSource
{
    Operator,
    StartupHook,
    Diagnostics
}

public enum ReadinessOverallStatus
{
    Passed,
    Failed,
    CompletedWithObservations
}
