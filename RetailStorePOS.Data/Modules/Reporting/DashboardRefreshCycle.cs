namespace RetailStorePOS.Data.Modules.Reporting;

public class DashboardRefreshCycle
{
    public string CycleId { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? SnapshotShownAtUtc { get; set; }
    public DateTime? FreshDataCompletedAtUtc { get; set; }
    public DashboardRefreshStatus Status { get; set; }
    public string? FailureMessage { get; set; }
    public bool ReplacedSnapshot { get; set; }
}

public enum DashboardRefreshStatus
{
    Loading,
    Succeeded,
    Failed,
    SucceededWithStaleFallbackCleared
}
