namespace RetailStorePOS.Data.Modules.Contracts;

public static class SyncWorkflowContract
{
    public static readonly WorkflowBoundary SyncPushBoundary = new()
    {
        WorkflowKey = "Sync.Push",
        CoordinatorModuleKey = "Sync",
        ParticipatingModules = ["Sync", "Telemetry"],
        OfflineCritical = false,
        WriteSequence = ["Sync"]
    };

    public static readonly ModuleContract SyncPushCommand = new()
    {
        ContractKey = "Sync.Push",
        OwnerModuleKey = "Sync",
        ContractType = ModuleContractType.Command,
        Purpose = "Push local state to remote when connectivity is available",
        OfflineAllowed = false
    };

    public static IReadOnlyList<ModuleContract> Contracts => [SyncPushCommand];
}
