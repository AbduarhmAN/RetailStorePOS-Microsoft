using System.IO;

namespace RetailStorePOS.Data.Modules.Contracts;

public static class SettingsPlatformContract
{
    public const string CurrencyCodeKey = "CurrencyCode";
    public const string RegionCodeKey = "RegionCode";
    public const string TaxEnabledKey = "TaxEnabled";
    public const string TaxRatePercentKey = "TaxRatePercent";
    public const string StoreNameKey = "StoreName";
    public const string StoreAddressKey = "StoreAddress";
    public const string PreferredPrinterNameKey = "PreferredPrinterName";
    public const string TouchModeEnabledKey = "TouchModeEnabled";
    public const string TaxRoundingStrategyKey = "TaxRoundingStrategy";
    public const string CashRoundingUnitKey = "CashRoundingUnit";
    public const string OnboardingPhaseClearedKey = "OnboardingPhaseCleared";
    public const string FirstRunTutorialClearedKey = "FirstRunTutorialCleared";
    public const string TelemetryActiveRunIdKey = "TelemetryActiveRunId";
    public const string TelemetryActiveRunStartedAtUtcKey = "TelemetryActiveRunStartedAtUtc";
    public const string TelemetryLastActivityAtUtcKey = "TelemetryLastActivityAtUtc";
    public const string TelemetryLastActivitySourceKey = "TelemetryLastActivitySource";
    public const string AppLanguageKey = "app_language";
    public const string StoreLanguageKey = "store_language";
    public const string SuppressCrashFeedbackPromptKey = "SuppressCrashFeedbackPrompt";
    public const string SessionIdleTimeoutMinutesKey = "SessionIdleTimeoutMinutes";

    public const string PreferencesFileName = "preferences.json";

    public static class LocalPreferencesFileContract
    {
        public const string ContractKey = "LocalPreferences";
    }

    public static string NormalizeLocalPreferencesPath(string path)
    {
        return Path.GetFullPath(path);
    }
}
