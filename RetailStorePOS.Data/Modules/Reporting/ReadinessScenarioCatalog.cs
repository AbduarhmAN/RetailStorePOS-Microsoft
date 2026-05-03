namespace RetailStorePOS.Data.Modules.Reporting;

public static class ReadinessScenarioCatalog
{
    public static List<VerificationScenario> GetScenarios()
    {
        return new List<VerificationScenario>
        {
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.StartupBootstrap,
                DisplayName = "Core Bootstrap",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "minimal", "representative" },
                ExecutionOrder = 1
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.DatabaseLocalReadWrite,
                DisplayName = "Local Data Access",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "minimal", "representative" },
                ExecutionOrder = 2
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.AuthLogin,
                DisplayName = "Authentication",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "minimal", "representative" },
                ExecutionOrder = 3
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.CatalogLookup,
                DisplayName = "Catalog Lookup",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 4
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.CheckoutCompleteSale,
                DisplayName = "Checkout Flow",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 5
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.TaxCalculate,
                DisplayName = "Tax Calculation",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 6
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.ProductsExportValid,
                DisplayName = "Product Export",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = false,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 7
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.ProductsImportValidRoundtrip,
                DisplayName = "Import Round-Trip",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 8
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.ProductsImportThumbnails,
                DisplayName = "Import Thumbnails",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = false,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 9
            },
            new()
            {
                ScenarioKey = ReadinessScenarioKeys.ProductsImportInvalidSafeFailure,
                DisplayName = "Import Safety",
                Priority = ScenarioPriority.P1,
                BlockingOnFail = true,
                DatasetProfiles = new List<string> { "representative" },
                ExecutionOrder = 10
            }
        };
    }

    public static List<DatasetProfile> GetProfiles()
    {
        return new List<DatasetProfile>
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
}
