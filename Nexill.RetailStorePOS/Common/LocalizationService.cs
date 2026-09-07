using System.ComponentModel;
using Microsoft.UI.Xaml;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Common;

/// <summary>
/// A reactive localization service that allows for live UI updates without app restart.
/// Uses the Singleton pattern for global access.
/// </summary>
public class LocalizationService : ObservableObject
{
    private static readonly LocalizationService _instance = new();
    public static LocalizationService Instance => _instance;

    private LocalizationService()
    {
        // Initial setup
    }

    /// <summary>
    /// Gets a localized string by its key.
    /// Used by {x:Bind Loc[Key]} in XAML.
    /// </summary>
    public string this[string key] => LocalizationHelper.GetString(key);

    /// <summary>
    /// The current flow direction based on the language.
    /// </summary>
    public FlowDirection FlowDirection => LocalizationHelper.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>
    /// The current effective language tag.
    /// </summary>
    public string CurrentLanguage => LocalizationHelper.GetEffectiveLanguageTag();

    /// <summary>
    /// Swaps the language and notifies all listeners to refresh their strings.
    /// </summary>
    /// <param name="languageTag">e.g., "ar-SA" or "en-US"</param>
    public void SetLanguage(string languageTag)
    {
        LocalizationHelper.SetRuntimeLanguage(languageTag);

        // Notify that 'this' (the indexer) and 'FlowDirection' have changed.
        // In XAML, Binding to Loc[key] will re-evaluate when we notify property change for an empty string or the indexer name.
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(string.Empty); // Special case: notifies that ALL properties have changed.
        OnPropertyChanged("Item[]");    // Specifically notifies the indexer.
    }
}
