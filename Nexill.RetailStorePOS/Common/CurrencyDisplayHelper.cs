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
        var numericAmount = ApplyNativeDigits(amount.ToString("N2", culture), culture);
        var displayCode = ResolveDisplayCode(currencyCode);
        return string.IsNullOrWhiteSpace(displayCode)
            ? numericAmount
            : $"{displayCode} {numericAmount}";
    }

    public static string FormatNumber(decimal amount)
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(amount.ToString("N2", culture), culture);
    }

    public static string FormatDate(DateTime value)
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(value.ToString("d", culture), culture);
    }

    public static string FormatDateTime(DateTime value)
    {
        var culture = ResolveDisplayCulture();
        return ApplyNativeDigits(value.ToString("g", culture), culture);
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
            return region.CurrencySymbol.Trim();
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyNativeName)
            && !string.Equals(region.CurrencyNativeName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return region.CurrencyNativeName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyEnglishName)
            && !string.Equals(region.CurrencyEnglishName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return region.CurrencyEnglishName.Trim();
        }

        return region.ISOCurrencySymbol;
    }

    private static CultureInfo ResolveDisplayCulture()
    {
        try
        {
            var languageTag = GlobalizationPreferences.Languages.FirstOrDefault();
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

        return CultureInfo.CurrentCulture;
    }

    private static string ApplyNativeDigits(string formattedText, CultureInfo culture)
    {
        var nativeDigits = culture.NumberFormat.NativeDigits;
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
