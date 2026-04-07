using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isWaitingToShowSubNavigationTip;
    private bool _isSyncingNavigationSelection;

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

        if (LoginRuntime.Auth.CurrentUser?.Username == "admin" &&
            (!LoginRuntime.Settings.IsOnboardingPhaseCleared() || !LoginRuntime.Settings.IsFirstRunTutorialCleared()))
        {
            SelectNavigation(UsersNav);
            return;
        }

        if (this.Frame?.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            if (tag.Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                SelectNavigation(UsersNav);
                return;
            }
        }

        if (ViewModel.CanManageSettings)
        {
            SelectNavigation(StoreNav);
        }
        else
        {
            SelectNavigation(PrefsNav);
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_isWaitingToShowSubNavigationTip)
        {
            LayoutUpdated -= SettingsPage_LayoutUpdated;
            _isWaitingToShowSubNavigationTip = false;
        }
    }

    private void UpdateNavigationAccess()
    {
        StoreNav.Visibility = ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        TaxNav.Visibility = ViewModel.CanManageSettings ? Visibility.Visible : Visibility.Collapsed;
        UsersNav.Visibility = LoginRuntime.Auth.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        PrefsNav.Visibility = Visibility.Visible;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ReloadCommand.Execute(null);

        if (e.Parameter is string targetTag && !string.IsNullOrWhiteSpace(targetTag))
        {
            if (targetTag.Equals("users_nested_route", StringComparison.OrdinalIgnoreCase) || targetTag.Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                SelectNavigation(UsersNav);
            }
        }
    }

    private void SettingsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingNavigationSelection || args.SelectedItemContainer is not NavigationViewItem nav)
        {
            return;
        }

        var wasTeachingTipOpen = SettingsSubNavigationTip.IsOpen;
        SettingsSubNavigationTip.IsOpen = false;
        NavigateToInnerPage(nav.Tag?.ToString());

        if (wasTeachingTipOpen)
        {
            DispatcherQueue.TryEnqueue(ShowUsersAdministratorAccessTeachingTip);
        }
    }

    private void NavigateToInnerPage(string? tag)
    {
        Type targetPageType = tag switch
        {
            "store" => typeof(StoreManagementPage),
            "tax" => typeof(TaxConfigurationPage),
            "users" => typeof(UsersPage),
            "prefs" => typeof(MyPreferencesPage),
            _ => typeof(MyPreferencesPage)
        };

        object? parameters = (targetPageType == typeof(UsersPage)) ? null : ViewModel;

        if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
        {
            SettingsContentFrame.Navigate(targetPageType, parameters);
        }
    }

    public void ShowSubNavigationTeachingTip()
    {
        if (!IsSubNavigationTipTargetReady())
        {
            QueueSubNavigationTeachingTip();
            return;
        }

        SettingsNavView.UpdateLayout();
        SettingsSubNavigationTip.IsOpen = false;
        SettingsSubNavigationTip.Target = SettingsNavView;
        SettingsSubNavigationTip.IsOpen = true;
    }

    private void SettingsSubNavigationTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        DispatcherQueue.TryEnqueue(ShowUsersAdministratorAccessTeachingTip);
    }

    private void ShowUsersAdministratorAccessTeachingTip()
    {
        if (SettingsContentFrame.Content is UsersPage usersPage)
        {
            usersPage.ShowAdministratorAccessTeachingTip();
        }
    }

    private bool IsSubNavigationTipTargetReady()
    {
        return IsLoaded &&
               SettingsNavView.Visibility == Visibility.Visible &&
               SettingsNavView.ActualWidth > 0 &&
               SettingsNavView.ActualHeight > 0;
    }

    private void SelectNavigation(NavigationViewItem nav)
    {
        if (nav.Visibility != Visibility.Visible)
        {
            return;
        }

        _isSyncingNavigationSelection = true;
        try
        {
            SettingsNavView.SelectedItem = nav;
        }
        finally
        {
            _isSyncingNavigationSelection = false;
        }

        NavigateToInnerPage(nav.Tag?.ToString());
    }

    private void QueueSubNavigationTeachingTip()
    {
        if (_isWaitingToShowSubNavigationTip)
        {
            return;
        }

        _isWaitingToShowSubNavigationTip = true;
        LayoutUpdated += SettingsPage_LayoutUpdated;
    }

    private void SettingsPage_LayoutUpdated(object? sender, object e)
    {
        if (!IsSubNavigationTipTargetReady())
        {
            return;
        }

        LayoutUpdated -= SettingsPage_LayoutUpdated;
        _isWaitingToShowSubNavigationTip = false;
        ShowSubNavigationTeachingTip();
    }
}
