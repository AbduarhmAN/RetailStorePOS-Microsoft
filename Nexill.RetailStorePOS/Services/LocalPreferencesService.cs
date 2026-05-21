using System.Text.Json;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Contracts;

namespace RetailStorePOS.App.Services;

public class LocalPreferencesService : ILocalPreferencesService
{
    public event EventHandler<LocalPreferencesChangedEventArgs>? PreferencesChanged;
    private readonly string _settingsPath;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    public string PreferencesPath => _settingsPath;
    public string PreferencesContractKey => SettingsPlatformContract.LocalPreferencesFileContract.ContractKey;

    public LocalPreferencesService()
    {
        var appFolder = AppDataPaths.GetRootFolder();
        _settingsPath = NormalizeLocalPreferencesPath(Path.Combine(appFolder, SettingsPlatformContract.PreferencesFileName));
    }

    // For testing purposes (inject custom path)
    public LocalPreferencesService(string customPath)
    {
        _settingsPath = NormalizeLocalPreferencesPath(customPath);
    }

    public async Task<LocalPreferences> LoadPreferencesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            TryMigrateLegacyPreferencesFile();

            if (!File.Exists(_settingsPath))
            {
                return new LocalPreferences();
            }

            using var stream = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync(stream, StartupJsonContext.Default.LocalPreferences)
                ?? new LocalPreferences();
        }
        catch
        {
            // Corrupt file -> return defaults
            return new LocalPreferences();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SavePreferencesAsync(LocalPreferences preferences)
    {
        await _fileLock.WaitAsync();
        try
        {
            EnsurePreferencesDirectory(_settingsPath);
            using var stream = new FileStream(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, preferences, StartupJsonContext.Default.LocalPreferences);
        }
        finally
        {
            _fileLock.Release();
        }
        PreferencesChanged?.Invoke(this, new LocalPreferencesChangedEventArgs(ClonePreferences(preferences)));
    }
    public async Task UpdateThemeAsync(bool isDarkMode)
    {
        var current = await LoadPreferencesAsync();
        current.IsDarkMode = isDarkMode;
        await SavePreferencesAsync(current);
    }

    public async Task UpdateQuickCashAsync(decimal[] amounts)
    {
        var current = await LoadPreferencesAsync();
        current.QuickCashAmounts = amounts;
        await SavePreferencesAsync(current);
    }

    public async Task UpdateInstallMetadataAsync(DateTime installedAtUtc)
    {
        var current = await LoadPreferencesAsync();
        current.InstalledAtUtc = installedAtUtc;
        await SavePreferencesAsync(current);
    }

    public async Task UpdateSetupCompletionAsync(DateTime setupCompletedAtUtc, string? eventId = null)
    {
        var current = await LoadPreferencesAsync();
        current.SetupCompletedAtUtc = setupCompletedAtUtc;
        current.SetupCompletedEventId = eventId;
        await SavePreferencesAsync(current);
    }

    public async Task UpdateDailyTargetAsync(decimal amount)
    {
        var current = await LoadPreferencesAsync();
        current.DailyTarget = amount;
        await SavePreferencesAsync(current);
    }

    public async Task UpdateSuppressCrashFeedbackAsync(bool suppress)
    {
        var current = await LoadPreferencesAsync();
        current.SuppressCrashFeedbackPrompt = suppress;
        await SavePreferencesAsync(current);
    }

    private static LocalPreferences ClonePreferences(LocalPreferences preferences)
    {
        return new LocalPreferences
        {
            IsDarkMode = preferences.IsDarkMode,
            QuickCashAmounts = preferences.QuickCashAmounts != null
                ? (decimal[])preferences.QuickCashAmounts.Clone()
                : new[] { 5.0m, 10.0m, 20.0m },
            InstallId = preferences.InstallId,
            InstallIdSource = preferences.InstallIdSource,
            PreviousInstallId = preferences.PreviousInstallId,
            PreviousInstallIdSource = preferences.PreviousInstallIdSource,
            InstallIdMigratedAtUtc = preferences.InstallIdMigratedAtUtc,
            InstalledAtUtc = preferences.InstalledAtUtc,
            SetupCompletedAtUtc = preferences.SetupCompletedAtUtc,
            FirstRunAt = preferences.FirstRunAt,
            LastSeenAt = preferences.LastSeenAt,
            LaunchCount = preferences.LaunchCount,
            CrashCount = preferences.CrashCount,
            LastUpdatePromptAt = preferences.LastUpdatePromptAt,
            LastUpdateInstalledAt = preferences.LastUpdateInstalledAt,
            SetupCompletedEventId = preferences.SetupCompletedEventId,
            DailyTarget = preferences.DailyTarget,
            SuppressCrashFeedbackPrompt = preferences.SuppressCrashFeedbackPrompt
        };
    }

    private static string NormalizeLocalPreferencesPath(string path)
    {
        return SettingsPlatformContract.NormalizeLocalPreferencesPath(path);
    }

    private static void EnsurePreferencesDirectory(string preferencesPath)
    {
        var directory = Path.GetDirectoryName(preferencesPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void TryMigrateLegacyPreferencesFile()
    {
        if (File.Exists(_settingsPath))
        {
            return;
        }

        var legacyPath = NormalizeLocalPreferencesPath(
            Path.Combine(
                AppDataPaths.GetLegacyRootFolder(),
                SettingsPlatformContract.PreferencesFileName));

        if (!File.Exists(legacyPath))
        {
            return;
        }

        EnsurePreferencesDirectory(_settingsPath);
        File.Copy(legacyPath, _settingsPath, overwrite: false);
    }
}
