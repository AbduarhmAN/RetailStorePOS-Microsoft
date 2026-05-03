namespace RetailStorePOS.Data.Modules.Contracts;

public static class InventoryWorkflowContract
{
    public static readonly ModuleContract ProductMaintenanceCommand = new()
    {
        ContractKey = "inventory.products.maintain",
        OwnerModuleKey = "Products",
        ContractType = ModuleContractType.Command,
        Purpose = "Maintain catalog data while keeping inventory-owned stock rules explicit.",
        Consumers = ["Products UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract InventoryStockAdjustmentCommand = new()
    {
        ContractKey = "inventory.stock-adjust",
        OwnerModuleKey = "Inventory",
        ContractType = ModuleContractType.Command,
        Purpose = "Apply stock movements through the Inventory owner path.",
        Consumers = ["Sales", "Products", "Reporting"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract TaxReferenceQuery = new()
    {
        ContractKey = "inventory.tax-reference",
        OwnerModuleKey = "Tax",
        ContractType = ModuleContractType.Query,
        Purpose = "Read tax reference data needed while maintaining products.",
        Consumers = ["Products"],
        OfflineAllowed = true
    };

    public static readonly WorkflowBoundary ProductMaintenanceBoundary = new()
    {
        WorkflowKey = "products.maintenance",
        CoordinatorModuleKey = "Products",
        ParticipatingModules = ["Inventory", "Tax", "Reporting"],
        OfflineCritical = false,
        WriteSequence =
        [
            "Products edits catalog data through inventory.products.maintain.",
            "Inventory-owned stock rules flow through inventory.stock-adjust.",
            "Tax reference data flows through inventory.tax-reference.",
            "Reporting consumes downstream outputs only."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        ProductMaintenanceCommand,
        InventoryStockAdjustmentCommand,
        TaxReferenceQuery
    ];

    public static T ResolveProductCatalog<T>(T participant) where T : class => RequireParticipant(ProductMaintenanceCommand.ContractKey, participant);
    public static T ResolveCatalogSearch<T>(T participant) where T : class => RequireParticipant(ProductMaintenanceCommand.ContractKey, participant);
    public static T ResolveTaxCategoryReference<T>(T participant) where T : class => RequireParticipant(TaxReferenceQuery.ContractKey, participant);
    public static T ResolveTaxRuleReference<T>(T participant) where T : class => RequireParticipant(TaxReferenceQuery.ContractKey, participant);
    public static T ResolveTaxGroupReference<T>(T participant) where T : class => RequireParticipant(TaxReferenceQuery.ContractKey, participant);

    private static T RequireParticipant<T>(string contractKey, T participant) where T : class
    {
        ArgumentNullException.ThrowIfNull(participant, contractKey);
        return participant;
    }
}
