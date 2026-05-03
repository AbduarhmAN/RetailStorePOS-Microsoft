namespace RetailStorePOS.Data.Modules.Reporting;

public class ScenarioResult
{
    public string RunId { get; set; } = string.Empty;
    public string ScenarioKey { get; set; } = string.Empty;
    public ScenarioStatus Status { get; set; }
    public ScenarioSeverity Severity { get; set; }
    public int DurationMs { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public List<string> ArtifactPaths { get; set; } = new();
}

public enum ScenarioStatus
{
    Passed,
    Failed,
    Skipped
}

public enum ScenarioSeverity
{
    Blocking,
    Observation
}
