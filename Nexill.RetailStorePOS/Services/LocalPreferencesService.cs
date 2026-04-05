using System.IO;
using System.Text.Json;
using System.Threading;
using RetailStorePOS.Data;

namespace RetailStorePOS.App.Services;

public class LocalPreferencesService : ILocalPreferencesService
{
    public event EventHandler<LocalPreferencesChangedEventArgs>? PreferencesChanged;
    private readonly string _settingsPath;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    // Ensure thread-safe or atomic writes if needed, but for MVP file I/O is sufficient
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalPreferencesService()
    {
        var appFolder = AppDataPaths.GetRootFolder();
        _settingsPath = Path.Combine(appFolder, "preferences.json");
    }

    // For testing purposes (inject custom path)
    public LocalPreferencesService(string customPath)
    {
        _settingsPath = customPath;
    }

    public async Task<LocalPreferences> LoadPreferencesAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new LocalPreferences(); // Defaults
        }

        await _fileLock.WaitAsync();
        try
        {
            using var stream = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<LocalPreferences>(stream, _jsonOptions)
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
            using var stream = new FileStream(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, preferences, _jsonOptions);
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

    private static LocalPreferences ClonePreferences(LocalPreferences preferences)
    {
        return new LocalPreferences
        {
            IsDarkMode = preferences.IsDarkMode,
            QuickCashAmounts = preferences.QuickCashAmounts != null
                ? (decimal[])preferences.QuickCashAmounts.Clone()
                : new[] { 5.0m, 10.0m, 20.0m },
            InstallId = preferences.InstallId,
            InstalledAtUtc = preferences.InstalledAtUtc,
            SetupCompletedAtUtc = preferences.SetupCompletedAtUtc,
            FirstRunAt = preferences.FirstRunAt,
            LastSeenAt = preferences.LastSeenAt,
            LaunchCount = preferences.LaunchCount,
            CrashCount = preferences.CrashCount,
            LastUpdatePromptAt = preferences.LastUpdatePromptAt,
            LastUpdateInstalledAt = preferences.LastUpdateInstalledAt,
            SetupCompletedEventId = preferences.SetupCompletedEventId
        };
    }
}
