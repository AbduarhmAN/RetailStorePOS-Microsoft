using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.Data.Modules.Sales;
using RetailStorePOS.Data.Modules.Tax;

namespace RetailStorePOS.Data.Modules.Contracts;

public static class CheckoutWorkflowContract
{
    public static readonly WorkflowBoundary CompleteSaleBoundary = new()
    {
        WorkflowKey = "Checkout.CompleteSale",
        CoordinatorModuleKey = "Sales",
        ParticipatingModules = ["Sales", "Products", "Inventory"],
        OfflineCritical = true,
        WriteSequence = ["Sales", "Inventory"]
    };

    public static readonly ModuleContract SaleCompletionCommand = new()
    {
        ContractKey = "Checkout.SaleCompletion",
        OwnerModuleKey = "Sales",
        ContractType = ModuleContractType.Command,
        Purpose = "Persist completed sale and decrement inventory",
        OfflineAllowed = true
    };

    public static readonly ModuleContract ProductLookupQuery = new()
    {
        ContractKey = "Checkout.ProductLookup",
        OwnerModuleKey = "Products",
        ContractType = ModuleContractType.Query,
        Purpose = "Lookup product by barcode or search term",
        OfflineAllowed = true
    };

    public static IReadOnlyList<ModuleContract> Contracts => [SaleCompletionCommand, ProductLookupQuery];

    public static SaleRepository ResolveSalesCoordinator(SaleRepository instance) => instance;
    public static T ResolveCatalogLookup<T>(T instance) where T : class => instance;
    public static ProductRepository ResolveProductOwner(ProductRepository instance) => instance;
    public static TaxCategoryRepository ResolveTaxCategoryReader(TaxCategoryRepository instance) => instance;
    public static TaxGroupRepository ResolveTaxGroupReader(TaxGroupRepository instance) => instance;
    public static T ResolveRegisterSessionManager<T>(T instance) where T : class => instance;
}
