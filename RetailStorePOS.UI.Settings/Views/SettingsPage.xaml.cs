using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Products.Views;
using RetailStorePOS.UI.Reporting.Views;
using RetailStorePOS.UI.Sales.Views;
using RetailStorePOS.UI.Settings.ViewModels;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isSyncingNavigationSelection;
    private string? _pendingInitialTag;

    public bool ShowAllNavigationItems { get; set; }

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel();
        InitializeComponent();
        ApplyNavigationText();
        SettingsContentFrame.CacheSize = 8;
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;

        ViewModel.Loc.PropertyChanged += OnLocalizationChanged;
    }

    private void ApplyNavigationText()
    {
        CheckoutNavButton.Content = ViewModel.SettingsPage_Nav_Checkout;
        ProductsNavButton.Content = ViewModel.SettingsPage_Nav_Products;
        ReportsNavButton.Content = ViewModel.SettingsPage_Nav_Reports;
        SettingsRootNavButton.Content = ViewModel.SettingsPage_Nav_Settings;
        StoreNavButton.Content = ViewModel.SettingsPage_Nav_Store;
        TaxNavButton.Content = ViewModel.SettingsPage_Nav_Tax;
        UsersNavButton.Content = ViewModel.SettingsPage_Nav_Users;
        PrefsNavButton.Content = ViewModel.SettingsPage_Nav_Prefs;
        LicenseNavButton.Content = LocalizationHelper.GetString("LicensePage_Title");
        AboutNavButton.Content = ViewModel.SettingsPage_Nav_About;
        SignOutNavButton.Content = ViewModel.SettingsPage_Nav_SignOut;
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
            ApplyNavigationText();
            Bindings.Update();
        });
    }

    private void UpdateNavigationAccess()
    {
        var mustChangePassword = LoginRuntime.Auth.CurrentUser?.MustChangePassword == true;
        var isAuthenticated = LoginRuntime.Auth.IsLoggedIn;
        var showAllPages = ShowAllNavigationItems;

        CheckoutNavButton.Visibility = showAllPages || (!mustChangePassword && LoginRuntime.Auth.CanCheckout) ? Visibility.Visible : Visibility.Collapsed;
        ProductsNavButton.Visibility = showAllPages || (!mustChangePassword && LoginRuntime.Auth.CanManageProducts) ? Visibility.Visible : Visibility.Collapsed;
        ReportsNavButton.Visibility = showAllPages || (!mustChangePassword && LoginRuntime.Auth.CanViewReports) ? Visibility.Visible : Visibility.Collapsed;
        StoreNavButton.Visibility = showAllPages || (!mustChangePassword && ViewModel.CanManageSettings) ? Visibility.Visible : Visibility.Collapsed;
        TaxNavButton.Visibility = showAllPages || (!mustChangePassword && ViewModel.CanManageSettings) ? Visibility.Visible : Visibility.Collapsed;
        UsersNavButton.Visibility = showAllPages || LoginRuntime.Auth.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        PrefsNavButton.Visibility = showAllPages || !mustChangePassword ? Visibility.Visible : Visibility.Collapsed;
        LicenseNavButton.Visibility = isAuthenticated && !mustChangePassword && LoginRuntime.Authorization.IsAdmin
            ? Visibility.Visible : Visibility.Collapsed;
        AboutNavButton.Visibility = showAllPages || !mustChangePassword ? Visibility.Visible : Visibility.Collapsed;
        SignOutNavButton.Visibility = isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
        SettingsRootNavButton.Visibility = Visibility.Visible;
        SettingsSubNavHost.Visibility =
            StoreNavButton.Visibility == Visibility.Visible ||
            TaxNavButton.Visibility == Visibility.Visible ||
            UsersNavButton.Visibility == Visibility.Visible ||
            PrefsNavButton.Visibility == Visibility.Visible ||
            LicenseNavButton.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
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

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncingNavigationSelection) return;

        if (sender is Button navButton && navButton.Tag is string tag)
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
            try
            {
                LoginRuntime.Authorization.RequireAuthenticated();
                LoginRuntime.Auth.Logout();
            }
            catch (UnauthorizedAccessException)
            {
                // Shell chrome refresh handled by Shell app
            }
            return;
        }

        if (LoginRuntime.Auth.CurrentUser?.MustChangePassword == true &&
            !tag.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            tag = "users";
        }

        var targetNavButton = GetNavButtonForTag(tag);
        if (targetNavButton is null || targetNavButton.Visibility != Visibility.Visible)
        {
            tag = GetFirstAvailableTag();
            targetNavButton = GetNavButtonForTag(tag);
            if (targetNavButton is null || targetNavButton.Visibility != Visibility.Visible)
            {
                StartupTrace.Write($"SettingsPage.NavigateToTag:no-visible-target:{tag ?? "<null>"}");
                return;
            }
        }

        Type? targetPageType = tag switch
        {
            "checkout" => typeof(CheckoutPage),
            "products" => typeof(ProductsPage),
            "reports" => typeof(ReportsPage),
            "store" => typeof(StoreManagementPage),
            "tax" => typeof(TaxConfigurationPage),
            "users" => typeof(UsersPage),
            "prefs" => typeof(MyPreferencesPage),
            "license" => typeof(LicensePage),
            "about" => typeof(AboutPage),
            _ => typeof(StoreManagementPage)
        };

        if (targetPageType is null)
        {
            return;
        }

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

            if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
            {
                SettingsContentFrame.Navigate(targetPageType, parameters);
            }

            TrySyncNavigationSelection(targetNavButton, tag);
            // Route tag set by Shell app
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"SettingsPage.NavigateToTag({tag}) failed: {ex}");
            LoginRuntime.ReportException(ex, $"SettingsPage.NavigateToTag.{tag}");
        }
    }

    private void TrySyncNavigationSelection(Button targetNavButton, string tag)
    {
        _isSyncingNavigationSelection = true;
        try
        {
            foreach (var button in GetNavigationButtons())
            {
                button.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            }

            targetNavButton.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
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

    private Button? GetNavButtonForTag(string? tag)
    {
        return tag switch
        {
            "checkout" => CheckoutNavButton,
            "products" => ProductsNavButton,
            "reports" => ReportsNavButton,
            "store" => StoreNavButton,
            "tax" => TaxNavButton,
            "users" => UsersNavButton,
            "prefs" => PrefsNavButton,
            "license" => LicenseNavButton,
            "about" => AboutNavButton,
            "signout" => SignOutNavButton,
            _ => null
        };
    }

    private Button[] GetNavigationButtons()
    {
        return new[]
        {
            CheckoutNavButton,
            ProductsNavButton,
            ReportsNavButton,
            SettingsRootNavButton,
            StoreNavButton,
            TaxNavButton,
            UsersNavButton,
            PrefsNavButton,
            LicenseNavButton,
            AboutNavButton,
            SignOutNavButton
        };
    }
}
