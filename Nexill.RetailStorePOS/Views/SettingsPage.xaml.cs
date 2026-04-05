using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isWaitingToShowSubNavigationTip;

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

        // Check if we are being forced into the Users tab because the first-run tutorial starts there.
        if (LoginRuntime.Auth.CurrentUser?.Username == "admin" &&
            (!LoginRuntime.Settings.IsOnboardingPhaseCleared() || !LoginRuntime.Settings.IsFirstRunTutorialCleared()))
        {
            SettingsNavView.SelectedItem = UsersNav;
            return;
        }

        // Check if external navigation passed a specific tag (e.g., from MainWindow Router)
        if (this.Frame?.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            if (tag.Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                SettingsNavView.SelectedItem = UsersNav;
                return;
            }
        }

        // Default routing
        if (ViewModel.CanManageSettings)
        {
            SettingsNavView.SelectedItem = StoreNav;
        }
        else
        {
            SettingsNavView.SelectedItem = PrefsNav;
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
        
        // Ensure UsersNav is tied to user management permissions (Onboarding bypasses this intentionally if Admin)
        UsersNav.Visibility = LoginRuntime.Auth.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        
        PrefsNav.Visibility = Visibility.Visible;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ReloadCommand.Execute(null);

        // If parameter was passed directly via Frame Navigation
        if (e.Parameter is string targetTag && !string.IsNullOrWhiteSpace(targetTag))
        {
            if (targetTag.Equals("users_nested_route", StringComparison.OrdinalIgnoreCase) || targetTag.Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                SettingsNavView.SelectedItem = UsersNav;
            }
        }
    }

    private void SettingsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer != null)
        {
            NavigateToInnerPage(args.SelectedItemContainer.Tag?.ToString());
        }
    }

    private void SettingsNavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
         if (args.InvokedItemContainer != null)
         {
             NavigateToInnerPage(args.InvokedItemContainer.Tag?.ToString());
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

        // Note: For UsersPage, we do NOT pass SettingsViewModel because it has its own UsersPageViewModel.
        // For the sub-setting pages, we pass the centralized SettingsViewModel.
        object? parameters = (targetPageType == typeof(UsersPage)) ? null : ViewModel;

        if (SettingsContentFrame.CurrentSourcePageType != targetPageType)
        {
            SettingsContentFrame.Navigate(targetPageType, parameters);
        }
    }

    private void SettingsPaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var wasTeachingTipOpen = SettingsSubNavigationTip.IsOpen;
        SettingsNavView.IsPaneOpen = !SettingsNavView.IsPaneOpen;
        SettingsSubNavigationTip.IsOpen = false;

        if (wasTeachingTipOpen)
        {
            DispatcherQueue.TryEnqueue(ShowUsersAdministratorAccessTeachingTip);
        }
    }

    public void ShowSubNavigationTeachingTip()
    {
        if (!IsSubNavigationTipTargetReady())
        {
            QueueSubNavigationTeachingTip();
            return;
        }

        SettingsPaneToggleButton.UpdateLayout();
        SettingsSubNavigationTip.IsOpen = false;
        SettingsSubNavigationTip.Target = SettingsPaneToggleButton;
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
               SettingsPaneToggleButton.Visibility == Visibility.Visible &&
               SettingsPaneToggleButton.ActualWidth > 0 &&
               SettingsPaneToggleButton.ActualHeight > 0;
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
