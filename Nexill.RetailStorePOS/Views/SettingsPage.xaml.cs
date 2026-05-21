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
        InitializeComponent();
        SettingsContentFrame.CacheSize = 8;
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        
        ViewModel.Loc.PropertyChanged += OnLocalizationChanged;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateNavigationAccess();
            NavigateToTag(ResolveRequestedTag());
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"SettingsPage.Loaded failed: {ex}");
            LoginRuntime.ReportException(ex, "SettingsPage.Loaded");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Loc.PropertyChanged -= OnLocalizationChanged;
        ViewModel.Dispose();
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Bindings.Update();
        });
    }

    private void UpdateNavigationAccess()
    {
        var mustChangePassword = LoginRuntime.Auth.CurrentUser?.MustChangePassword == true;

        CheckoutNavItem.Visibility = !mustChangePassword && LoginRuntime.Auth.CanCheckout ? Visibility.Visible : Visibility.Collapsed;
        ProductsNavItem.Visibility = !mustChangePassword && LoginRuntime.Auth.CanManageProducts ? Visibility.Visible : Visibility.Collapsed;
        ReportsNavItem.Visibility = !mustChangePassword && LoginRuntime.Auth.CanViewReports ? Visibility.Visible : Visibility.Collapsed;
        StoreNav.Visibility = !mustChangePassword && ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        TaxNav.Visibility = !mustChangePassword && ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        UsersNav.Visibility = LoginRuntime.Auth.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        PrefsNav.Visibility = mustChangePassword ? Visibility.Collapsed : Visibility.Visible;
        AboutNavItem.Visibility = mustChangePassword ? Visibility.Collapsed : Visibility.Visible;
        SignOutNavItem.Visibility = Visibility.Visible;
        SettingsRootNav.Visibility = Visibility.Visible;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
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

        if (LoginRuntime.Auth.CurrentUser?.MustChangePassword == true &&
            !tag.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            tag = "users";
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
            ViewModel.EnsureDataForSection(tag);
            SettingsRootNav.IsExpanded = tag is "store" or "tax" or "users" or "prefs";

            if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
            {
                SettingsContentFrame.Navigate(targetPageType, parameters);
            }

            TrySyncNavigationSelection(targetNavItem, tag);
            MainWindow.Current?.SetCurrentRouteTag(tag);
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
                SettingsNavView.SelectedItem = targetNavItem;
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

        if (LoginRuntime.Auth.CurrentUser?.MustChangePassword == true)
        {
            return "users";
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
        if (LoginRuntime.Auth.CurrentUser?.MustChangePassword == true)
        {
            return "users";
        }

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
