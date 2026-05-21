namespace RetailStorePOS.Data.Modules.Reporting;

public static class ReadinessPaths
{
    private const string ReadinessFolder = "Readiness";
    private const string WorkspacesFolder = "Workspaces";
    private const string ReportsFolder = "Reports";
    private const string SnapshotsFolder = "Snapshots";

    public static string GetReadinessRoot() => AppDataPaths.Combine(ReadinessFolder);
    public static string GetWorkspacesRoot() => AppDataPaths.Combine(ReadinessFolder, WorkspacesFolder);
    public static string GetReportsRoot() => AppDataPaths.Combine(ReadinessFolder, ReportsFolder);
    public static string GetSnapshotsRoot() => AppDataPaths.Combine(ReadinessFolder, SnapshotsFolder);

    public static string GetReportPath(string runId) => Path.Combine(GetReportsRoot(), $"report_{runId}.json");
    public static string GetSnapshotPath() => Path.Combine(GetSnapshotsRoot(), "dashboard_snapshot.json");
}
