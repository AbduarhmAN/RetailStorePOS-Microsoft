namespace RetailStorePOS.Data.Modules.Contracts;

public sealed record WorkflowBoundary
{
    public string WorkflowKey { get; init; } = string.Empty;
    public string CoordinatorModuleKey { get; init; } = string.Empty;
    public IReadOnlyList<string> ParticipatingModules { get; init; } = [];
    public bool OfflineCritical { get; init; }
    public IReadOnlyList<string> WriteSequence { get; init; } = [];
}
