namespace RetailStorePOS.Data.Modules.Contracts;

public static class CheckoutWorkflowContract
{
    public static readonly ModuleContract ProductLookupQuery = new()
    {
        ContractKey = "checkout.products.lookup",
        OwnerModuleKey = "Products",
        ContractType = ModuleContractType.Query,
        Purpose = "Resolve product catalog data for checkout line selection.",
        Consumers = ["Sales", "Checkout UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract TaxResolutionQuery = new()
    {
        ContractKey = "checkout.tax.resolve",
        OwnerModuleKey = "Tax",
        ContractType = ModuleContractType.Query,
        Purpose = "Resolve tax category and tax group data for checkout pricing.",
        Consumers = ["Sales", "Checkout UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract SaleCompletionCommand = new()
    {
        ContractKey = "checkout.sales.complete",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Command,
        Purpose = "Persist the completed sale and hand off owner-approved stock writes.",
        Consumers = ["Checkout UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract RegisterSessionQuery = new()
    {
        ContractKey = "checkout.register.session-query",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Query,
        Purpose = "Query active register session status to gate checkout access.",
        Consumers = ["Checkout UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract RegisterOpenCommand = new()
    {
        ContractKey = "checkout.register.open",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Command,
        Purpose = "Initialize a new register session with opening balance.",
        Consumers = ["Checkout UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract RegisterCloseSummaryQuery = new()
    {
        ContractKey = "checkout.register.close-summary",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Query,
        Purpose = "Return the active-session Cash and Card totals needed by the close-register dialog.",
        Consumers = ["Main shell UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract RegisterCloseCommand = new()
    {
        ContractKey = "checkout.register.close",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Command,
        Purpose = "Persist the close timestamp, counted cash, and closing note for the active register session.",
        Consumers = ["Main shell UI"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract RegisterCashAdjustmentCommand = new()
    {
        ContractKey = "checkout.register.cash-adjustment",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Command,
        Purpose = "Persist a Cash In or Cash Out adjustment for the active register session.",
        Consumers = ["Main shell UI"],
        OfflineAllowed = true
    };

    public static readonly WorkflowBoundary CompleteSaleBoundary = new()
    {
        WorkflowKey = "checkout.complete-sale",
        CoordinatorModuleKey = "Sales",
        ParticipatingModules = ["Products", "Inventory", "Tax", "Users/Auth", "Telemetry"],
        OfflineCritical = true,
        WriteSequence =
        [
            "Sales reads product selection through checkout.products.lookup.",
            "Sales resolves tax data through checkout.tax.resolve.",
            "Sales persists the receipt through checkout.sales.complete.",
            "Inventory applies stock movement through inventory.stock-adjust.",
            "Telemetry records optional lifecycle events through telemetry.lifecycle-event."
        ]
    };

    public static readonly WorkflowBoundary RegisterCloseBoundary = new()
    {
        WorkflowKey = "checkout.register-close",
        CoordinatorModuleKey = "Sales",
        ParticipatingModules = ["Users/Auth"],
        OfflineCritical = true,
        WriteSequence =
        [
            "Shell UI resolves the active session through checkout.register.session-query.",
            "Shell UI loads close totals through checkout.register.close-summary.",
            "Shell UI optionally records Cash In / Out adjustments through checkout.register.cash-adjustment.",
            "Sales persists the register close through checkout.register.close.",
            "UI triggers local logout through Users/Auth only after the Sales write succeeds."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        ProductLookupQuery,
        TaxResolutionQuery,
        SaleCompletionCommand,
        RegisterSessionQuery,
        RegisterOpenCommand,
        RegisterCloseSummaryQuery,
        RegisterCloseCommand,
        RegisterCashAdjustmentCommand
    ];

    public static T ResolveCatalogLookup<T>(T participant) where T : class => RequireParticipant(ProductLookupQuery.ContractKey, participant);
    public static T ResolveProductOwner<T>(T participant) where T : class => RequireParticipant(ProductLookupQuery.ContractKey, participant);
    public static T ResolveTaxCategoryReader<T>(T participant) where T : class => RequireParticipant(TaxResolutionQuery.ContractKey, participant);
    public static T ResolveTaxGroupReader<T>(T participant) where T : class => RequireParticipant(TaxResolutionQuery.ContractKey, participant);
    public static T ResolveSalesCoordinator<T>(T participant) where T : class => RequireParticipant(SaleCompletionCommand.ContractKey, participant);
    public static T ResolveRegisterSessionManager<T>(T participant) where T : class => RequireParticipant(RegisterSessionQuery.ContractKey, participant);

    private static T RequireParticipant<T>(string contractKey, T participant) where T : class
    {
        ArgumentNullException.ThrowIfNull(participant, contractKey);
        return participant;
    }
}
