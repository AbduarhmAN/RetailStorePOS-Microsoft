namespace RetailStorePOS.Data.Modules.Reporting;

public static class ReadinessScenarioCatalog
{
    public static List<VerificationScenario> GetAllScenarios() => new()
    {
        new() { ScenarioKey = ReadinessScenarioKeys.StartupBootstrap, DisplayName = "Startup Bootstrap", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["minimal", "representative"], ExecutionOrder = 1 },
        new() { ScenarioKey = ReadinessScenarioKeys.DatabaseLocalReadWrite, DisplayName = "Database Local Read/Write", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["minimal", "representative"], ExecutionOrder = 2 },
        new() { ScenarioKey = ReadinessScenarioKeys.AuthLogin, DisplayName = "Auth Login", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["representative"], ExecutionOrder = 3 },
        new() { ScenarioKey = ReadinessScenarioKeys.CatalogLookup, DisplayName = "Catalog Lookup", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["representative"], ExecutionOrder = 4 },
        new() { ScenarioKey = ReadinessScenarioKeys.CheckoutCompleteSale, DisplayName = "Checkout Complete Sale", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["representative"], ExecutionOrder = 5 },
        new() { ScenarioKey = ReadinessScenarioKeys.TaxCalculate, DisplayName = "Tax Calculate", Priority = ScenarioPriority.P1, BlockingOnFail = true, DatasetProfiles = ["representative"], ExecutionOrder = 6 },
        new() { ScenarioKey = ReadinessScenarioKeys.ProductsExportValid, DisplayName = "Products Export Valid", Priority = ScenarioPriority.P2, BlockingOnFail = false, DatasetProfiles = ["representative"], ExecutionOrder = 7 },
        new() { ScenarioKey = ReadinessScenarioKeys.ProductsImportValidRoundtrip, DisplayName = "Products Import Valid Roundtrip", Priority = ScenarioPriority.P2, BlockingOnFail = false, DatasetProfiles = ["representative"], ExecutionOrder = 8 },
        new() { ScenarioKey = ReadinessScenarioKeys.ProductsImportThumbnails, DisplayName = "Products Import Thumbnails", Priority = ScenarioPriority.P2, BlockingOnFail = false, DatasetProfiles = ["representative"], ExecutionOrder = 9 },
        new() { ScenarioKey = ReadinessScenarioKeys.ProductsImportInvalidSafeFailure, DisplayName = "Products Import Invalid Safe Failure", Priority = ScenarioPriority.P2, BlockingOnFail = false, DatasetProfiles = ["representative"], ExecutionOrder = 10 },
    };

    public static List<DatasetProfile> GetAllProfiles() => new()
    {
        new()
        {
            ProfileKey = "minimal",
            DisplayName = "Minimal Dataset",
            WorkingDatabasePath = AppDataPaths.Combine("Readiness", "Workspaces", "minimal", "pos_readiness.db"),
            SeedSource = "",
            ImportSampleDirectory = AppDataPaths.Combine("Readiness", "Workspaces", "minimal", "Samples"),
            ExpectedScale = "Near-empty database"
        },
        new()
        {
            ProfileKey = "representative",
            DisplayName = "Representative Dataset",
            WorkingDatabasePath = AppDataPaths.Combine("Readiness", "Workspaces", "representative", "pos_readiness.db"),
            SeedSource = AppDataPaths.Combine("seed_representative.db"),
            ImportSampleDirectory = AppDataPaths.Combine("Readiness", "Workspaces", "representative", "Samples"),
            ExpectedScale = "Populated catalog and sales"
        }
    };
}
