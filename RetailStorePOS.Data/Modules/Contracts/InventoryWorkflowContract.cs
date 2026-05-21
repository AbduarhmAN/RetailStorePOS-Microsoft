using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.Data.Modules.Tax;

namespace RetailStorePOS.Data.Modules.Contracts;

public static class InventoryWorkflowContract
{
    public static readonly WorkflowBoundary ProductMaintenanceBoundary = new()
    {
        WorkflowKey = "Inventory.ProductMaintenance",
        CoordinatorModuleKey = "Products",
        ParticipatingModules = ["Products", "Inventory", "Tax"],
        OfflineCritical = true,
        WriteSequence = ["Products", "Inventory"]
    };

    public static readonly ModuleContract InventoryStockAdjustmentCommand = new()
    {
        ContractKey = "Inventory.StockAdjustment",
        OwnerModuleKey = "Inventory",
        ContractType = ModuleContractType.Command,
        Purpose = "Adjust stock levels on sale or manual correction",
        OfflineAllowed = true
    };

    public static IReadOnlyList<ModuleContract> Contracts => [InventoryStockAdjustmentCommand];

    public static ProductRepository ResolveProductCatalog(ProductRepository instance) => instance;
    public static T ResolveCatalogSearch<T>(T instance) where T : class => instance;
    public static TaxCategoryRepository ResolveTaxCategoryReference(TaxCategoryRepository instance) => instance;
    public static TaxRuleRepository ResolveTaxRuleReference(TaxRuleRepository instance) => instance;
    public static TaxGroupRepository ResolveTaxGroupReference(TaxGroupRepository instance) => instance;
}
