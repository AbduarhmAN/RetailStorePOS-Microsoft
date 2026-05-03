namespace RetailStorePOS.Data.Modules.Reporting;

public static class ReadinessScenarioKeys
{
    public const string StartupBootstrap = "startup.bootstrap";
    public const string DatabaseLocalReadWrite = "database.local-read-write";
    public const string AuthLogin = "auth.login";
    public const string CatalogLookup = "catalog.lookup";
    public const string CheckoutCompleteSale = "checkout.complete-sale";
    public const string TaxCalculate = "tax.calculate";
    public const string ProductsExportValid = "products.export.valid";
    public const string ProductsImportValidRoundtrip = "products.import.valid-roundtrip";
    public const string ProductsImportThumbnails = "products.import.thumbnails";
    public const string ProductsImportInvalidSafeFailure = "products.import.invalid-safe-failure";
}
