using System.Globalization;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Modules.Tax;

namespace RetailStorePOS.Data.Modules.Settings;

public sealed class SettingsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SettingsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public string GetCurrencyCode()
    {
        return GetSetting(SettingsPlatformContract.CurrencyCodeKey, "USD");
    }

    public string GetRegionCode()
    {
        return GetSetting(SettingsPlatformContract.RegionCodeKey, RegionInfo.CurrentRegion.TwoLetterISORegionName);
    }

    public void SetCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        SetSetting(SettingsPlatformContract.CurrencyCodeKey, currencyCode.Trim().ToUpperInvariant());
    }

    public void SetRegionCode(string regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            throw new ArgumentException("Region code is required.", nameof(regionCode));
        }

        SetSetting(SettingsPlatformContract.RegionCodeKey, regionCode.Trim().ToUpperInvariant());
    }

    public TaxSettings GetTaxSettings()
    {
        var enabled = GetSetting(SettingsPlatformContract.TaxEnabledKey, "0") == "1";
        var rateText = GetSetting(SettingsPlatformContract.TaxRatePercentKey, "0");

        if (!decimal.TryParse(rateText, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
        {
            rate = 0m;
        }

        return new TaxSettings
        {
            Enabled = enabled,
            RatePercent = rate
        };
    }

    public void SetTaxSettings(TaxSettings settings)
    {
        SetSetting(SettingsPlatformContract.TaxEnabledKey, settings.Enabled ? "1" : "0");
        SetSetting(SettingsPlatformContract.TaxRatePercentKey, settings.RatePercent.ToString(CultureInfo.InvariantCulture));
    }

    public string GetStoreName() => GetSetting(SettingsPlatformContract.StoreNameKey, "My Store");
    public string GetStoreAddress() => GetSetting(SettingsPlatformContract.StoreAddressKey, "");
    public string? GetPreferredPrinterName() => GetSetting(SettingsPlatformContract.PreferredPrinterNameKey, string.Empty) is var value && !string.IsNullOrWhiteSpace(value) ? value : null;
    public bool GetTouchModeEnabled() => GetSetting(SettingsPlatformContract.TouchModeEnabledKey, "0") == "1";

    public void SetStoreName(string value) => SetSetting(SettingsPlatformContract.StoreNameKey, value.Trim());
    public void SetStoreAddress(string value) => SetSetting(SettingsPlatformContract.StoreAddressKey, value.Trim());
    public void SetPreferredPrinterName(string? value) => SetSetting(SettingsPlatformContract.PreferredPrinterNameKey, value?.Trim() ?? string.Empty);
    public void SetTouchModeEnabled(bool enabled) => SetSetting(SettingsPlatformContract.TouchModeEnabledKey, enabled ? "1" : "0");

    public string GetTaxRoundingStrategy() => GetSetting(SettingsPlatformContract.TaxRoundingStrategyKey, "HALF_UP");
    public void SetTaxRoundingStrategy(string value) => SetSetting(SettingsPlatformContract.TaxRoundingStrategyKey, value);

    public string GetCashRoundingUnit() => GetSetting(SettingsPlatformContract.CashRoundingUnitKey, "0.01");
    public void SetCashRoundingUnit(string value) => SetSetting(SettingsPlatformContract.CashRoundingUnitKey, value);

    public bool IsOnboardingPhaseCleared() => GetSetting(SettingsPlatformContract.OnboardingPhaseClearedKey, "0") == "1";
    public void ClearOnboardingPhase() => SetSetting(SettingsPlatformContract.OnboardingPhaseClearedKey, "1");

    public bool IsFirstRunTutorialCleared() => GetSetting(SettingsPlatformContract.FirstRunTutorialClearedKey, "0") == "1";
    public void ClearFirstRunTutorial() => SetSetting(SettingsPlatformContract.FirstRunTutorialClearedKey, "1");

    public string? GetActiveRunId() => GetSetting(SettingsPlatformContract.TelemetryActiveRunIdKey, string.Empty) is var v && !string.IsNullOrEmpty(v) ? v : null;
    public void SetActiveRunId(string? runId) => SetSetting(SettingsPlatformContract.TelemetryActiveRunIdKey, runId ?? "");

    public string? GetActiveRunStartedAt() => GetSetting(SettingsPlatformContract.TelemetryActiveRunStartedAtUtcKey, string.Empty) is var v && !string.IsNullOrEmpty(v) ? v : null;
    public void SetActiveRunStartedAt(string? startedAt) => SetSetting(SettingsPlatformContract.TelemetryActiveRunStartedAtUtcKey, startedAt ?? "");

    public string? GetLastActivityAt() => GetSetting(SettingsPlatformContract.TelemetryLastActivityAtUtcKey, string.Empty) is var v && !string.IsNullOrEmpty(v) ? v : null;
    public void SetLastActivityAt(string? lastActivityAt) => SetSetting(SettingsPlatformContract.TelemetryLastActivityAtUtcKey, lastActivityAt ?? "");

    public string? GetLastActivitySource() => GetSetting(SettingsPlatformContract.TelemetryLastActivitySourceKey, string.Empty) is var v && !string.IsNullOrEmpty(v) ? v : null;
    public void SetLastActivitySource(string? source) => SetSetting(SettingsPlatformContract.TelemetryLastActivitySourceKey, source ?? "");

    public string GetAppLanguage() => GetSetting(SettingsPlatformContract.AppLanguageKey, "en");
    public void SetAppLanguage(string tag) => SetSetting(SettingsPlatformContract.AppLanguageKey, tag);

    public string GetStoreLanguage() => GetSetting(SettingsPlatformContract.StoreLanguageKey, "en");
    public void SetStoreLanguage(string tag) => SetSetting(SettingsPlatformContract.StoreLanguageKey, tag);

    public RetailStorePOS.Data.Models.TelemetryRuntimeState GetTelemetryState()
    {
        return new RetailStorePOS.Data.Models.TelemetryRuntimeState
        {
            ActiveRunId = GetActiveRunId(),
            ActiveRunStartedAt = DateTime.TryParse(GetActiveRunStartedAt(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var startedAt) ? startedAt : null,
            LastActivityAt = DateTime.TryParse(GetLastActivityAt(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var lastActivityAt) ? lastActivityAt : null,
            LastActivitySource = GetLastActivitySource()
        };
    }

    public void SetTelemetryState(RetailStorePOS.Data.Models.TelemetryRuntimeState state)
    {
        SetActiveRunId(state.ActiveRunId);
        SetActiveRunStartedAt(state.ActiveRunStartedAt?.ToString("O"));
        SetLastActivityAt(state.LastActivityAt?.ToString("O"));
        SetLastActivitySource(state.LastActivitySource);
    }

    public void SetTelemetryState(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, RetailStorePOS.Data.Models.TelemetryRuntimeState state)
    {
        SetSetting(connection, transaction, SettingsPlatformContract.TelemetryActiveRunIdKey, state.ActiveRunId ?? "");
        SetSetting(connection, transaction, SettingsPlatformContract.TelemetryActiveRunStartedAtUtcKey, state.ActiveRunStartedAt?.ToString("O") ?? "");
        SetSetting(connection, transaction, SettingsPlatformContract.TelemetryLastActivityAtUtcKey, state.LastActivityAt?.ToString("O") ?? "");
        SetSetting(connection, transaction, SettingsPlatformContract.TelemetryLastActivitySourceKey, state.LastActivitySource ?? "");
    }

    private void SetSetting(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO settings (key, value)
VALUES (@key, @value)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;";

        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    public Microsoft.Data.Sqlite.SqliteConnection OpenConnection() => _factory.OpenConnection();

    private string GetSetting(string key, string defaultValue)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = @key;";
        command.Parameters.AddWithValue("@key", key);

        var result = command.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    private void SetSetting(string key, string value)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO settings (key, value)
VALUES (@key, @value)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;";

        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }
}
