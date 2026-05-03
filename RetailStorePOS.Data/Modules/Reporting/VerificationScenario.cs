namespace RetailStorePOS.Data.Modules.Reporting;

public class VerificationScenario
{
    public string ScenarioKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ScenarioPriority Priority { get; set; }
    public bool BlockingOnFail { get; set; }
    public List<string> DatasetProfiles { get; set; } = new();
    public int ExecutionOrder { get; set; }
    public List<string> RequiredContracts { get; set; } = new();
}

public enum ScenarioPriority
{
    P1,
    P2
}
