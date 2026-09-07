using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using RetailStorePOS.UI.Login.ViewModels;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Models;
using RetailStorePOS.UI.Common.Services;
using Windows.Foundation;
using Windows.System;

namespace RetailStorePOS.UI.Login.Views;

public sealed partial class LoginPage : Page
{
    private const int CashierMode = 0;
    private const int AdminMode = 1;
    private static readonly TimeSpan IndicatorAnimationDuration = TimeSpan.FromMilliseconds(380);
    private static readonly SolidColorBrush SelectedModeBrush = new(Colors.White);
    private static readonly SolidColorBrush UnselectedModeBrush = new(ColorHelper.FromArgb(255, 61, 66, 92));
    private Storyboard? _modeIndicatorStoryboard;
    private bool _isIndicatorTransitionActive;
    private bool _cashierPinTipDismissed;
    private TeachingTip CashierPinTip = null!;

    public LoginPage()
    {
        StartupTrace.Write("LoginPage.ctor:start");
        ViewModel = new LoginWindowViewModel(LoginRuntime.Auth);
        this.InitializeComponent();
        InitializeCashierPinTip();

        // Apply RTL/LTR manually to avoid Native AOT boxed enum unmarshaling crash in XAML
        FlowDirection = ViewModel.Loc.FlowDirection;
        ViewModel.Loc.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LocalizationService.FlowDirection) || e.PropertyName == string.Empty)
            {
                FlowDirection = ViewModel.Loc.FlowDirection;
            }
        };

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplyMode(GetInitialMode(), focusInput: false, clearStatus: false);
        UpdateCashierContentVisibility();
        Loaded += LoginPage_Loaded;
        Unloaded += LoginPage_Unloaded;

        StartupTrace.Write("LoginPage.ctor:end");
    }

    public LoginWindowViewModel ViewModel { get; }

    private void InitializeCashierPinTip()
    {
        CashierPinTip = new TeachingTip
        {
            Title = ViewModel.CashierPinTipTitle,
            Subtitle = ViewModel.CashierPinTipSubtitle,
            Target = PinPasswordBox,
            CloseButtonContent = ViewModel.Generic_Close,
            PreferredPlacement = TeachingTipPlacementMode.Left,
            TailVisibility = TeachingTipTailVisibility.Visible,
            PlacementMargin = new Microsoft.UI.Xaml.Thickness(12),
            ShouldConstrainToRootBounds = true,
            IsLightDismissEnabled = false,
            IsOpen = false
        };

        CashierPinTip.CloseButtonClick += CashierPinTip_CloseButtonClick;
        Canvas.SetZIndex(CashierPinTip, 100);
        if (Content is Panel rootPanel)
        {
            rootPanel.Children.Add(CashierPinTip);
        }
    }

    private int GetInitialMode()
    {
        if (LoginRuntime.Auth.IsLocked && LoginRuntime.Auth.CurrentUser is { } lockedUser)
        {
            return lockedUser.IsAdmin ? AdminMode : CashierMode;
        }

        return !LoginRuntime.IsOnboardingPhaseCleared ? AdminMode : CashierMode;
    }

    private async void LoginPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
        StartupTrace.Write("LoginPage.Loaded");
        Loaded -= LoginPage_Loaded;
        StartupTrace.Write("LoginPage.Loaded:before UpdateModeIndicator");
        UpdateModeIndicator(animate: false);
        StartupTrace.Write("LoginPage.Loaded:after UpdateModeIndicator");
        PrepareLockedSessionUi();
        ShowBootstrapAdminHintIfNeeded();
        UpdateOnboardingHintVisibility();
        FocusCurrentMode();

        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.LoginPage.Initialize");
        }

        PrepareLockedSessionUi();
        UpdateCashierContentVisibility();
        FocusCurrentMode();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in async void handler: {ex.Message}");
        }
    }

    private void UpdateOnboardingHintVisibility()
    {
        var isCleared = LoginRuntime.IsOnboardingPhaseCleared;
        var shouldShow = !isCleared && LoginRuntime.IsBootstrapPasswordChangeStillRequired;
        AdminOnboardingHintPanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
        UpdateFirstRunCashierTipVisibility();
    }

    private void LoginPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Unloaded -= LoginPage_Unloaded;
    }

    private void CashierCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LoginStaffOption option })
        {
            return;
        }

        SetSelectedStaff(option);
        PinPasswordBox.Password = string.Empty;
        PinPasswordBox.Focus(FocusState.Programmatic);
        ClearStatus();
    }

    private void CashierTabButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyMode(CashierMode);
    }

    private void AdminTabButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyMode(AdminMode);
    }

    private void ModeTabStrip_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isIndicatorTransitionActive)
        {
            return;
        }

        UpdateModeIndicator(animate: false);
    }

    private void ApplyMode(int modeIndex, bool focusInput = true, bool clearStatus = true)
    {
        StartupTrace.Write($"LoginPage.ApplyMode:{modeIndex}");
        modeIndex = modeIndex == AdminMode ? AdminMode : CashierMode;
        ViewModel.SelectedModeIndex = modeIndex;

        var showCashier = modeIndex == CashierMode;
        CashierLoginPanel.Visibility = showCashier ? Visibility.Visible : Visibility.Collapsed;
        AdminLoginPanel.Visibility = showCashier ? Visibility.Collapsed : Visibility.Visible;
        CashierActionPanel.Visibility = showCashier ? Visibility.Visible : Visibility.Collapsed;
        AdminActionPanel.Visibility = showCashier ? Visibility.Collapsed : Visibility.Visible;

        AdminPasswordBox.Password = string.Empty;
        PinPasswordBox.Password = string.Empty;

        UpdateModeTabVisuals(modeIndex);
        UpdateModeIndicator(animate: true);

        if (showCashier)
        {
            EnsureCashierSelection();
        }
        else
        {
            ClearSelectedStaff();
        }

        UpdateCashierContentVisibility();

        if (clearStatus)
        {
            ClearStatus();
        }

        if (focusInput)
        {
            FocusCurrentMode();
        }

        UpdateOnboardingHintVisibility();
        StartupTrace.Write($"LoginPage.ApplyModeComplete:{modeIndex}");
    }

    private void UpdateModeTabVisuals(int modeIndex)
    {
        var cashierSelected = modeIndex == CashierMode;

        CashierTabText.Foreground = cashierSelected ? SelectedModeBrush : UnselectedModeBrush;
        CashierTabIcon.Foreground = cashierSelected ? SelectedModeBrush : UnselectedModeBrush;
        AdminTabText.Foreground = cashierSelected ? UnselectedModeBrush : SelectedModeBrush;
        AdminTabIcon.Foreground = cashierSelected ? UnselectedModeBrush : SelectedModeBrush;
    }

    private void UpdateModeIndicator(bool animate)
    {
        if (ModeTabStrip.ActualWidth <= 0)
        {
            return;
        }

        var tabWidth = ModeTabStrip.ActualWidth / 2;
        var targetX = ViewModel.SelectedModeIndex == CashierMode ? 0 : tabWidth;
        var targetWidth = Math.Max(0, tabWidth);

        if (!animate || !IsLoaded)
        {
            StopModeIndicatorAnimation();
            ModeIndicatorTransform.X = targetX;
            ModeIndicator.Width = targetWidth;
            return;
        }

        var currentX = ModeIndicatorTransform.X;
        var currentWidth = ModeIndicator.ActualWidth > 0 ? ModeIndicator.ActualWidth : ModeIndicator.Width;
        if (Math.Abs(currentX - targetX) < 0.5 && Math.Abs(currentWidth - targetWidth) < 0.5)
        {
            return;
        }

        StopModeIndicatorAnimation();
        _isIndicatorTransitionActive = true;

        var movementAnimation = new DoubleAnimation
        {
            To = targetX,
            Duration = new Duration(IndicatorAnimationDuration),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        Storyboard.SetTarget(movementAnimation, ModeIndicatorTransform);
        Storyboard.SetTargetProperty(movementAnimation, "X");

        var widthAnimation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = new Duration(IndicatorAnimationDuration),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        Storyboard.SetTarget(widthAnimation, ModeIndicator);
        Storyboard.SetTargetProperty(widthAnimation, "Width");

        _modeIndicatorStoryboard = new Storyboard();
        _modeIndicatorStoryboard.Children.Add(movementAnimation);
        _modeIndicatorStoryboard.Children.Add(widthAnimation);
        _modeIndicatorStoryboard.Completed += ModeIndicatorStoryboard_Completed;
        _modeIndicatorStoryboard.Begin();
    }

    private void StopModeIndicatorAnimation()
    {
        if (_modeIndicatorStoryboard is not null)
        {
            _modeIndicatorStoryboard.Completed -= ModeIndicatorStoryboard_Completed;
        }

        _modeIndicatorStoryboard?.Stop();
        _modeIndicatorStoryboard = null;
        _isIndicatorTransitionActive = false;
    }

    private void ModeIndicatorStoryboard_Completed(object? sender, object e)
    {
        _isIndicatorTransitionActive = false;
        if (_modeIndicatorStoryboard is not null)
        {
            _modeIndicatorStoryboard.Completed -= ModeIndicatorStoryboard_Completed;
            _modeIndicatorStoryboard = null;
        }
    }

    private void EnsureCashierSelection()
    {
        var existingOption = GetSelectedStaffOption();
        if (existingOption is not null)
        {
            SetSelectedStaff(existingOption);
            return;
        }

        if (ViewModel.Staff.Count == 0)
        {
            ClearSelectedStaff();
            return;
        }

        var firstOption = (LoginStaffOption)ViewModel.Staff[0]!;
        SetSelectedStaff(firstOption);
    }

    private void SetSelectedStaff(LoginStaffOption selectedOption)
    {
        foreach (LoginStaffOption option in ViewModel.Staff)
        {
            option.IsSelected = ReferenceEquals(option, selectedOption);
        }

        ViewModel.SelectedStaff = selectedOption.User;
    }

    private void ClearSelectedStaff()
    {
        foreach (LoginStaffOption option in ViewModel.Staff)
        {
            option.IsSelected = false;
        }

        ViewModel.SelectedStaff = null;
    }

    private LoginStaffOption? GetSelectedStaffOption()
    {
        foreach (LoginStaffOption option in ViewModel.Staff)
        {
            if (option.IsSelected)
            {
                return option;
            }
        }

        return null;
    }

    private void FocusCurrentMode()
    {
        if (ViewModel.SelectedModeIndex == AdminMode)
        {
            if (LoginRuntime.Auth.IsLocked && LoginRuntime.Auth.CurrentUser is { } lockedUser)
            {
                AdminPasswordBox.Focus(FocusState.Programmatic);
                return;
            }

            UsernameBox.Focus(FocusState.Programmatic);
            return;
        }

        if (ViewModel.Staff.Count == 0)
        {
            AdminTabButton.Focus(FocusState.Programmatic);
            return;
        }

        if (ViewModel.SelectedStaff is not null)
        {
            PinPasswordBox.Focus(FocusState.Programmatic);
            return;
        }

        StaffListView.Focus(FocusState.Programmatic);
    }

    private void Staff_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCashierContentVisibility();
    }

    private void UpdateCashierContentVisibility()
    {
        bool hasStaff = ViewModel.Staff.Count > 0;
        StaffListView.Visibility = hasStaff ? Visibility.Visible : Visibility.Collapsed;
        CashierEmptyState.Visibility = hasStaff ? Visibility.Collapsed : Visibility.Visible;
        CashierActionButton.IsEnabled = hasStaff;
        PinPasswordBox.IsEnabled = hasStaff;

        if (!hasStaff)
        {
            ClearSelectedStaff();
            PinPasswordBox.Password = string.Empty;
        }
        else if (ViewModel.SelectedModeIndex == CashierMode && GetSelectedStaffOption() is null)
        {
            EnsureCashierSelection();
        }

        UpdateOnboardingHintVisibility();
    }

    private void PinPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !CashierActionButton.IsEnabled)
        {
            return;
        }

        CashierSignInButton_Click(CashierActionButton, new RoutedEventArgs());
        e.Handled = true;
    }

    private void AdminCredentialBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        AdminSignInButton_Click(AdminActionButton, new RoutedEventArgs());
        e.Handled = true;
    }

    private void AdminSignInButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Username = UsernameBox.Text;
        ViewModel.Password = AdminPasswordBox.Password;
        ViewModel.LoginPasswordCommand.Execute(null);
        RefreshStatus();
    }

    private void CashierSignInButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Pin = PinPasswordBox.Password;
        ViewModel.LoginPinCommand.Execute(null);
        RefreshStatus();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LoginWindowViewModel.IsAuthenticated) or nameof(LoginWindowViewModel.HasError) or nameof(LoginWindowViewModel.SuccessMessage))
        {
            RefreshStatus();
        }
        else if (e.PropertyName is nameof(LoginWindowViewModel.Staff))
        {
            SyncStaffItemsSource();
            UpdateCashierContentVisibility();
        }
    }

    /// <summary>
    /// Programmatically populates the staff card ItemsControl.
    /// Binding a .NET collection to <c>ItemsControl.ItemsSource</c> via x:Bind
    /// crashes in the CsWinRT ABI layer (<c>IItemsControlMethods.set_ItemsSource</c>
    /// throws <c>ArgumentException</c>, which aborts the whole compiled binding pass
    /// and blanks the page). Adding items directly to <c>ItemsControl.Items</c>
    /// sidesteps the collection projection. Same fix as CheckoutPage.ReceiptItemsControl.
    /// </summary>
    private void SyncStaffItemsSource()
    {
        try
        {
            StaffListView.Items.Clear();
            var items = ViewModel.Staff;
            for (var i = 0; i < items.Count; i++)
            {
                StaffListView.Items.Add(items[i]);
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.LoginPage.SyncStaffItemsSource");
        }
    }

    private void RefreshStatus()
    {
        var showCashier = ViewModel.SelectedModeIndex == CashierMode;

        if (ViewModel.HasError)
        {
            StatusInfoBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Message = ViewModel.ErrorMessage;
            return;
        }

        if (ViewModel.IsAuthenticated)
        {
            StatusInfoBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Message = ViewModel.SuccessMessage;
            return;
        }

        ClearStatus();
    }

    private void ShowBootstrapAdminHintIfNeeded()
    {
        var hint = LoginRuntime.ConsumeBootstrapAdminHint();
        if (hint is null || !LoginRuntime.IsBootstrapPasswordChangeStillRequired)
        {
            return;
        }

        ViewModel.SetBootstrapAdminHint(hint);
        ApplyMode(AdminMode, focusInput: false, clearStatus: false);
        UsernameBox.Text = hint.Username;
        ViewModel.Username = hint.Username;
        AdminPasswordBox.Password = string.Empty;

        StatusInfoBar.Visibility = Visibility.Visible;
        StatusInfoBar.IsOpen = true;
        StatusInfoBar.Severity = InfoBarSeverity.Warning;
        StatusInfoBar.Title = LocalizationHelper.GetString("LoginPage_Status_DefaultAdminCreated");
        StatusInfoBar.Message = string.Format(
            LocalizationHelper.GetString("LoginPage_Status_DefaultAdminMessage"),
            hint.Username,
            hint.Password,
            hint.Pin);

        AdminOnboardingHintPanel.Visibility = Visibility.Visible;

        AdminPasswordBox.Focus(FocusState.Programmatic);
    }

    private void UpdateFirstRunCashierTipVisibility()
    {
        var isCleared = LoginRuntime.IsOnboardingPhaseCleared;
        var shouldShow = ViewModel.SelectedModeIndex == CashierMode &&
                         !isCleared &&
                         LoginRuntime.IsBootstrapPasswordChangeStillRequired &&
                         !_cashierPinTipDismissed;
        CashierPinTip.IsOpen = shouldShow;
    }

    private void CashierPinTip_CloseButtonClick(TeachingTip sender, object args)
    {
        _cashierPinTipDismissed = true;
        sender.IsOpen = false;
    }

    private void PrepareLockedSessionUi()
    {
        if (!LoginRuntime.Auth.IsLocked || LoginRuntime.Auth.CurrentUser is not { } lockedUser)
        {
            return;
        }

        CashierTabButton.IsEnabled = false;
        AdminTabButton.IsEnabled = false;

        if (lockedUser.IsAdmin)
        {
            ApplyMode(AdminMode, focusInput: false, clearStatus: false);
            UsernameBox.Text = lockedUser.Username;
            ViewModel.Username = lockedUser.Username;
            UsernameBox.IsEnabled = false;
            return;
        }

        ApplyMode(CashierMode, focusInput: false, clearStatus: false);
        StaffListView.IsEnabled = false;

        foreach (LoginStaffOption option in ViewModel.Staff)
        {
            if (option.User.Id == lockedUser.Id)
            {
                SetSelectedStaff(option);
                return;
            }
        }
    }

    private void ClearStatus()
    {
        StatusInfoBar.IsOpen = false;
        StatusInfoBar.Title = string.Empty;
        StatusInfoBar.Message = string.Empty;
        StatusInfoBar.Visibility = Visibility.Collapsed;
    }
}
