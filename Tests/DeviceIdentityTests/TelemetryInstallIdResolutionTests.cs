using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nexill.RetailStorePOS.Services.DeviceIdentity;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Migrations;

namespace DeviceIdentityTests;

[TestClass]
public sealed class TelemetryInstallIdResolutionTests
{
    private string _testRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "RetailStorePOS_DeviceIdentityTests",
            Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_testRoot, true); } catch { }
    }

    [TestMethod]
    public async Task LegacyDatabaseAndPreferences_LogAppLaunch_UpgradesToLatestAndPromotesInstallIdAsync()
    {
        var hardwareInstallId = DeviceIdentityService.CreateStableInstallId();
        if (string.IsNullOrWhiteSpace(hardwareInstallId))
        {
            Assert.Inconclusive("Hardware install ID is unavailable on this machine.");
        }

        var legacyInstallId = Guid.NewGuid().ToString();
        var databasePath = PrepareLegacyDatabase();
        var preferencesPath = Path.Combine(_testRoot, "local-preferences.json");
        var preferencesService = new LocalPreferencesService(preferencesPath);
        await preferencesService.SavePreferencesAsync(new LocalPreferences
        {
            InstallId = legacyInstallId,
            InstallIdSource = "legacy_guid",
            InstalledAtUtc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            FirstRunAt = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            LastSeenAt = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        });

        DatabaseInitializer.Initialize(databasePath);
        var maintenanceResult = DatabaseInitializer.RunMaintenanceMigrations(databasePath);

        Assert.IsTrue(maintenanceResult.Success);
        Assert.AreEqual(24, maintenanceResult.Version);
        Assert.AreEqual(24, GetUserVersion(databasePath));

        var telemetry = new TelemetryService(preferencesService, new SqliteConnectionFactory(databasePath));
        var runId = Guid.NewGuid().ToString();

        await telemetry.LogAppLaunchAsync(runId);

        var updatedPreferences = await preferencesService.LoadPreferencesAsync();
        Assert.AreEqual(hardwareInstallId, updatedPreferences.InstallId);
        Assert.AreEqual("hardware_v1_hash", updatedPreferences.InstallIdSource);
        Assert.AreEqual(legacyInstallId, updatedPreferences.PreviousInstallId);
        Assert.AreEqual("legacy_guid", updatedPreferences.PreviousInstallIdSource);
        Assert.IsTrue(updatedPreferences.InstallIdMigratedAtUtc.HasValue);

        var migrationEvent = GetSingleInstallationEvent(databasePath, "install_id_migrated");
        Assert.AreEqual(hardwareInstallId, migrationEvent.InstallId);

        using (var payload = JsonDocument.Parse(migrationEvent.PayloadJson))
        {
            Assert.AreEqual(legacyInstallId, payload.RootElement.GetProperty("previous_install_id").GetString());
            Assert.AreEqual(hardwareInstallId, payload.RootElement.GetProperty("install_id").GetString());
            Assert.AreEqual("hardware_v1_hash", payload.RootElement.GetProperty("install_id_source").GetString());
        }

        var appLaunchEvent = GetSingleInstallationEvent(databasePath, "app_launch");
        Assert.AreEqual(hardwareInstallId, appLaunchEvent.InstallId);
    }

    [TestMethod]
    public async Task LegacyPreferences_MigrationRunsOnlyOnceAcrossRepeatedAppLaunchesAsync()
    {
        var hardwareInstallId = DeviceIdentityService.CreateStableInstallId();
        if (string.IsNullOrWhiteSpace(hardwareInstallId))
        {
            Assert.Inconclusive("Hardware install ID is unavailable on this machine.");
        }

        var legacyInstallId = Guid.NewGuid().ToString();
        var databasePath = PrepareLegacyDatabase();
        var preferencesPath = Path.Combine(_testRoot, "local-preferences.json");
        var preferencesService = new LocalPreferencesService(preferencesPath);
        await preferencesService.SavePreferencesAsync(new LocalPreferences
        {
            InstallId = legacyInstallId,
            InstallIdSource = "legacy_guid"
        });

        DatabaseInitializer.Initialize(databasePath);
        var maintenanceResult = DatabaseInitializer.RunMaintenanceMigrations(databasePath);
        Assert.IsTrue(maintenanceResult.Success);

        var telemetry = new TelemetryService(preferencesService, new SqliteConnectionFactory(databasePath));

        await telemetry.LogAppLaunchAsync(Guid.NewGuid().ToString());
        await telemetry.LogAppLaunchAsync(Guid.NewGuid().ToString());

        var updatedPreferences = await preferencesService.LoadPreferencesAsync();
        Assert.AreEqual(hardwareInstallId, updatedPreferences.InstallId);
        Assert.AreEqual(legacyInstallId, updatedPreferences.PreviousInstallId);
        Assert.AreEqual(1, CountInstallationEvents(databasePath, "install_id_migrated"));
        Assert.AreEqual(2, CountInstallationEvents(databasePath, "app_launch"));
    }

    private string PrepareLegacyDatabase()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Databases",
            "retailstorepos-1.1.0-pos.db");
        if (!File.Exists(fixturePath))
        {
            Assert.Inconclusive($"Missing migration fixture: {fixturePath}");
        }

        var databasePath = Path.Combine(_testRoot, "legacy-upgrade.db");
        File.Copy(fixturePath, databasePath, overwrite: false);
        return databasePath;
    }

    private static int GetUserVersion(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CountInstallationEvents(string databasePath, string eventType)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM installation_events WHERE event_type = @eventType;";
        command.Parameters.AddWithValue("@eventType", eventType);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static InstallationEventSnapshot GetSingleInstallationEvent(string databasePath, string eventType)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT install_id, payload_json
FROM installation_events
WHERE event_type = @eventType
ORDER BY occurred_at DESC, created_at DESC
LIMIT 1;";
        command.Parameters.AddWithValue("@eventType", eventType);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new AssertFailedException($"No installation event with type '{eventType}' was found.");
        }

        return new InstallationEventSnapshot(
            InstallId: reader.GetString(0),
            PayloadJson: reader.GetString(1));
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var factory = new SqliteConnectionFactory(databasePath);
        return factory.OpenConnection();
    }

    private sealed record InstallationEventSnapshot(string InstallId, string PayloadJson);
}
