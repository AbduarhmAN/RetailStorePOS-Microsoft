using System.Globalization;
using Windows.System.UserProfile;

namespace RetailStorePOS.WinUiLogin.Common;

internal static class CurrencyDisplayHelper
{
    public static string ResolveConfiguredCurrencyCode()
    {
        try
        {
            var currencyCode = LoginRuntime.Settings?.GetCurrencyCode();
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            return string.IsNullOrWhiteSpace(normalizedCurrencyCode)
                ? "USD"
                : normalizedCurrencyCode;
        }
        catch
        {
            // Fallback to USD if settings retrieval fails.
            return "USD";
        }
    }

    public static string ResolveConfiguredDisplayCode()
    {
        return ResolveDisplayCode(ResolveConfiguredCurrencyCode());
    }

    public static string ResolveDisplayCode(string? currencyCode)
    {
        var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
        if (string.IsNullOrWhiteSpace(normalizedCurrencyCode))
        {
            normalizedCurrencyCode = "USD";
        }

        var preferredRegionCode = TryGetSavedRegionCode();
        if (!string.IsNullOrWhiteSpace(preferredRegionCode))
        {
            var preferredRegion = TryCreateRegionInfo(preferredRegionCode);
            if (preferredRegion is not null
                && string.Equals(preferredRegion.ISOCurrencySymbol, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveLocalizedCurrencyToken(preferredRegion);
            }
        }

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (string.Equals(region.ISOCurrencySymbol, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLocalizedCurrencyToken(region);
                }
            }
            catch (ArgumentException)
            {
                // Some cultures might not have an associated RegionInfo; skip them.
            }
        }

        return normalizedCurrencyCode;
    }

    public static string FormatConfiguredAmount(decimal amount)
    {
        return FormatAmount(amount, ResolveConfiguredCurrencyCode());
    }

    public static string FormatAmount(decimal amount, string? currencyCode)
    {
        var culture = ResolveDisplayCulture();
        var format = amount == Math.Truncate(amount) ? "N0" : "N2";
        var numericAmount = ApplyNativeDigits(amount.ToString(format, culture), culture);
        var displayCode = ResolveDisplayCode(currencyCode);
        return string.IsNullOrWhiteSpace(displayCode)
            ? numericAmount
            : $"{displayCode} {numericAmount}";
    }

    public static string FormatNumber(decimal amount, string format = "N2")
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(amount.ToString(format, culture), culture);
    }

    public static string FormatInt(long amount)
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(amount.ToString("N0", culture), culture);
    }

    public static string FormatPercent(double percent, string format = "0")
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits($"{percent.ToString(format, culture)}%", culture);
    }

    public static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatStringWithDigits(string input)
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(input, culture);
    }

    private static string? TryGetSavedRegionCode()
    {
        try
        {
            return LoginRuntime.Settings?.GetRegionCode();
        }
        catch
        {
            // Return null if region code retrieval fails.
            return null;
        }
    }

    private static RegionInfo? TryCreateRegionInfo(string regionCode)
    {
        try
        {
            return new RegionInfo(regionCode);
        }
        catch (ArgumentException)
        {
            // Invalid region code; return null.
            return null;
        }
    }

    private static string ResolveLocalizedCurrencyToken(RegionInfo region)
    {
        if (!string.IsNullOrWhiteSpace(region.CurrencySymbol)
            && !string.Equals(region.CurrencySymbol, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return region.CurrencySymbol.Replace(".", "").Trim();
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyNativeName)
            && !string.Equals(region.CurrencyNativeName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return region.CurrencyNativeName.Replace(".", "").Trim();
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyEnglishName)
            && !string.Equals(region.CurrencyEnglishName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return region.CurrencyEnglishName.Replace(".", "").Trim();
        }

        return region.ISOCurrencySymbol;
    }

    private static CultureInfo ResolveDisplayCulture()
    {
        try
        {
            var languageTag = LocalizationHelper.GetEffectiveLanguageTag();

            if (!string.IsNullOrWhiteSpace(languageTag))
            {
                return CultureInfo.CreateSpecificCulture(languageTag);
            }
        }
        catch (CultureNotFoundException ex)
        {
            // The preferred language tag is not a valid culture; fall back to CurrentCulture.
            StartupTrace.Write($"CurrencyDisplayHelper.ResolveDisplayCulture: CultureNotFoundException: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // The preferred language tag is invalid; fall back to CurrentCulture.
            StartupTrace.Write($"CurrencyDisplayHelper.ResolveDisplayCulture: ArgumentException: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            StartupTrace.Write($"CurrencyDisplayHelper.ResolveDisplayCulture: InvalidOperationException: {ex.Message}");
        }

        try
        {
            var systemLanguage = GlobalizationPreferences.Languages.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(systemLanguage))
            {
                return CultureInfo.CreateSpecificCulture(systemLanguage);
            }
        }
        catch (Exception ex) when (ex is CultureNotFoundException or ArgumentException or InvalidOperationException)
        {
            StartupTrace.Write($"CurrencyDisplayHelper.ResolveDisplayCulture: System fallback failed: {ex.Message}");
        }

        return CultureInfo.GetCultureInfo(LocalizationHelper.DefaultLanguage);
    }

    private static string ApplyNativeDigits(string formattedText, CultureInfo culture)
    {
        var nativeDigits = culture.NumberFormat.NativeDigits;

        // Force Eastern Arabic digits if we are in an Arabic culture and the system 
        // hasn't already provided them (some Windows configs default to Western digits even for ar-SA).
        if (culture.TwoLetterISOLanguageName == "ar" && nativeDigits[0] == "0")
        {
            nativeDigits = new[] { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
        }

        if (nativeDigits.Length != 10)
        {
            return formattedText;
        }

        var buffer = new System.Text.StringBuilder(formattedText.Length * 2);

        foreach (var character in formattedText)
        {
            if (character >= '0' && character <= '9')
            {
                buffer.Append(nativeDigits[character - '0']);
            }
            else
            {
                buffer.Append(character);
            }
        }

        return buffer.ToString();
    }

    private static string NormalizeCurrencyCode(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? string.Empty
            : currencyCode.Trim().ToUpperInvariant();
    }
}
