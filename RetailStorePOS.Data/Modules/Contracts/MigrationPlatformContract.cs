using RetailStorePOS.Data.Modules.Migrations;

namespace RetailStorePOS.Data.Modules.Contracts;

public static class MigrationPlatformContract
{
    public static readonly ModuleContract StartupSafeMigrationCommand = new()
    {
        ContractKey = "Migration.StartupSafe",
        OwnerModuleKey = "Migrations",
        ContractType = ModuleContractType.Command,
        Purpose = "Run lightweight additive migrations during splash",
        OfflineAllowed = true
    };

    public static readonly ModuleContract MaintenanceMigrationCommand = new()
    {
        ContractKey = "Migration.Maintenance",
        OwnerModuleKey = "Migrations",
        ContractType = ModuleContractType.Command,
        Purpose = "Run heavy backfill/rebuild migrations in background",
        OfflineAllowed = true
    };

    public static string NormalizeDatabasePath(string databasePath)
    {
        return Path.GetFullPath(databasePath);
    }

    public static bool ShouldRunInStartupPath(MigrationCategory category)
    {
        return category == MigrationCategory.StartupSafe;
    }

    public static bool ShouldValidateBeforeCommit(MigrationCategory category)
    {
        return category >= MigrationCategory.HeavyBackfill;
    }
}
