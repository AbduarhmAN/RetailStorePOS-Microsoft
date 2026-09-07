using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RetailStorePOS.Data.Models;
using RetailStorePOS.UI.Settings.ViewModels;
using System.Collections.Specialized;
using System.Collections;
using Microsoft.UI.Dispatching;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class UsersPage : Page
{
    private enum UsersOnboardingStep
    {
        None,
        AdminRowTipOpen,
        WaitingForNewUserTip,
        NewUserTipOpen,
        WaitingForUnlockTip,
        UnlockTipOpen,
        WaitingForSaveTip,
        SaveTipOpen,
        Completed
    }

    private bool _isSyncingSelection;
    private bool _isWaitingToShowAdministratorTip;
    private bool _isAdministratorSelectionRequested;
    private UsersOnboardingStep _usersOnboardingStep = UsersOnboardingStep.None;

    public UsersPageViewModel ViewModel { get; } = new();
    private readonly DispatcherQueue _uiDispatcher;

    public UsersPage()
    {
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += UsersPage_Loaded;
        Unloaded += UsersPage_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void UsersPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Users.CollectionChanged += Users_CollectionChanged;
        SyncCollectionToItemsControl(ViewModel.Users, UsersListView.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        EnsureAdministratorSelectedForOnboarding();
        SyncListSelection();

        if (ViewModel.IsOnboardingActive && _usersOnboardingStep == UsersOnboardingStep.None)
        {
            ShowAdministratorAccessTeachingTip();
        }
    }

    private void UsersPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Users.CollectionChanged -= Users_CollectionChanged;

        if (_isWaitingToShowAdministratorTip)
        {
            LayoutUpdated -= UsersPage_LayoutUpdated;
            _isWaitingToShowAdministratorTip = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedUser))
        {
            SyncListSelection();
            QueueUsersOnboardingEvaluation();
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsOnboardingActive) ||
            e.PropertyName == nameof(ViewModel.HasSelectedUserVisibility) ||
            e.PropertyName == nameof(ViewModel.IsUnlockRequiredVisibility))
        {
            QueueUsersOnboardingEvaluation();
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

        QueueUsersOnboardingEvaluation();
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

    private void UserCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not User clickedUser)
        {
            return;
        }

        _isAdministratorSelectionRequested = true;

        if (!Equals(ViewModel.SelectedUser, clickedUser))
        {
            ViewModel.SelectedUser = clickedUser;
            SyncListSelection();
        }

        QueueUsersOnboardingEvaluation();
    }

    private void EditorPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearAdminPasswordValidation();
    }

    private void Users_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.Users, UsersListView.Items, e));

    private void SyncCollectionToItemsControl(IList source, ItemCollection target, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    target.Remove(item);
                }
            }
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (e.NewStartingIndex >= 0 && e.NewStartingIndex < target.Count)
                    {
                        target.Insert(e.NewStartingIndex, item);
                    }
                    else
                    {
                        target.Add(item);
                    }
                }
            }
        }
    }

    public void ShowAdministratorAccessTeachingTip()
    {
        if (!ViewModel.IsOnboardingActive || _usersOnboardingStep == UsersOnboardingStep.Completed)
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
        CloseAllUsersOnboardingTips();
        AdministratorAccessTip.Target = target;
        AdministratorAccessTip.IsOpen = true;
        _usersOnboardingStep = UsersOnboardingStep.AdminRowTipOpen;
    }

    private void AdministratorAccessTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        CompleteUsersOnboardingSequence();
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

        EvaluateUsersOnboardingState();

        StopWaitingForLayoutIfIdle();
    }

    private FrameworkElement? GetSelectedUserCardTarget()
    {
        return ViewModel.SelectedUser is null
            ? null
            : UsersListView.ContainerFromItem(ViewModel.SelectedUser) as FrameworkElement;
    }

    private void NewUserTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        _usersOnboardingStep = UsersOnboardingStep.WaitingForUnlockTip;
        QueueUsersOnboardingEvaluation();
    }

    private void UnlockTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        _usersOnboardingStep = UsersOnboardingStep.WaitingForSaveTip;
        EnsureSaveChangesButtonVisible();
        QueueUsersOnboardingEvaluation();
    }

    private void SaveChangesTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        CompleteUsersOnboardingSequence();
    }

    private void UsersWalkthroughTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        CompleteUsersOnboardingSequence();
    }

    private void QueueUsersOnboardingEvaluation()
    {
        if (_usersOnboardingStep == UsersOnboardingStep.Completed || !ViewModel.IsOnboardingActive)
        {
            return;
        }

        EnsureLayoutUpdatedSubscription();
        DispatcherQueue.TryEnqueue(() =>
        {
            EvaluateUsersOnboardingState();
            StopWaitingForLayoutIfIdle();
        });
    }

    private void EvaluateUsersOnboardingState()
    {
        if (!ViewModel.IsOnboardingActive || _usersOnboardingStep == UsersOnboardingStep.Completed)
        {
            return;
        }

        if (_usersOnboardingStep != UsersOnboardingStep.None &&
            _usersOnboardingStep != UsersOnboardingStep.AdminRowTipOpen &&
            (ViewModel.SelectedUser is not User selectedUser || !IsAdministratorUser(selectedUser)))
        {
            CompleteUsersOnboardingSequence();
            return;
        }

        if (_usersOnboardingStep == UsersOnboardingStep.AdminRowTipOpen && _isAdministratorSelectionRequested)
        {
            if (ViewModel.SelectedUser is not null && !IsAdministratorUser(ViewModel.SelectedUser))
            {
                _isAdministratorSelectionRequested = false;
                return;
            }

            if (IsSelectedAdministratorReadyForWalkthrough())
            {
                _isAdministratorSelectionRequested = false;
                AdministratorAccessTip.IsOpen = false;
                _usersOnboardingStep = UsersOnboardingStep.WaitingForNewUserTip;
            }
        }

        if (_usersOnboardingStep == UsersOnboardingStep.WaitingForNewUserTip &&
            IsWalkthroughTargetReady(NewUserButton))
        {
            ShowNewUserTeachingTip();
            return;
        }

        if (_usersOnboardingStep == UsersOnboardingStep.WaitingForUnlockTip)
        {
            if (!IsVisibleTarget(UnlockUserButton))
            {
                _usersOnboardingStep = UsersOnboardingStep.WaitingForSaveTip;
            }
            else if (IsWalkthroughTargetReady(UnlockUserButton))
            {
                ShowUnlockTeachingTip();
                return;
            }
        }

        if (_usersOnboardingStep == UsersOnboardingStep.WaitingForSaveTip)
        {
            EnsureSaveChangesButtonVisible();
            if (IsWalkthroughTargetReady(SaveUserChangesButton))
            {
                ShowSaveChangesTeachingTip();
            }
        }
    }

    private static bool IsVisibleTarget(FrameworkElement target)
    {
        return target.Visibility == Visibility.Visible;
    }

    private static bool IsAdministratorUser(User user)
    {
        return user.IsAdmin || string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSelectedAdministratorReadyForWalkthrough()
    {
        return ViewModel.SelectedUser is User selectedUser &&
               IsAdministratorUser(selectedUser) &&
               ViewModel.HasSelectedUser &&
               IsWalkthroughTargetReady(NewUserButton);
    }

    private bool IsWalkthroughTargetReady(FrameworkElement target)
    {
        return IsLoaded &&
               ViewModel.HasSelectedUser &&
               target.Visibility == Visibility.Visible &&
               target.ActualWidth > 0 &&
               target.ActualHeight > 0;
    }

    private void ShowNewUserTeachingTip()
    {
        CloseAllUsersOnboardingTips();
        NewUserButton.UpdateLayout();
        NewUserTeachingTip.IsOpen = true;
        _usersOnboardingStep = UsersOnboardingStep.NewUserTipOpen;
    }

    private void ShowUnlockTeachingTip()
    {
        CloseAllUsersOnboardingTips();
        UnlockUserButton.UpdateLayout();
        UnlockTeachingTip.IsOpen = true;
        _usersOnboardingStep = UsersOnboardingStep.UnlockTipOpen;
    }

    private void ShowSaveChangesTeachingTip()
    {
        CloseAllUsersOnboardingTips();
        SaveUserChangesButton.UpdateLayout();
        SaveChangesTeachingTip.IsOpen = true;
        _usersOnboardingStep = UsersOnboardingStep.SaveTipOpen;
    }

    private void EnsureSaveChangesButtonVisible()
    {
        SaveUserChangesButton.StartBringIntoView();
        UserEditorScrollViewer.UpdateLayout();
    }

    private void CloseAllUsersOnboardingTips()
    {
        AdministratorAccessTip.IsOpen = false;
        NewUserTeachingTip.IsOpen = false;
        UnlockTeachingTip.IsOpen = false;
        SaveChangesTeachingTip.IsOpen = false;
    }

    private void EnsureLayoutUpdatedSubscription()
    {
        LayoutUpdated -= UsersPage_LayoutUpdated;
        LayoutUpdated += UsersPage_LayoutUpdated;
    }

    private void StopWaitingForLayoutIfIdle()
    {
        if (_isWaitingToShowAdministratorTip ||
            _isAdministratorSelectionRequested ||
            _usersOnboardingStep == UsersOnboardingStep.WaitingForNewUserTip ||
            _usersOnboardingStep == UsersOnboardingStep.WaitingForUnlockTip ||
            _usersOnboardingStep == UsersOnboardingStep.WaitingForSaveTip)
        {
            return;
        }

        LayoutUpdated -= UsersPage_LayoutUpdated;
    }

    private void CompleteUsersOnboardingSequence()
    {
        _usersOnboardingStep = UsersOnboardingStep.Completed;
        _isAdministratorSelectionRequested = false;
        _isWaitingToShowAdministratorTip = false;
        CloseAllUsersOnboardingTips();
        StopWaitingForLayoutIfIdle();
    }
}


