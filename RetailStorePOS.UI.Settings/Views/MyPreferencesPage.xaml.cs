using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.UI.Settings.ViewModels;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class MyPreferencesPage : Page
{
    public SettingsViewModel ViewModel { get; private set; } = null!;

    public MyPreferencesPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }
}
