using RetailStorePOS.Data.Modules.Contracts;

namespace RetailStorePOS.Data.Modules.Migrations;

public enum MigrationCategory
{
    /// <summary>
    /// Fast, additive, non-breaking changes that can run in the splash path.
    /// </summary>
    StartupSafe,

    /// <summary>
    /// Data backfills or migrations that may take significant time.
    /// </summary>
    HeavyBackfill,

    /// <summary>
    /// Breaking schema changes or operations requiring full table rebuilds.
    /// </summary>
    RebuildOrDestructive
}

public sealed class MigrationStep
{
    public int Version { get; init; }
    public string Description { get; init; } = string.Empty;
    public MigrationCategory Category { get; init; }
    public Action<Microsoft.Data.Sqlite.SqliteConnection> Execute { get; init; } = null!;
    public bool RunsInStartupPath => MigrationPlatformContract.ShouldRunInStartupPath(Category);
    public string CoordinationContractKey => RunsInStartupPath
        ? MigrationPlatformContract.StartupSafeMigrationCommand.ContractKey
        : MigrationPlatformContract.MaintenanceMigrationCommand.ContractKey;
}
