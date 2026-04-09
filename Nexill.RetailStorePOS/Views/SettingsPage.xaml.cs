using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateNavigationAccess();
        NavigateToTag(ResolveRequestedTag());
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
    }

    private void SettingsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
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
                return;
            }
        }

        _isSyncingNavigationSelection = true;
        try
        {
            SettingsRootNav.IsExpanded = tag is "store" or "tax" or "users" or "prefs";
            SettingsNavView.SelectedItem = targetNavItem;
        }
        finally
        {
            _isSyncingNavigationSelection = false;
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

        if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
        {
            SettingsContentFrame.Navigate(targetPageType, parameters);
        }

        MainWindow.Current?.SetCurrentRouteTag(tag);
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
