using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isSyncingNavigationSelection;
    private string? _pendingInitialTag;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel();
        StartupTrace.Write("SettingsPage.ctor:start");
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        StartupTrace.Write("SettingsPage.ctor:end");
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        StartupTrace.Write("SettingsPage.Loaded:start");
        try
        {
            UpdateNavigationAccess();
            NavigateToTag(ResolveRequestedTag());
            StartupTrace.Write("SettingsPage.Loaded:end");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"SettingsPage.Loaded failed: {ex}");
            LoginRuntime.ReportException(ex, "SettingsPage.Loaded");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
    }

    private void UpdateNavigationAccess()
    {
        CheckoutNavItem.Visibility = LoginRuntime.Auth.CanCheckout ? Visibility.Visible : Visibility.Collapsed;
        ProductsNavItem.Visibility = LoginRuntime.Auth.CanManageProducts ? Visibility.Visible : Visibility.Collapsed;
        ReportsNavItem.Visibility = LoginRuntime.Auth.CanViewReports ? Visibility.Visible : Visibility.Collapsed;
        StoreNav.Visibility = ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        TaxNav.Visibility = ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        UsersNav.Visibility = LoginRuntime.Auth.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        PrefsNav.Visibility = Visibility.Visible;
        AboutNavItem.Visibility = Visibility.Visible;
        SignOutNavItem.Visibility = Visibility.Visible;
        SettingsRootNav.Visibility = Visibility.Visible;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartupTrace.Write($"SettingsPage.OnNavigatedTo:start:{e.Parameter}");
        ViewModel.ReloadCommand.Execute(null);
        UpdateNavigationAccess();

        if (e.Parameter is string targetTag && !string.IsNullOrWhiteSpace(targetTag))
        {
            _pendingInitialTag = targetTag;
        }

        if (IsLoaded)
        {
            NavigateToTag(ResolveRequestedTag());
        }

        StartupTrace.Write("SettingsPage.OnNavigatedTo:end");
    }

    private void SettingsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingNavigationSelection) return;

        if (args.SelectedItemContainer is NavigationViewItem navItem && navItem.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    public bool IsNavigationPaneOpen => SettingsNavView.IsPaneOpen;

    public void ToggleNavigationPane()
    {
        SettingsNavView.IsPaneOpen = !SettingsNavView.IsPaneOpen;
    }

    public void RefreshNavigationAccess()
    {
        UpdateNavigationAccess();
    }

    public void NavigateToTag(string? tag)
    {
        StartupTrace.Write($"SettingsPage.NavigateToTag:start:{tag ?? "<null>"}");

        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = GetFirstAvailableTag();
        }

        tag = NormalizeTag(tag);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (tag.Equals("signout", StringComparison.OrdinalIgnoreCase))
        {
            LoginRuntime.Auth.Logout();
            return;
        }

        var targetNavItem = GetNavItemForTag(tag);
        if (targetNavItem is null || targetNavItem.Visibility != Visibility.Visible)
        {
            tag = GetFirstAvailableTag();
            targetNavItem = GetNavItemForTag(tag);
            if (targetNavItem is null || targetNavItem.Visibility != Visibility.Visible)
            {
                StartupTrace.Write($"SettingsPage.NavigateToTag:no-visible-target:{tag ?? "<null>"}");
                return;
            }
        }

        Type targetPageType = tag switch
        {
            "checkout" => typeof(CheckoutPage),
            "products" => typeof(ProductsPage),
            "reports" => typeof(ReportsPage),
            "store" => typeof(StoreManagementPage),
            "tax" => typeof(TaxConfigurationPage),
            "users" => typeof(UsersPage),
            "prefs" => typeof(MyPreferencesPage),
            "about" => typeof(AboutPage),
            _ => typeof(StoreManagementPage)
        };

        object? parameters = targetPageType switch
        {
            var type when type == typeof(StoreManagementPage) => ViewModel,
            var type when type == typeof(TaxConfigurationPage) => ViewModel,
            var type when type == typeof(MyPreferencesPage) => ViewModel,
            _ => null
        };

        try
        {
            SettingsRootNav.IsExpanded = tag is "store" or "tax" or "users" or "prefs";

            if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
            {
                StartupTrace.Write($"SettingsPage.NavigateToTag:navigate:{tag}:{targetPageType.Name}");
                SettingsContentFrame.Navigate(targetPageType, parameters);
            }

            TrySyncNavigationSelection(targetNavItem, tag);
            MainWindow.Current?.SetCurrentRouteTag(tag);
            StartupTrace.Write($"SettingsPage.NavigateToTag:complete:{tag}:{targetPageType.Name}");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"SettingsPage.NavigateToTag({tag}) failed: {ex}");
            LoginRuntime.ReportException(ex, $"SettingsPage.NavigateToTag.{tag}");
        }
    }

    private void TrySyncNavigationSelection(NavigationViewItem targetNavItem, string tag)
    {
        _isSyncingNavigationSelection = true;
        try
        {
            if (!ReferenceEquals(SettingsNavView.SelectedItem, targetNavItem))
            {
                StartupTrace.Write($"SettingsPage.TrySyncNavigationSelection:start:{tag}");
                SettingsNavView.SelectedItem = targetNavItem;
                StartupTrace.Write($"SettingsPage.TrySyncNavigationSelection:end:{tag}");
            }
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"SettingsPage.TrySyncNavigationSelection({tag}) failed: {ex}");
            LoginRuntime.ReportException(ex, $"SettingsPage.TrySyncNavigationSelection.{tag}");
        }
        finally
        {
            _isSyncingNavigationSelection = false;
        }
    }

    public void ShowSubNavigationTeachingTip()
    {
        // Feature disabled
    }

    private void ShowUsersAdministratorAccessTeachingTip()
    {
        if (SettingsContentFrame.Content is UsersPage usersPage)
        {
            usersPage.ShowAdministratorAccessTeachingTip();
        }
    }

    private void SelectNavigation(NavigationViewItem nav)
    {
        NavigateToTag(nav.Tag?.ToString());
    }

    private string ResolveRequestedTag()
    {
        if (!string.IsNullOrWhiteSpace(_pendingInitialTag))
        {
            var pending = _pendingInitialTag;
            _pendingInitialTag = null;
            return pending;
        }

        if (LoginRuntime.Auth.CurrentUser?.Username == "admin" &&
            (!LoginRuntime.Settings.IsOnboardingPhaseCleared() || !LoginRuntime.Settings.IsFirstRunTutorialCleared()))
        {
            return "users";
        }

        if (Frame?.Tag is string frameTag && !string.IsNullOrWhiteSpace(frameTag))
        {
            return frameTag;
        }

        return GetFirstAvailableTag();
    }

    private string NormalizeTag(string tag)
    {
        if (tag.Equals("users_nested_route", StringComparison.OrdinalIgnoreCase))
        {
            return "users";
        }

        if (tag.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("settings-root", StringComparison.OrdinalIgnoreCase))
        {
            return ViewModel.CanManageSettings ? "store" : LoginRuntime.Auth.CanManageUsers ? "users" : "prefs";
        }

        return tag;
    }

    private string GetFirstAvailableTag()
    {
        if (LoginRuntime.Auth.CanCheckout)
        {
            return "checkout";
        }

        if (LoginRuntime.Auth.CanManageProducts)
        {
            return "products";
        }

        if (LoginRuntime.Auth.CanViewReports)
        {
            return "reports";
        }

        if (ViewModel.CanManageSettings)
        {
            return "store";
        }

        if (LoginRuntime.Auth.CanManageUsers)
        {
            return "users";
        }

        return "prefs";
    }

    private NavigationViewItem? GetNavItemForTag(string? tag)
    {
        return tag switch
        {
            "checkout" => CheckoutNavItem,
            "products" => ProductsNavItem,
            "reports" => ReportsNavItem,
            "store" => StoreNav,
            "tax" => TaxNav,
            "users" => UsersNav,
            "prefs" => PrefsNav,
            "about" => AboutNavItem,
            "signout" => SignOutNavItem,
            _ => null
        };
    }
}
