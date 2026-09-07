using System;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace RetailStorePOS.UI.Common;

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

    // Resolved-string cache keyed by resource key for the CURRENT language.
    // MRT resolution probes several namespace paths and each miss throws a
    // caught first-chance COM exception; under a debugger that turns a single
    // Bindings.Update() (dozens of localized x:Binds, repeated per navigation)
    // into a multi-second stall. Caching makes repeat resolutions O(1) and
    // eliminates the repeated throwing probes. Cleared on any language change.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _stringCache = new();

    private static (ResourceManager Manager, ResourceContext Context) GetResourceSystem()
    {
        if (_manager == null)
        {
            try
            {
                // In a packaged (Store) app, accessing Package.Current works.
                var _ = Windows.ApplicationModel.Package.Current;
                System.Diagnostics.Debug.WriteLine("LocalizationHelper: Initializing ResourceManager for PACKAGED Store App");
                _manager = new ResourceManager();
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("LocalizationHelper: Initializing ResourceManager for UNPACKAGED App (Development)");
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
            _stringCache.Clear(); // language changed — cached resolutions are stale
            System.Diagnostics.Debug.WriteLine($"LocalizationHelper: ResourceContext Language Qualifier set to {lang}");
        }

        return (_manager, _context);
    }

    private static void ApplyLanguageToContext(string languageTag)
    {
        _currentLanguage = NormalizeLanguageTag(languageTag);

        // Ensure the manager/context exist
        var (manager, context) = GetResourceSystem();
        context.QualifierValues["Language"] = _currentLanguage;
        _stringCache.Clear(); // language changed — cached resolutions are stale

        System.Diagnostics.Debug.WriteLine($"LocalizationHelper: Applied Language {_currentLanguage} to Context");
    }

    public static readonly (string Tag, string NativeName, string EnglishName, bool IsRtl)[] SupportedLanguages =
    [
        ("en-US", "English", "English", false),
        ("ar-SA", "العربية", "Arabic", true),
    ];

    public static string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key;
        }

        // Ensure the context reflects the current language before serving cache;
        // GetResourceSystem() also clears the cache when the language changed.
        GetResourceSystem();

        if (_stringCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resolved = ResolveStringUncached(key);
        _stringCache[key] = resolved;
        return resolved;
    }

    private static string ResolveStringUncached(string key)
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
                    System.Diagnostics.Debug.WriteLine($"LocalizationHelper.GetString: FALLBACK to English for {key}");
                    return fallbackResult;
                }
            }

            System.Diagnostics.Debug.WriteLine($"LocalizationHelper.GetString: [NOT FOUND] {key}");
            return key;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocalizationHelper.GetString: ERROR for {key}: {ex.Message}");
            return key;
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
        System.Diagnostics.Debug.WriteLine($"LocalizationHelper: SetRuntimeLanguage to {tag}");
        
        // 1. Set the OS-level override (best effort)
        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = tag;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocalizationHelper: PrimaryLanguageOverride failed for {tag}: {ex.Message}");
        }

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
        return string.IsNullOrEmpty(resolved) || string.Equals(resolved, key, StringComparison.Ordinal) ? fallback : resolved;
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
            "arabic" or "العربية" => "ar-SA",
            "english" => DefaultLanguage,
            "en" or "en-us" => DefaultLanguage,
            var value when value.StartsWith("ar-", StringComparison.Ordinal) => "ar-SA",
            "fr" or "fr-fr" or "french" or "francais" or "français" => DefaultLanguage,
            var value when value.StartsWith("fr-", StringComparison.Ordinal) => DefaultLanguage,
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
