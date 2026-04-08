using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.ViewModels;
using Windows.Foundation;
using Windows.System;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class LoginPage : Page
{
    private const int CashierMode = 0;
    private const int AdminMode = 1;
    private static readonly TimeSpan IndicatorAnimationDuration = TimeSpan.FromMilliseconds(380);
    private static readonly SolidColorBrush SelectedModeBrush = new(Colors.White);
    private static readonly SolidColorBrush UnselectedModeBrush = new(ColorHelper.FromArgb(255, 88, 101, 123));
    private Storyboard? _modeIndicatorStoryboard;
    private bool _isIndicatorTransitionActive;

    public LoginPage()
    {
        StartupTrace.Write("LoginPage.ctor:start");
        ViewModel = new LoginWindowViewModel(LoginRuntime.Auth);
        InitializeComponent();

        StaffListView.ItemsSource = ViewModel.Staff;
        ViewModel.Staff.CollectionChanged += Staff_CollectionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplyMode(GetInitialMode(), focusInput: false, clearStatus: false);
        UpdateCashierContentVisibility();
        Loaded += LoginPage_Loaded;
        Unloaded += LoginPage_Unloaded;
        StartupTrace.Write("LoginPage.ctor:end");
    }

    public LoginWindowViewModel ViewModel { get; }

    private int GetInitialMode()
    {
        return ViewModel.IsSetupMode || ViewModel.Staff.Count == 0 ? AdminMode : CashierMode;
    }

    private void LoginPage_Loaded(object sender, RoutedEventArgs e)
    {
        StartupTrace.Write("LoginPage.Loaded");
        Loaded -= LoginPage_Loaded;
        StartupTrace.Write("LoginPage.Loaded:before UpdateModeIndicator");
        UpdateModeIndicator(animate: false);
        StartupTrace.Write("LoginPage.Loaded:after UpdateModeIndicator");
        StartupTrace.Write("LoginPage.Loaded:before FocusCurrentMode");
        FocusCurrentMode();
        StartupTrace.Write("LoginPage.Loaded:after FocusCurrentMode");
        ShowBootstrapAdminHintIfNeeded();
        UpdateDefaultAdminPasswordTipVisibility();
        UpdateOnboardingHintVisibility();
    }

    private void UpdateOnboardingHintVisibility()
    {
        var isCleared = LoginRuntime.Settings.IsOnboardingPhaseCleared();
        OnboardingHintPanel.Visibility = isCleared ? Visibility.Collapsed : Visibility.Visible;
        UpdateDefaultAdminPasswordTipVisibility();
    }

    private void LoginPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Staff.CollectionChanged -= Staff_CollectionChanged;
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

    private void ModeTabStrip_SizeChanged(object sender, SizeChangedEventArgs e)
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

        AdminPasswordBox.Password = string.Empty;
        PinPasswordBox.Password = string.Empty;

        UpdateModeCopy();
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

        UpdateDefaultAdminPasswordTipVisibility();
        StartupTrace.Write($"LoginPage.ApplyModeComplete:{modeIndex}");
    }

    private void UpdateModeCopy()
    {
        var showCashier = ViewModel.SelectedModeIndex == CashierMode;
        var staffCount = ViewModel.Staff.Count;
        var hasStaff = staffCount > 0;

        ModeTitleText.Text = showCashier
            ? "Cashier sign-in"
            : ViewModel.IsSetupMode ? "Administrator setup" : "Administrator sign-in";

        ModeSubtitleText.Text = showCashier
            ? "Select a cashier and unlock the register with a PIN."
            : ViewModel.IsSetupMode
                ? "Use the administrator account to finish setup before opening the store."
                : "Use an administrator account for users, inventory, reports, and store controls.";

        HeroSupportText.Text = showCashier
            ? "Cashier mode keeps the counter moving."
            : ViewModel.IsSetupMode
                ? "Administrator access is required to finish first-time setup."
                : "Administrator access protects pricing, users, reports, and settings.";

        CashierCountBadgeText.Text = hasStaff
            ? "Ready"
            : "No profiles yet";

        CashierHelperText.Text = hasStaff
            ? "Choose a cashier card, then enter the PIN."
            : "Add an active staff user with a PIN in Users to enable quick cashier sign-in.";

        AdminHelperText.Text = ViewModel.IsSetupMode
            ? "Use the administrator account to finish first-time setup."
            : "Use an administrator account for secure changes and store-wide controls.";

        SetupCallout.Visibility = ViewModel.IsSetupMode ? Visibility.Visible : Visibility.Collapsed;
        SetupCalloutText.Text = hasStaff
            ? "Finish admin setup, then the cashier profiles will be ready for the next shift."
            : "Finish admin setup, then add cashier profiles with PINs.";

        CashierActionButton.Content = "Login";
        AdminActionButton.Content = "Login as admin";
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

        var activeButton = ViewModel.SelectedModeIndex == CashierMode ? CashierTabButton : AdminTabButton;
        if (activeButton.ActualWidth <= 0)
        {
            return;
        }

        var buttonOrigin = activeButton.TransformToVisual(ModeTabStrip).TransformPoint(new Point(0, 0));
        var targetX = buttonOrigin.X + 3;
        var targetWidth = Math.Max(0, activeButton.ActualWidth - 6);

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

        var firstOption = ViewModel.Staff[0];
        SetSelectedStaff(firstOption);
    }

    private void SetSelectedStaff(LoginStaffOption selectedOption)
    {
        foreach (var option in ViewModel.Staff)
        {
            option.IsSelected = ReferenceEquals(option, selectedOption);
        }

        ViewModel.SelectedStaff = selectedOption.User;
    }

    private void ClearSelectedStaff()
    {
        foreach (var option in ViewModel.Staff)
        {
            option.IsSelected = false;
        }

        ViewModel.SelectedStaff = null;
    }

    private LoginStaffOption? GetSelectedStaffOption()
    {
        foreach (var option in ViewModel.Staff)
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
        var hasStaff = ViewModel.Staff.Count > 0;
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

        UpdateModeCopy();
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

    }

    private void RefreshStatus()
    {
        var showCashier = ViewModel.SelectedModeIndex == CashierMode;

        if (ViewModel.HasError)
        {
            StatusInfoBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = showCashier ? "Cashier sign-in failed" : "Admin sign-in failed";
            StatusInfoBar.Message = ViewModel.ErrorMessage;
            return;
        }

        if (ViewModel.IsAuthenticated)
        {
            StatusInfoBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = showCashier ? "Cashier unlocked" : "Administrator signed in";
            StatusInfoBar.Message = ViewModel.SuccessMessage;
            return;
        }

        ClearStatus();
    }

    private void ShowBootstrapAdminHintIfNeeded()
    {
        var hint = LoginRuntime.ConsumeBootstrapAdminHint();
        if (hint is null)
        {
            return;
        }

        ApplyMode(AdminMode, focusInput: false, clearStatus: false);
        UsernameBox.Text = hint.Username;
        ViewModel.Username = hint.Username;
        AdminPasswordBox.Password = string.Empty;

        StatusInfoBar.Visibility = Visibility.Visible;
        StatusInfoBar.IsOpen = true;
        StatusInfoBar.Severity = InfoBarSeverity.Warning;
        StatusInfoBar.Title = "Default administrator created";
        StatusInfoBar.Message = $"Sign in with username '{hint.Username}' and password '{hint.Password}', then change it after setup.";

        AdminPasswordBox.Focus(FocusState.Programmatic);
    }

    private static bool IsDefaultAdminPasswordStillActive()
    {
        var admin = LoginRuntime.Users.GetByUsername("admin");
        return admin is not null
            && admin.IsAdmin
            && admin.IsActive
            && RetailStorePOS.Data.Repositories.UserRepository.VerifyPassword("1234", admin.PasswordHash);
    }

    private void UpdateDefaultAdminPasswordTipVisibility()
    {
        DefaultAdminPasswordTip.IsOpen = false;
    }

    private void ClearStatus()
    {
        StatusInfoBar.IsOpen = false;
        StatusInfoBar.Title = string.Empty;
        StatusInfoBar.Message = string.Empty;
        StatusInfoBar.Visibility = Visibility.Collapsed;
    }
}


