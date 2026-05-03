namespace RetailStorePOS.Data.Modules.Contracts;

public static class SyncWorkflowContract
{
    public static readonly OwnedDataAsset SyncOperationalState = new()
    {
        AssetKey = "sync.operational-state",
        AssetType = OwnedDataAssetType.OperationalState,
        OwnerModuleKey = "Sync",
        PrimaryConsumers = ["Sync"],
        Notes = "Optional cloud synchronization cursors, retries, and checkpoints."
    };

    public static readonly ModuleContract SyncOutboxConsumptionQuery = new()
    {
        ContractKey = "sync.owner-exports",
        OwnerModuleKey = "Sync",
        ContractType = ModuleContractType.Query,
        Purpose = "Consume owner-published exports and outbox entries without taking ownership of business data.",
        Consumers = ["Sync"],
        OfflineAllowed = false
    };

    public static readonly ModuleContract SyncOperationalWriteCommand = new()
    {
        ContractKey = "sync.operational-write",
        OwnerModuleKey = "Sync",
        ContractType = ModuleContractType.Command,
        Purpose = "Persist sync-only operational state.",
        Consumers = ["Sync"],
        OfflineAllowed = false
    };

    public static readonly WorkflowBoundary SyncPushBoundary = new()
    {
        WorkflowKey = "sync.push",
        CoordinatorModuleKey = "Sync",
        ParticipatingModules = ["Sales", "Products", "Inventory", "Users/Auth", "Tax", "Telemetry", "Settings"],
        OfflineCritical = false,
        WriteSequence =
        [
            "Sync reads owner-published exports through sync.owner-exports.",
            "Sync writes only sync-owned operational state through sync.operational-write.",
            "Sync never becomes the owner of local source-of-truth business data."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        SyncOutboxConsumptionQuery,
        SyncOperationalWriteCommand
    ];
}
