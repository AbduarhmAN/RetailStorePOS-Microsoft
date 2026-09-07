using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;

namespace RetailStorePOS.UI.Common;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private ResourceLoader? _loader;

    private LocalizationService()
    {
        try
        {
            _loader = new ResourceLoader();
        }
        catch { }
    }

    private string _manualLanguageOverride = "en-US";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public FlowDirection CurrentFlowDirection
    {
        get
        {
            return LocalizationHelper.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }

    public FlowDirection FlowDirection => CurrentFlowDirection;

    public string GreetingText => _loader?.GetString("Greeting/Text") ?? "";

    public string this[string key] => LocalizationHelper.GetString(key);

    public void SetLanguage(string languageTag)
    {
        _manualLanguageOverride = languageTag;
        LocalizationHelper.SetRuntimeLanguage(languageTag);
        
        try
        {
            _loader = new ResourceLoader();
        }
        catch { }
        
        OnPropertyChanged(nameof(GreetingText));
        OnPropertyChanged(nameof(CurrentFlowDirection));
    }
}
