using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class UsersPage : Page
{
    private bool _isSyncingSelection;
    private bool _isWaitingToShowAdministratorTip;

    public UsersPageViewModel ViewModel { get; } = new();

    public UsersPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += UsersPage_Loaded;
        Unloaded += UsersPage_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void UsersPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureAdministratorSelectedForOnboarding();
        SyncListSelection();
    }

    private void UsersPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isWaitingToShowAdministratorTip)
        {
            LayoutUpdated -= UsersPage_LayoutUpdated;
            _isWaitingToShowAdministratorTip = false;
        }

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedUser))
        {
            SyncListSelection();
        }
    }

    private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            ViewModel.SelectedUser = UsersListView.SelectedItem as User;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void SyncListSelection()
    {
        if (UsersListView == null)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            if (!Equals(UsersListView.SelectedItem, ViewModel.SelectedUser))
            {
                UsersListView.SelectedItem = ViewModel.SelectedUser;
            }

            if (ViewModel.SelectedUser != null)
            {
                UsersListView.ScrollIntoView(ViewModel.SelectedUser);
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public void ShowAdministratorAccessTeachingTip()
    {
        EnsureAdministratorSelectedForOnboarding();
        SyncListSelection();

        if (!IsAdministratorTipTargetReady())
        {
            QueueAdministratorAccessTeachingTip();
            return;
        }

        AdministratorAccessToggle.UpdateLayout();
        AdministratorAccessTip.IsOpen = false;
        AdministratorAccessTip.Target = AdministratorAccessToggle;
        AdministratorAccessTip.IsOpen = true;
    }

    private void AdministratorAccessTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
    }

    private void EnsureAdministratorSelectedForOnboarding()
    {
        if (!ViewModel.IsOnboardingActive || ViewModel.SelectedUser != null)
        {
            return;
        }

        ViewModel.SelectedUser = ViewModel.Users.FirstOrDefault(user => string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase))
            ?? ViewModel.Users.FirstOrDefault(user => user.IsAdmin);
    }

    private bool IsAdministratorTipTargetReady()
    {
        return IsLoaded &&
               ViewModel.HasSelectedUser &&
               AdministratorAccessToggle.ActualWidth > 0 &&
               AdministratorAccessToggle.ActualHeight > 0;
    }

    private void QueueAdministratorAccessTeachingTip()
    {
        if (_isWaitingToShowAdministratorTip)
        {
            return;
        }

        _isWaitingToShowAdministratorTip = true;
        LayoutUpdated += UsersPage_LayoutUpdated;
    }

    private void UsersPage_LayoutUpdated(object? sender, object e)
    {
        if (!IsAdministratorTipTargetReady())
        {
            return;
        }

        LayoutUpdated -= UsersPage_LayoutUpdated;
        _isWaitingToShowAdministratorTip = false;
        ShowAdministratorAccessTeachingTip();
    }
}


