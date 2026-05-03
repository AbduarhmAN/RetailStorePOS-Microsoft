using RetailStorePOS.Data.Modules.Migrations;

namespace RetailStorePOS.Data.Modules.Contracts;

public static class MigrationPlatformContract
{
    public static readonly OwnedDataAsset SchemaVersionState = new()
    {
        AssetKey = "migrations.schema-version",
        AssetType = OwnedDataAssetType.OperationalState,
        OwnerModuleKey = "Migrations",
        PrimaryConsumers = ["Migrations"],
        Notes = "SQLite schema version and migration execution path metadata."
    };

    public static readonly ModuleContract StartupSafeMigrationCommand = new()
    {
        ContractKey = "migrations.startup-safe",
        OwnerModuleKey = "Migrations",
        ContractType = ModuleContractType.Command,
        Purpose = "Run additive startup-safe schema transitions during normal launch.",
        Consumers = ["App Runtime"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract MaintenanceMigrationCommand = new()
    {
        ContractKey = "migrations.maintenance",
        OwnerModuleKey = "Migrations",
        ContractType = ModuleContractType.Command,
        Purpose = "Run full maintenance migrations with validation outside the splash path.",
        Consumers = ["App Runtime", "Maintenance UI"],
        OfflineAllowed = true
    };

    public static readonly WorkflowBoundary StartupSafeBoundary = new()
    {
        WorkflowKey = "migrations.startup-safe",
        CoordinatorModuleKey = "Migrations",
        ParticipatingModules = ["Migrations"],
        OfflineCritical = true,
        WriteSequence =
        [
            "Normalize the local database path.",
            "Initialize the schema if needed.",
            "Run startup-safe migrations only through migrations.startup-safe."
        ]
    };

    public static readonly WorkflowBoundary MaintenanceBoundary = new()
    {
        WorkflowKey = "migrations.maintenance",
        CoordinatorModuleKey = "Migrations",
        ParticipatingModules = ["Migrations"],
        OfflineCritical = false,
        WriteSequence =
        [
            "Normalize the local database path.",
            "Run database validation before maintenance work.",
            "Run all pending migrations through migrations.maintenance.",
            "Validate integrity before completing maintenance."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        StartupSafeMigrationCommand,
        MaintenanceMigrationCommand
    ];

    public static string NormalizeDatabasePath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database path must remain local for migration workflows.");
        }

        return fullPath;
    }

    public static bool ShouldRunInStartupPath(MigrationCategory category) => category <= MigrationCategory.StartupSafe;
    public static bool ShouldValidateBeforeCommit(MigrationCategory category) => category >= MigrationCategory.HeavyBackfill;
}
