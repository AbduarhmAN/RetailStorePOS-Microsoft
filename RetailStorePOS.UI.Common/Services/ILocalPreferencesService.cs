using System.Text.Json.Serialization;

namespace RetailStorePOS.UI.Common.Services;

public interface ILocalPreferencesService
{
    event EventHandler<LocalPreferencesChangedEventArgs>? PreferencesChanged;
    string PreferencesPath { get; }
    string PreferencesContractKey { get; }

    // Loads settings from disk. Returns default if file missing.
    Task<LocalPreferences> LoadPreferencesAsync();

    // Saves current settings to disk.
    Task SavePreferencesAsync(LocalPreferences preferences);

    // Helper methods for individual updates
    Task UpdateThemeAsync(bool isDarkMode);
    Task UpdateQuickCashAsync(decimal[] amounts);
    Task UpdateInstallMetadataAsync(DateTime installedAtUtc);
    Task UpdateSetupCompletionAsync(DateTime setupCompletedAtUtc, string? eventId = null);
    Task UpdateDailyTargetAsync(decimal amount);
    Task UpdateSuppressCrashFeedbackAsync(bool suppress);
}

public sealed class LocalPreferencesChangedEventArgs : EventArgs
{
    public LocalPreferencesChangedEventArgs(LocalPreferences preferences)
    {
        Preferences = preferences;
    }

    public LocalPreferences Preferences { get; }
}

public class LocalPreferences
{
    public bool IsDarkMode { get; set; } = false;

    [JsonPropertyName("quickCash")]
    public decimal[] QuickCashAmounts { get; set; } = new[] { 5.0m, 10.0m, 20.0m };

    public string? InstallId { get; set; }
    public string? InstallIdSource { get; set; }
    public string? PreviousInstallId { get; set; }
    public string? PreviousInstallIdSource { get; set; }
    public DateTime? InstallIdMigratedAtUtc { get; set; }

    public DateTime? InstalledAtUtc { get; set; }

    public DateTime? SetupCompletedAtUtc { get; set; }

    public DateTime? FirstRunAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public int LaunchCount { get; set; }

    public int CrashCount { get; set; }

    public DateTime? LastUpdatePromptAt { get; set; }

    public DateTime? LastUpdateInstalledAt { get; set; }

    public string? SetupCompletedEventId { get; set; }

    public decimal DailyTarget { get; set; } = 200000m;

    public bool SuppressCrashFeedbackPrompt { get; set; } = false;
}
