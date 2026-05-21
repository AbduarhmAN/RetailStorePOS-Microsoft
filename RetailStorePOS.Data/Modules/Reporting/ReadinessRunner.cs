namespace RetailStorePOS.Data.Modules.Reporting;

public sealed class ReadinessRunner
{
    private readonly ReadinessWorkspaceManager _workspaceManager;
    private readonly ReadinessReportStore _reportStore;
    private readonly Dictionary<string, Func<DatasetProfile, string, Task<ScenarioResult>>> _handlers = new();

    public ReadinessRunner(ReadinessWorkspaceManager workspaceManager, ReadinessReportStore reportStore)
    {
        _workspaceManager = workspaceManager;
        _reportStore = reportStore;
    }

    public void RegisterHandler(string scenarioKey, Func<DatasetProfile, string, Task<ScenarioResult>> handler)
    {
        _handlers[scenarioKey] = handler;
    }

    public async Task<ReadinessRun> RunFullPassAsync(string? profileKey = null)
    {
        var scenarios = ReadinessScenarioCatalog.GetAllScenarios();
        var profiles = ReadinessScenarioCatalog.GetAllProfiles();
        if (!string.IsNullOrEmpty(profileKey))
            profiles = profiles.Where(p => p.ProfileKey == profileKey).ToList();
        var run = new ReadinessRun { TotalScenarios = scenarios.Count };
        var results = new List<ScenarioResult>();

        foreach (var profile in profiles)
        {
            _workspaceManager.PrepareWorkspace(profile);

            foreach (var scenario in scenarios.Where(s => s.DatasetProfiles.Contains(profile.ProfileKey)))
            {
                if (_handlers.TryGetValue(scenario.ScenarioKey, out var handler))
                {
                    var result = await handler(profile, run.RunId);
                    result.RunId = run.RunId;
                    result.ScenarioKey = scenario.ScenarioKey;
                    result.Severity = scenario.BlockingOnFail ? ScenarioSeverity.Blocking : ScenarioSeverity.Observation;
                    results.Add(result);
                }
            }

            _workspaceManager.CleanupWorkspace(profile);
        }

        run.PassedScenarios = results.Count(r => r.Status == ScenarioStatus.Passed);
        run.FailedScenarios = results.Count(r => r.Status == ScenarioStatus.Failed);
        run.CompletedAt = DateTime.UtcNow.ToString("o");

        _reportStore.SaveReport(run, results);
        return run;
    }
}
