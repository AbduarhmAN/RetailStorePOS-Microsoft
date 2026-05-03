using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace RetailStorePOS.WinUiLogin.Common;

public static class LocalizationHelper
{
    private static ResourceLoader? _loader;

    private static ResourceLoader Loader => _loader ??= new ResourceLoader();

    public static readonly (string Tag, string NativeName, string EnglishName, bool IsRtl)[] SupportedLanguages =
    [
        ("en-US", "English", "English", false),
        ("ar-SA", "العربية", "Arabic", true),
        ("fr-FR", "Français", "French", false),
    ];

    public static string GetString(string key)
    {
        try
        {
            return Loader.GetString(key);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static void ResetResourceLoader()
    {
        _loader = null;
    }

    public static string Format(string key, params object[] args)
    {
        var template = Loader.GetString(key);
        return string.Format(template, args);
    }

    public static string GetEffectiveLanguageTag()
    {
        var lang =
            TryGetPrimaryLanguageOverride()
            ?? TryGetFirstPreferredLanguage()
            ?? DefaultLanguage;

        return NormalizeLanguageTag(lang);
    }

    public static bool IsRtl
    {
        get
        {
            var lang = GetEffectiveLanguageTag();
            return lang.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? TryGetPrimaryLanguageOverride()
    {
        try
        {
            var overrideTag = ApplicationLanguages.PrimaryLanguageOverride;
            return string.IsNullOrWhiteSpace(overrideTag) ? null : overrideTag;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetFirstPreferredLanguage()
    {
        try
        {
            var languages = ApplicationLanguages.Languages;
            return languages.Count > 0 ? languages[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public static string NormalizeLanguageTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return DefaultLanguage;
        }

        var normalized = tag.Trim().Replace('_', '-').ToLowerInvariant();

        var mapped = normalized switch
        {
            "ar" or "ar-sa" => "ar-SA",
            "fr" or "fr-fr" => "fr-FR",
            "arabic" or "العربية" => "ar-SA",
            "french" or "francais" or "français" => "fr-FR",
            "english" => DefaultLanguage,
            "en" or "en-us" => DefaultLanguage,
            var value when value.StartsWith("ar-", StringComparison.Ordinal) => "ar-SA",
            var value when value.StartsWith("fr-", StringComparison.Ordinal) => "fr-FR",
            var value when value.StartsWith("en-", StringComparison.Ordinal) => DefaultLanguage,
            _ => DefaultLanguage
        };

        foreach (var supportedLanguage in SupportedLanguages)
        {
            if (string.Equals(mapped, supportedLanguage.Tag, StringComparison.OrdinalIgnoreCase))
            {
                return supportedLanguage.Tag;
            }
        }

        return DefaultLanguage;
    }

    public const string DefaultLanguage = "en-US";
}
