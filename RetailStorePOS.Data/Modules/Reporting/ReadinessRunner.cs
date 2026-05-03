namespace RetailStorePOS.Data.Modules.Reporting;

using System.Diagnostics;
using System.Threading.Tasks;

public class ReadinessRunner
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

    public async Task<ReadinessRun> RunFullPassAsync(string profileKey, IProgress<int>? progress = null)
    {
        var profile = ReadinessScenarioCatalog.GetProfiles().FirstOrDefault(p => p.ProfileKey == profileKey);
        if (profile == null) throw new ArgumentException($"Unknown dataset profile: {profileKey}");

        var runId = Guid.NewGuid().ToString("N");
        var run = new ReadinessRun
        {
            RunId = runId,
            StartedAtUtc = DateTime.UtcNow,
            DatasetProfileKey = profileKey,
            TriggerSource = ReadinessTriggerSource.Operator,
            OverallStatus = ReadinessOverallStatus.Passed,
            ReportPath = ReadinessPaths.GetReportPath(runId)
        };

        _workspaceManager.PrepareWorkspace(profile);

        var scenarios = ReadinessScenarioCatalog.GetScenarios()
            .Where(s => s.DatasetProfiles.Contains(profileKey))
            .OrderBy(s => s.ExecutionOrder)
            .ToList();

        var results = new List<ScenarioResult>();

        foreach (var scenario in scenarios)
        {
            var result = await ExecuteScenarioAsync(run.RunId, scenario, profile);
            results.Add(result);

            if (result.Status == ScenarioStatus.Failed)
            {
                if (scenario.BlockingOnFail)
                {
                    run.BlockingFailureCount++;
                    run.OverallStatus = ReadinessOverallStatus.Failed;
                }
                else
                {
                    run.ObservationCount++;
                    if (run.OverallStatus == ReadinessOverallStatus.Passed)
                    {
                        run.OverallStatus = ReadinessOverallStatus.CompletedWithObservations;
                    }
                }
            }
            else if (result.Status == ScenarioStatus.Skipped)
            {
                run.ObservationCount++;
            }
        }

        run.CompletedAtUtc = DateTime.UtcNow;
        _reportStore.SaveReport(run, results);

        return run;
    }

    private async Task<ScenarioResult> ExecuteScenarioAsync(string runId, VerificationScenario scenario, DatasetProfile profile)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScenarioResult
        {
            RunId = runId,
            ScenarioKey = scenario.ScenarioKey,
            Severity = scenario.BlockingOnFail ? ScenarioSeverity.Blocking : ScenarioSeverity.Observation,
            Status = ScenarioStatus.Skipped,
            Summary = "Scenario logic placeholder in foundational runner shell."
        };

        // Logic for specific scenarios will be implemented in Phase 3 and Phase 4
        // by extending this runner or providing scenario-specific handlers.
        if (_handlers.TryGetValue(scenario.ScenarioKey, out var handler))
        {
            try
            {
                result = await handler(profile, runId);
                result.RunId = runId;
                result.ScenarioKey = scenario.ScenarioKey;
                result.Severity = scenario.BlockingOnFail ? ScenarioSeverity.Blocking : ScenarioSeverity.Observation;
            }
            catch (Exception ex)
            {
                result.Status = ScenarioStatus.Failed;
                result.Summary = $"Unhandled exception during scenario execution: {ex.Message}";
                result.FailureCode = "UNHANDLED_EXCEPTION";
            }
        }
        else
        {
            await Task.Yield();
        }
        
        stopwatch.Stop();
        result.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        return result;
    }
}
