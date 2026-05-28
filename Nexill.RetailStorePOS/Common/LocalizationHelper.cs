using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace RetailStorePOS.WinUiLogin.Common;

/// <summary>
/// Handles application-wide localization using the Windows App SDK ResourceManager.
/// Specifically optimized for unpackaged (non-MSIX) applications where standard 
/// PrimaryLanguageOverride is often ignored by the OS.
/// </summary>
public static class LocalizationHelper
{
    private static ResourceManager? _manager;
    private static ResourceContext? _context;
    private static string _currentLanguage = DefaultLanguage;

    private static (ResourceManager Manager, ResourceContext Context) GetResourceSystem()
    {
        if (_manager == null)
        {
            try
            {
                // In a packaged (Store) app, accessing Package.Current works.
                var _ = Windows.ApplicationModel.Package.Current;
                StartupTrace.Write("LocalizationHelper: Initializing ResourceManager for PACKAGED Store App");
                _manager = new ResourceManager();
            }
            catch
            {
                StartupTrace.Write("LocalizationHelper: Initializing ResourceManager for UNPACKAGED App (Development)");
                // In unpackaged mode, we must manually point to the resources.pri if it's not in the default location
                _manager = new ResourceManager();
            }

            _context = _manager.CreateResourceContext();
        }

        // IMPORTANT: In Packaged apps, simply modifying the qualifier on a singleton context 
        // doesn't always refresh the ResourceMap's view. We ensure the qualifier is fresh.
        var lang = GetEffectiveLanguageTag();
        if (_context!.QualifierValues["Language"] != lang)
        {
            _context.QualifierValues["Language"] = lang;
            StartupTrace.Write($"LocalizationHelper: ResourceContext Language Qualifier set to {lang}");
        }

        return (_manager, _context);
    }

    private static void ApplyLanguageToContext(string languageTag)
    {
        _currentLanguage = NormalizeLanguageTag(languageTag);
        
        // Ensure the manager/context exist
        var (manager, context) = GetResourceSystem();
        context.QualifierValues["Language"] = _currentLanguage;
        
        StartupTrace.Write($"LocalizationHelper: Applied Language {_currentLanguage} to Context");
    }

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
            var (manager, context) = GetResourceSystem();

            // 1. TRY CURRENT LANGUAGE
            var result = TryResolve(manager, context, key);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            // 2. FALLBACK TO ENGLISH
            if (_currentLanguage != DefaultLanguage)
            {
                var fallbackContext = manager.CreateResourceContext();
                fallbackContext.QualifierValues["Language"] = DefaultLanguage;

                var fallbackResult = TryResolve(manager, fallbackContext, key);
                if (!string.IsNullOrEmpty(fallbackResult))
                {
                    StartupTrace.Write($"LocalizationHelper.GetString: FALLBACK to English for {key}");
                    return fallbackResult;
                }
            }

            StartupTrace.Write($"LocalizationHelper.GetString: [NOT FOUND] {key}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LocalizationHelper.GetString: ERROR for {key}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Tries to resolve a resource key using a tiered namespace probe.
    /// In Packaged apps, resources may be under "Resources/[key]" or "[AssemblyName]/Resources/[key]".
    /// </summary>
    private static string? TryResolve(ResourceManager manager, ResourceContext context, string key)
    {
        if (key.Contains('.'))
        {
            // Most .resw property-style keys such as Foo_Bar.Text are stored
            // under MRT subpaths like Resources/Foo_Bar/Text. Probe that first
            // to avoid first-chance COM exceptions on the raw dotted form.
            string mrtPath = key.Replace('.', '/');
            var result = ResolveProbe(manager, context, $"Resources/{mrtPath}");
            if (result != null) return result;

            result = ResolveProbe(manager, context, $"Nexill.RetailStorePOS/Resources/{mrtPath}");
            if (result != null) return result;

            result = ResolveProbe(manager, context, $"Resources/{key}");
            if (result != null) return result;

            result = ResolveProbe(manager, context, $"Nexill.RetailStorePOS/Resources/{key}");
            if (result != null) return result;
        }
        else
        {
            var result = ResolveProbe(manager, context, $"Resources/{key}");
            if (result != null) return result;

            result = ResolveProbe(manager, context, $"Nexill.RetailStorePOS/Resources/{key}");
            if (result != null) return result;

            // Fallback for plain keys that may actually be stored as a .Text sub-item.
            result = ResolveProbe(manager, context, $"Resources/{key}/Text");
            if (result != null) return result;

            result = ResolveProbe(manager, context, $"Nexill.RetailStorePOS/Resources/{key}/Text");
            if (result != null) return result;
        }

        return null;
    }

    private static string? ResolveProbe(ResourceManager manager, ResourceContext context, string fullPath)
    {
        try
        {
            var candidate = manager.MainResourceMap.GetValue(fullPath, context);
            if (candidate != null && !string.IsNullOrEmpty(candidate.ValueAsString))
            {
                return candidate.ValueAsString;
            }
        }
        catch { /* probe failed, continue to next tier */ }
        return null;
    }

    /// <summary>
    /// Forces the resource system to switch to a new language at runtime.
    /// Note: This affects future GetString calls but does not automatically update existing UI.
    /// </summary>
    public static void SetRuntimeLanguage(string languageTag)
    {
        var tag = NormalizeLanguageTag(languageTag);
        StartupTrace.Write($"LocalizationHelper: SetRuntimeLanguage to {tag}");
        
        // 1. Set the OS-level override (best effort)
        try { ApplicationLanguages.PrimaryLanguageOverride = tag; } catch { }

        // 2. Set our internal context (reliable for unpackaged apps)
        if (_manager == null) GetResourceSystem();
        ApplyLanguageToContext(tag);
    }

    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    /// <summary>
    /// Returns the localized string for <paramref name="key"/>, or
    /// <paramref name="fallback"/> when the key is missing or resolves to an
    /// empty value. Useful for new code that ships strings ahead of formal
    /// translation — the fallback ensures the UI never displays a blank label.
    /// </summary>
    public static string GetString(string key, string fallback)
    {
        var resolved = GetString(key);
        return string.IsNullOrEmpty(resolved) ? fallback : resolved;
    }

    public static string GetEffectiveLanguageTag()
    {
        // Prioritize our internal tracked language for unpackaged app reliability
        if (!string.IsNullOrEmpty(_currentLanguage) && _currentLanguage != DefaultLanguage)
        {
            return _currentLanguage;
        }

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
