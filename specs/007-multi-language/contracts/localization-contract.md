# UI Contract: Localization

## LocalizationHelper API

```csharp
// Nexill.RetailStorePOS/Common/LocalizationHelper.cs
public static class LocalizationHelper
{
    // Singleton ResourceLoader for UI strings
    private static readonly ResourceLoader _loader = new();

    // Supported languages (compile-time list)
    public static readonly (string Tag, string NativeName, string EnglishName, bool IsRtl)[] SupportedLanguages =
    [
        ("en", "English", "English", false),
        ("ar", "العربية", "Arabic", true),
        ("fr", "Français", "French", false),
    ];

    // Get a localized string by key
    public static string GetString(string key) => _loader.GetString(key);

    // Get a localized string with format arguments
    public static string Format(string key, params object[] args)
        => string.Format(_loader.GetString(key), args);

    // Check if the active language is RTL
    public static bool IsRtl
        => Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride == "ar";

    // Default language if none set
    public const string DefaultLanguage = "en";
}
```

## SettingsRepository Extensions

```csharp
// Added to RetailStorePOS.Data/SettingsRepository.cs
public string GetAppLanguage() => GetSetting("app_language", "en");
public void SetAppLanguage(string tag) => SetSetting("app_language", tag);

public string GetStoreLanguage() => GetSetting("store_language", "en");
public void SetStoreLanguage(string tag) => SetSetting("store_language", tag);
```

## SettingsPlatformContract Extensions

```csharp
// Added to RetailStorePOS.Data/Modules/Contracts/SettingsPlatformContract.cs
public const string AppLanguageKey = "app_language";
public const string StoreLanguageKey = "store_language";
```

## App.xaml.cs Startup Contract

```csharp
// In App() constructor, BEFORE InitializeComponent():
var factory = new SqliteConnectionFactory();
var settings = new SettingsRepository(factory);
var lang = settings.GetAppLanguage();

if (!string.IsNullOrEmpty(lang) && lang != "en")
{
    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
}
```

## MainWindow RTL Contract

```csharp
// In MainWindow constructor or Loaded:
if (LocalizationHelper.IsRtl)
{
    Content.FlowDirection = FlowDirection.RightToLeft;
}
```

## Store Management Language Dropdown Contract

```
UI Elements:
├── ComboBox: "Display Language" (app_language)
│   ├── Items: English, العربية, Français
│   ├── SelectedItem: current app_language
│   └── OnChanged: save to settings + show restart InfoBar
└── ComboBox: "Receipt Language" (store_language)
    ├── Items: English, العربية, Français  
    ├── SelectedItem: current store_language
    └── OnChanged: save to settings (immediate, no restart needed)
```
