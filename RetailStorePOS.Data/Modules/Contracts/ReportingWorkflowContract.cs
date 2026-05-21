namespace RetailStorePOS.Data.Modules.Contracts;

public static class ReportingWorkflowContract
{
    public static readonly ModuleContract DashboardReadModelQuery = new()
    {
        ContractKey = "reporting.dashboard.read-model",
        OwnerModuleKey = "Reporting",
        ContractType = ModuleContractType.Query,
        Purpose = "Navigate and compose the dashboard reporting surface.",
        Consumers = ["Reports UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract ReceiptHistoryQuery = new()
    {
        ContractKey = "reporting.receipts.read-model",
        OwnerModuleKey = "Reporting",
        ContractType = ModuleContractType.Query,
        Purpose = "Navigate and compose receipt history reporting views.",
        Consumers = ["Reports UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract SalesReadModelQuery = new()
    {
        ContractKey = "reporting.sales.read-model",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Query,
        Purpose = "Read sales summaries and receipt metrics for reporting.",
        Consumers = ["Reporting"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract InventoryReadModelQuery = new()
    {
        ContractKey = "reporting.inventory.read-model",
        OwnerModuleKey = "Inventory",
        ContractType = ModuleContractType.Query,
        Purpose = "Read stock health and catalog availability snapshots for reporting.",
        Consumers = ["Reporting"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract ReadinessAutomationContract = new()
    {
        ContractKey = "reporting.readiness.automation",
        OwnerModuleKey = "Reporting",
        ContractType = ModuleContractType.Command,
        Purpose = "Execute automated readiness verification against isolated datasets.",
        Consumers = ["Reports UI", "Startup"],
        OfflineAllowed = true
    };

    public static readonly WorkflowBoundary ReportingRefreshBoundary = new()
    {
        WorkflowKey = "reporting.refresh",
        CoordinatorModuleKey = "Reporting",
        ParticipatingModules = ["Sales", "Inventory", "Tax", "Telemetry"],
        OfflineCritical = false,
        WriteSequence =
        [
            "Reporting navigates through reporting.dashboard.read-model or reporting.receipts.read-model.",
            "Reporting reads sales summaries through reporting.sales.read-model.",
            "Reporting reads inventory health through reporting.inventory.read-model.",
            "Reporting stays read-only for source-of-truth business data."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        DashboardReadModelQuery,
        ReceiptHistoryQuery,
        SalesReadModelQuery,
        InventoryReadModelQuery,
        ReadinessAutomationContract
    ];

    public static T ResolveSalesReadModel<T>(T participant) where T : class => RequireParticipant(SalesReadModelQuery.ContractKey, participant);
    public static T ResolveInventoryReadModel<T>(T participant) where T : class => RequireParticipant(InventoryReadModelQuery.ContractKey, participant);

    private static T RequireParticipant<T>(string contractKey, T participant) where T : class
    {
        ArgumentNullException.ThrowIfNull(participant, contractKey);
        return participant;
    }
}
