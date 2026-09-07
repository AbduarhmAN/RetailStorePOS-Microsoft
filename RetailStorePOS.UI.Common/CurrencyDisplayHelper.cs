using System.Collections.Generic;
using System.Globalization;
using Windows.System.UserProfile;

namespace RetailStorePOS.UI.Common;

public static class CurrencyDisplayHelper
{
    private static readonly object CacheSync = new();
    private static readonly Dictionary<string, string> DisplayCodeCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _hasConfiguredCurrencyCodeCache;
    private static string _cachedConfiguredCurrencyCode = "USD";
    private static bool _hasRegionCodeCache;
    private static string? _cachedRegionCode;

    public static void InvalidateConfiguredState()
    {
        lock (CacheSync)
        {
            _hasConfiguredCurrencyCodeCache = false;
            _cachedConfiguredCurrencyCode = "USD";
            _hasRegionCodeCache = false;
            _cachedRegionCode = null;
            DisplayCodeCache.Clear();
        }
    }

    public static string ResolveConfiguredCurrencyCode()
    {
        lock (CacheSync)
        {
            if (_hasConfiguredCurrencyCodeCache)
            {
                return _cachedConfiguredCurrencyCode;
            }
        }

        string resolvedCurrencyCode;
        try
        {
            var currencyCode = LoginRuntime.Settings?.GetCurrencyCode();
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            resolvedCurrencyCode = string.IsNullOrWhiteSpace(normalizedCurrencyCode)
                ? "USD"
                : normalizedCurrencyCode;
        }
        catch
        {
            // Fallback to USD if settings retrieval fails.
            resolvedCurrencyCode = "USD";
        }

        lock (CacheSync)
        {
            _cachedConfiguredCurrencyCode = resolvedCurrencyCode;
            _hasConfiguredCurrencyCodeCache = true;
            return _cachedConfiguredCurrencyCode;
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
        var cacheKey = $"{preferredRegionCode ?? string.Empty}|{normalizedCurrencyCode}";

        lock (CacheSync)
        {
            if (DisplayCodeCache.TryGetValue(cacheKey, out var cachedDisplayCode))
            {
                return cachedDisplayCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredRegionCode))
        {
            var preferredRegion = TryCreateRegionInfo(preferredRegionCode);
            if (preferredRegion is not null
                && string.Equals(preferredRegion.ISOCurrencySymbol, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                var preferredDisplayCode = ResolveLocalizedCurrencyToken(preferredRegion);
                CacheDisplayCode(cacheKey, preferredDisplayCode);
                return preferredDisplayCode;
            }
        }

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (string.Equals(region.ISOCurrencySymbol, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    var localizedDisplayCode = ResolveLocalizedCurrencyToken(region);
                    CacheDisplayCode(cacheKey, localizedDisplayCode);
                    return localizedDisplayCode;
                }
            }
            catch (ArgumentException)
            {
                // Some cultures might not have an associated RegionInfo; skip them.
            }
        }

        CacheDisplayCode(cacheKey, normalizedCurrencyCode);
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
        lock (CacheSync)
        {
            if (_hasRegionCodeCache)
            {
                return _cachedRegionCode;
            }
        }

        string? resolvedRegionCode;
        try
        {
            resolvedRegionCode = NormalizeRegionCode(LoginRuntime.Settings?.GetRegionCode());
            if (string.IsNullOrWhiteSpace(resolvedRegionCode))
            {
                resolvedRegionCode = null;
            }
        }
        catch
        {
            // Return null if region code retrieval fails.
            resolvedRegionCode = null;
        }

        lock (CacheSync)
        {
            _cachedRegionCode = resolvedRegionCode;
            _hasRegionCodeCache = true;
            return _cachedRegionCode;
        }
    }

    private static void CacheDisplayCode(string cacheKey, string displayCode)
    {
        lock (CacheSync)
        {
            DisplayCodeCache[cacheKey] = displayCode;
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
            return CleanCurrencyToken(region.CurrencySymbol);
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyNativeName)
            && !string.Equals(region.CurrencyNativeName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return CleanCurrencyToken(region.CurrencyNativeName);
        }

        if (!string.IsNullOrWhiteSpace(region.CurrencyEnglishName)
            && !string.Equals(region.CurrencyEnglishName, region.ISOCurrencySymbol, StringComparison.OrdinalIgnoreCase))
        {
            return CleanCurrencyToken(region.CurrencyEnglishName);
        }

        return region.ISOCurrencySymbol;
    }

    /// <summary>
    /// Normalises a currency token for display.
    ///
    /// Many Arabic currency symbols use a period to separate the two letters
    /// of the abbreviation - e.g. SAR "ر.س", KWD "د.ك", AED "د.إ", IQD "ع.د".
    /// Stripping the period collapses them into glued letters ("رس", "دك")
    /// which Arabic readers find harder to scan. Replacing with a space
    /// keeps the visual separation: "ر.س" becomes "ر س".
    ///
    /// For Latin currency tokens (USD, EUR, etc.) there is no period to
    /// replace and this method is effectively a Trim().
    /// </summary>
    private static string CleanCurrencyToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(raw.Length);
        var lastWasSpace = false;
        foreach (var ch in raw)
        {
            char outCh;
            if (ch == '.')
            {
                outCh = ' ';
            }
            else
            {
                outCh = ch;
            }

            if (char.IsWhiteSpace(outCh))
            {
                // Collapse consecutive whitespace into a single space.
                if (lastWasSpace) continue;
                buffer.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                buffer.Append(outCh);
                lastWasSpace = false;
            }
        }

        return buffer.ToString().Trim();
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

    private static string NormalizeRegionCode(string? regionCode)
    {
        return string.IsNullOrWhiteSpace(regionCode)
            ? string.Empty
            : regionCode.Trim().ToUpperInvariant();
    }
}
