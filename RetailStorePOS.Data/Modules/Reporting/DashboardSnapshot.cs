namespace RetailStorePOS.Data.Modules.Reporting;

public class DashboardSnapshot
{
    public string SnapshotKey { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public string DatasetFingerprint { get; set; } = string.Empty;
    public object? MetricsPayload { get; set; }
    public string? SourceRunId { get; set; }
    public bool IsStale { get; set; }
}
