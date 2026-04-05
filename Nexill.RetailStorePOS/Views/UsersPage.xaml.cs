using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class UsersPage : Page
{
    private bool _isSyncingSelection;
    private bool _isWaitingToShowAdministratorTip;
    private bool _isWaitingToShowUserActionTip;
    private bool _hasShownAdministratorTip;
    private bool _hasCompletedUserActionTipSequence;
    private bool _isUserActionTipSequenceQueued;
    private int _pendingUserActionTipStepIndex = -1;
    private int _userActionTipStepIndex = -1;

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

        if (ViewModel.IsOnboardingActive && !_hasShownAdministratorTip && !_hasCompletedUserActionTipSequence)
        {
            ShowAdministratorAccessTeachingTip();
        }
    }

    private void UsersPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isWaitingToShowAdministratorTip || _isWaitingToShowUserActionTip)
        {
            LayoutUpdated -= UsersPage_LayoutUpdated;
            _isWaitingToShowAdministratorTip = false;
            _isWaitingToShowUserActionTip = false;
            _pendingUserActionTipStepIndex = -1;
        }

        _isUserActionTipSequenceQueued = false;

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

    private void UsersListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not User clickedUser)
        {
            return;
        }

        TryStartAdministratorActionTipSequence(clickedUser);
    }

    private void UserCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not User clickedUser)
        {
            return;
        }

        e.Handled = TryStartAdministratorActionTipSequence(clickedUser);
    }

    public void ShowAdministratorAccessTeachingTip()
    {
        if (!ViewModel.IsOnboardingActive || _hasShownAdministratorTip || _hasCompletedUserActionTipSequence)
        {
            return;
        }

        EnsureAdministratorSelectedForOnboarding();
        SyncListSelection();
        UsersListView.UpdateLayout();
        var target = GetSelectedUserCardTarget();

        if (!IsAdministratorTipTargetReady(target))
        {
            QueueAdministratorAccessTeachingTip();
            return;
        }

        target!.UpdateLayout();
        AdministratorAccessTip.IsOpen = false;
        AdministratorAccessTip.Target = target;
        AdministratorAccessTip.IsOpen = true;
        _hasShownAdministratorTip = true;
    }

    private void AdministratorAccessTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        _hasShownAdministratorTip = true;
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

    private bool IsAdministratorTipTargetReady(FrameworkElement? target)
    {
        return IsLoaded &&
               ViewModel.HasSelectedUser &&
               target is not null &&
               target.ActualWidth > 0 &&
               target.ActualHeight > 0;
    }

    private void QueueAdministratorAccessTeachingTip()
    {
        if (_isWaitingToShowAdministratorTip)
        {
            return;
        }

        _isWaitingToShowAdministratorTip = true;
        EnsureLayoutUpdatedSubscription();
    }

    private void UsersPage_LayoutUpdated(object? sender, object e)
    {
        if (_isWaitingToShowAdministratorTip)
        {
            var administratorTarget = GetSelectedUserCardTarget();
            if (IsAdministratorTipTargetReady(administratorTarget))
            {
                _isWaitingToShowAdministratorTip = false;
                ShowAdministratorAccessTeachingTip();
            }
        }

        if (_isWaitingToShowUserActionTip && _pendingUserActionTipStepIndex >= 0)
        {
            if (TryResolveUserActionTeachingTipStep(_pendingUserActionTipStepIndex, out var resolvedStepIndex, out var target, out _, out _, out _, out _) &&
                IsUserActionTipTargetReady(target))
            {
                _isWaitingToShowUserActionTip = false;
                ShowUserActionTeachingTip(resolvedStepIndex);
            }
        }

        StopWaitingForLayoutIfIdle();
    }

    private FrameworkElement? GetSelectedUserCardTarget()
    {
        return ViewModel.SelectedUser is null
            ? null
            : UsersListView.ContainerFromItem(ViewModel.SelectedUser) as FrameworkElement;
    }

    private void StartUserActionTeachingTipSequence()
    {
        if (_hasCompletedUserActionTipSequence)
        {
            return;
        }

        ShowUserActionTeachingTip(0);
    }

    private void ShowUserActionTeachingTip(int requestedStepIndex)
    {
        if (_hasCompletedUserActionTipSequence)
        {
            return;
        }

        if (!TryResolveUserActionTeachingTipStep(
                requestedStepIndex,
                out var resolvedStepIndex,
                out var target,
                out var title,
                out var subtitle,
                out var actionButtonContent,
                out var placement))
        {
            CompleteUserActionTeachingTipSequence();
            return;
        }

        if (!IsUserActionTipTargetReady(target))
        {
            QueueUserActionTeachingTip(resolvedStepIndex);
            return;
        }

        _pendingUserActionTipStepIndex = -1;
        _userActionTipStepIndex = resolvedStepIndex;
        target!.UpdateLayout();
        UserEditorActionsTip.IsOpen = false;
        UserEditorActionsTip.Target = target;
        UserEditorActionsTip.Title = title;
        UserEditorActionsTip.Subtitle = subtitle;
        UserEditorActionsTip.ActionButtonContent = actionButtonContent;
        UserEditorActionsTip.PreferredPlacement = placement;
        UserEditorActionsTip.IsOpen = true;
    }

    private void QueueUserActionTeachingTip(int stepIndex)
    {
        _pendingUserActionTipStepIndex = stepIndex;
        if (_isWaitingToShowUserActionTip)
        {
            return;
        }

        _isWaitingToShowUserActionTip = true;
        EnsureLayoutUpdatedSubscription();
    }

    private void UserEditorActionsTip_ActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        ShowUserActionTeachingTip(_userActionTipStepIndex + 1);
    }

    private void UserEditorActionsTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        CompleteUserActionTeachingTipSequence();
    }

    private bool TryResolveUserActionTeachingTipStep(
        int requestedStepIndex,
        out int resolvedStepIndex,
        out FrameworkElement? target,
        out string title,
        out string subtitle,
        out string actionButtonContent,
        out TeachingTipPlacementMode placement)
    {
        for (var stepIndex = requestedStepIndex; stepIndex <= 2; stepIndex++)
        {
            switch (stepIndex)
            {
                case 0 when IsVisibleTarget(NewUserButton):
                    resolvedStepIndex = stepIndex;
                    target = NewUserButton;
                    title = "New user";
                    subtitle = "Use this to create another staff account from the user-management page.";
                    actionButtonContent = "Next";
                    placement = TeachingTipPlacementMode.Bottom;
                    return true;
                case 1 when IsVisibleTarget(UnlockUserButton):
                    resolvedStepIndex = stepIndex;
                    target = UnlockUserButton;
                    title = "Unlock";
                    subtitle = "Use this before editing protected credentials and permission settings.";
                    actionButtonContent = "Next";
                    placement = TeachingTipPlacementMode.Bottom;
                    return true;
                case 2 when IsVisibleTarget(SaveUserChangesButton):
                    resolvedStepIndex = stepIndex;
                    target = SaveUserChangesButton;
                    title = "Save changes";
                    subtitle = "Use this after reviewing the administrator settings and password changes.";
                    actionButtonContent = "Finish";
                    placement = TeachingTipPlacementMode.Top;
                    return true;
            }
        }

        resolvedStepIndex = -1;
        target = null;
        title = string.Empty;
        subtitle = string.Empty;
        actionButtonContent = string.Empty;
        placement = TeachingTipPlacementMode.Bottom;
        return false;
    }

    private bool IsUserActionTipTargetReady(FrameworkElement? target)
    {
        return target is not null &&
               target.Visibility == Visibility.Visible &&
               target.ActualWidth > 0 &&
               target.ActualHeight > 0;
    }

    private static bool IsVisibleTarget(FrameworkElement target)
    {
        return target.Visibility == Visibility.Visible;
    }

    private static bool IsAdministratorUser(User user)
    {
        return user.IsAdmin || string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryStartAdministratorActionTipSequence(User clickedUser)
    {
        if (!Equals(ViewModel.SelectedUser, clickedUser))
        {
            ViewModel.SelectedUser = clickedUser;
            SyncListSelection();
        }

        if (!ViewModel.IsOnboardingActive ||
            !IsAdministratorUser(clickedUser) ||
            _hasCompletedUserActionTipSequence ||
            UserEditorActionsTip.IsOpen ||
            _isUserActionTipSequenceQueued)
        {
            return false;
        }

        AdministratorAccessTip.IsOpen = false;
        _isWaitingToShowAdministratorTip = false;
        StopWaitingForLayoutIfIdle();
        _isUserActionTipSequenceQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _isUserActionTipSequenceQueued = false;
            if (_hasCompletedUserActionTipSequence || UserEditorActionsTip.IsOpen)
            {
                return;
            }

            UpdateLayout();
            NewUserButton.UpdateLayout();
            UnlockUserButton.UpdateLayout();
            SaveUserChangesButton.UpdateLayout();
            StartUserActionTeachingTipSequence();
        });
        return true;
    }

    private void EnsureLayoutUpdatedSubscription()
    {
        LayoutUpdated -= UsersPage_LayoutUpdated;
        LayoutUpdated += UsersPage_LayoutUpdated;
    }

    private void StopWaitingForLayoutIfIdle()
    {
        if (_isWaitingToShowAdministratorTip || _isWaitingToShowUserActionTip)
        {
            return;
        }

        LayoutUpdated -= UsersPage_LayoutUpdated;
    }

    private void CompleteUserActionTeachingTipSequence()
    {
        _hasCompletedUserActionTipSequence = true;
        _userActionTipStepIndex = -1;
        _pendingUserActionTipStepIndex = -1;
        _isWaitingToShowUserActionTip = false;
        UserEditorActionsTip.IsOpen = false;
        StopWaitingForLayoutIfIdle();
    }
}


