using System.Globalization;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class SettingsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SettingsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public string GetCurrencyCode()
    {
        return GetSetting("currency_code", "USD");
    }

    public void SetCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        SetSetting("currency_code", currencyCode.Trim().ToUpperInvariant());
    }

    public TaxSettings GetTaxSettings()
    {
        var enabled = GetSetting("tax_enabled", "0") == "1";
        var rateText = GetSetting("tax_rate_percent", "0");

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
        SetSetting("tax_enabled", settings.Enabled ? "1" : "0");
        SetSetting("tax_rate_percent", settings.RatePercent.ToString(CultureInfo.InvariantCulture));
    }

    public string GetStoreName() => GetSetting("store_name", "My Store");
    public string GetStoreAddress() => GetSetting("store_address", "");

    public void SetStoreName(string value) => SetSetting("store_name", value.Trim());
    public void SetStoreAddress(string value) => SetSetting("store_address", value.Trim());

    public string GetTaxRoundingStrategy() => GetSetting("tax_rounding_strategy", "HALF_UP");
    public void SetTaxRoundingStrategy(string value) => SetSetting("tax_rounding_strategy", value);

    public string GetCashRoundingUnit() => GetSetting("cash_rounding_unit", "0.01");
    public void SetCashRoundingUnit(string value) => SetSetting("cash_rounding_unit", value);

    public bool IsOnboardingPhaseCleared() => GetSetting("onboarding_phase_cleared", "0") == "1";
    public void ClearOnboardingPhase() => SetSetting("onboarding_phase_cleared", "1");

    public bool IsFirstRunTutorialCleared() => GetSetting("first_run_tutorial_cleared", "0") == "1";
    public void ClearFirstRunTutorial() => SetSetting("first_run_tutorial_cleared", "1");

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
