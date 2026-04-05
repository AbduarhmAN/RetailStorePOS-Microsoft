using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Views;
using Windows.Graphics;
using WinRT.Interop;

namespace RetailStorePOS.WinUiLogin;

public sealed partial class MainWindow : Window
{
    public static new MainWindow? Current { get; private set; }

    private AppWindow? _appWindow;
    private bool _isSyncingNavigationSelection;
    private bool _isFirstRunTutorialQueued;
    private bool _isFirstRunTutorialRunning;
    private bool _isFirstRunTutorialPaused;
    private bool _isFirstRunTutorialCompletedThisSession;
    private long _shellPaneOpenStateToken = -1;
    private bool _isSettingsSubNavigationTipArmed;
    private bool _lastShellNavigationPaneState;
    private int _firstRunTutorialStepIndex = -1;
    private TutorialStep[]? _firstRunTutorialSteps;

    public MainWindow()
    {
        StartupTrace.Write("MainWindow.ctor:start");
        Current = this;
        InitializeComponent();
        ConfigureWindow();
        _shellPaneOpenStateToken = ShellNavigationView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, ShellNavigationView_PaneOpenStateChanged);
        _lastShellNavigationPaneState = ShellNavigationView.IsPaneOpen;

        RootFrame.Navigated += RootFrame_Navigated;
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), true);

        
        RootGrid.Loaded += MainWindow_Loaded;

        StartupTrace.Write("MainWindow.ctor:end");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Run the heavy database and runtime initialization off the UI thread
            await Task.Run(() => LoginRuntime.Initialize());

            // Initialize the root UI state
            LoginRuntime.Auth.LoginStateChanged += Auth_LoginStateChanged;
            UpdateShellChrome();
            UpdateNavigationAccess();
            RootFrame.Navigate(typeof(LoginPage));

            // Smoothly fade out the splash overlay
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(animation);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, SplashOverlay);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");

            storyboard.Completed += (s, args) =>
            {
                SplashOverlay.Visibility = Visibility.Collapsed;
            };

            storyboard.Begin();
        }
        catch (Exception ex)
        {
            SplashStatusTitle.Text = "Startup Error";
            SplashStatusDetail.Text = ex.Message;
        }
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        TrySetWindowIcon();
        var preferredSize = new SizeInt32(1365, 768);
        _appWindow.Resize(preferredSize);
        CenterWindow(windowId, preferredSize);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 98, 113, 135);
        titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 154, 165, 181);
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 241, 245, 249);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 226, 232, 240);
    }

    private void TrySetWindowIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico");
            if (!File.Exists(iconPath))
            {
                StartupTrace.Write($"MainWindow.TrySetWindowIcon missing icon: {iconPath}");
                return;
            }

            _appWindow.SetIcon(iconPath);
            StartupTrace.Write($"MainWindow.TrySetWindowIcon applied: {iconPath}");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.TrySetWindowIcon failed: {ex.Message}");
            LoginRuntime.ReportException(ex, "MainWindow.TrySetWindowIcon");
        }
    }

    private void CenterWindow(WindowId windowId, SizeInt32 preferredSize)
    {
        if (_appWindow is null)
        {
            return;
        }

        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var width = Math.Min(preferredSize.Width, workArea.Width);
        var height = Math.Min(preferredSize.Height, workArea.Height);
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

        _appWindow.Move(new PointInt32(x, y));
    }

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (LoginRuntime.Auth.CurrentUser is null)
        {
            return;
        }

        if (_isFirstRunTutorialRunning && _firstRunTutorialStepIndex == 0)
        {
            QueueFirstRunTutorialSubNavigationStep();
        }
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_isFirstRunTutorialRunning ||
            _firstRunTutorialStepIndex != 0 ||
            RootFrame.CurrentSourcePageType != typeof(SettingsPage))
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (IsDescendantOf(source, PaneToggleButton) ||
            IsDescendantOf(source, FirstRunTutorialTip))
        {
            return;
        }

        QueueFirstRunTutorialSubNavigationStep();
    }

    private void ShellNavigationView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingNavigationSelection || LoginRuntime.Auth.CurrentUser is null)
        {
            return;
        }

        if (sender is not ListView listView || listView.SelectedItem is not ListViewItem item || item.Tag is not string tag)
        {
            return;
        }

        NavigateToTag(tag);
    }

    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        UpdateShellChrome();

        if (e.SourcePageType == typeof(LoginPage))
        {
            ResetFirstRunTutorialSession();
            RootFrame.Tag = null;
            SyncNavigationSelection(null);
            return;
        }

        if (e.SourcePageType == typeof(CheckoutPage))
        {
            if (!LoginRuntime.Auth.CanCheckout)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            RootFrame.Tag = "checkout";
        }
        else if (e.SourcePageType == typeof(ProductsPage))
        {
            if (!LoginRuntime.Auth.CanManageProducts)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            RootFrame.Tag = "products";
        }
        else if (e.SourcePageType == typeof(UsersPage))
        {
            if (!LoginRuntime.Auth.CanManageUsers)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            RootFrame.Tag = "users";
        }
        else if (e.SourcePageType == typeof(AboutPage))
        {
            RootFrame.Tag = "about";
        }
        else if (e.SourcePageType == typeof(ReportsPage))
        {
            if (!LoginRuntime.Auth.CanViewReports)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            RootFrame.Tag = "reports";
        }
        else if (e.SourcePageType == typeof(SettingsPage))
        {
            RootFrame.Tag = "settings";
        }

        SyncNavigationSelection(RootFrame.Tag as string);

        if (_isSettingsSubNavigationTipArmed &&
            e.SourcePageType == typeof(SettingsPage) &&
            GetCurrentSettingsPage() is SettingsPage settingsPage)
        {
            _isSettingsSubNavigationTipArmed = false;
            DispatcherQueue.TryEnqueue(() => settingsPage.ShowSubNavigationTeachingTip());
        }

        if (_isFirstRunTutorialRunning && RootFrame.CurrentSourcePageType != typeof(SettingsPage))
        {
            PauseFirstRunTutorial();
        }

        MaybeStartFirstRunTutorial();
    }

    private void Auth_LoginStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateShellChrome();
            UpdateNavigationAccess();

            if (LoginRuntime.Auth.CurrentUser is null && RootFrame.CurrentSourcePageType != typeof(LoginPage))
            {
                RootFrame.Tag = null;
                RootFrame.Navigate(typeof(LoginPage));
                return;
            }

            if (LoginRuntime.Auth.CurrentUser?.Username == "admin" && !LoginRuntime.Settings.IsOnboardingPhaseCleared())
            {
                NavigateToTag("settings");
                return;
            }

            if (RootFrame.CurrentSourcePageType == typeof(CheckoutPage) && !LoginRuntime.Auth.CanCheckout)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            if (RootFrame.CurrentSourcePageType == typeof(ProductsPage) && !LoginRuntime.Auth.CanManageProducts)
            {
                NavigateToFirstAvailablePage();
                return;
            }

            if (RootFrame.CurrentSourcePageType == typeof(ReportsPage) && !LoginRuntime.Auth.CanViewReports)
            {
                NavigateToFirstAvailablePage();
                return;
            }


        });
    }

    private void MaybeStartFirstRunTutorial()
    {
        if (_isFirstRunTutorialCompletedThisSession)
        {
            return;
        }

        if (LoginRuntime.Auth.CurrentUser is null)
        {
            return;
        }

        if (RootFrame.CurrentSourcePageType != typeof(SettingsPage))
        {
            return;
        }

        if (LoginRuntime.Settings.IsFirstRunTutorialCleared())
        {
            return;
        }

        var settingsPage = GetCurrentSettingsPage();
        if (settingsPage is null)
        {
            return;
        }

        if (_isFirstRunTutorialPaused && _firstRunTutorialSteps is not null && _firstRunTutorialStepIndex >= 0)
        {
            ResumeFirstRunTutorial();
            return;
        }

        if (_isFirstRunTutorialQueued || _isFirstRunTutorialRunning)
        {
            return;
        }

        var steps = BuildFirstRunTutorialSteps();
        if (steps.Length == 0)
        {
            return;
        }

        _isFirstRunTutorialQueued = true;
        _ = RunFirstRunTutorialAsync(steps);
    }

    private async Task RunFirstRunTutorialAsync(TutorialStep[] steps)
    {
        try
        {
            await Task.Delay(250);

            var settingsPage = GetCurrentSettingsPage();

            if (LoginRuntime.Auth.CurrentUser is null ||
                _isFirstRunTutorialCompletedThisSession ||
                RootFrame.CurrentSourcePageType != typeof(SettingsPage) ||
                LoginRuntime.Settings.IsFirstRunTutorialCleared() ||
                settingsPage is null)
            {
                return;
            }

            _firstRunTutorialSteps = steps;
            _firstRunTutorialStepIndex = 0;
            _isFirstRunTutorialPaused = false;
            _isFirstRunTutorialRunning = true;
            ShowFirstRunTutorialStep(_firstRunTutorialStepIndex);
        }
        finally
        {
            _isFirstRunTutorialQueued = false;
        }
    }

    private void ShowFirstRunTutorialStep(int stepIndex)
    {
        if (_firstRunTutorialSteps is null ||
            stepIndex < 0 ||
            stepIndex >= _firstRunTutorialSteps.Length)
        {
            CompleteFirstRunTutorial();
            return;
        }

        var step = _firstRunTutorialSteps[stepIndex];

        if (!IsTutorialTargetReady(step.Target))
        {
            _isFirstRunTutorialPaused = true;
            _isFirstRunTutorialRunning = false;
            FirstRunTutorialTip.IsOpen = false;
            return;
        }

        step.Target.UpdateLayout();

        FirstRunTutorialTip.IsOpen = false;
        FirstRunTutorialTip.Target = step.Target;
        FirstRunTutorialTip.Title = step.Title;
        FirstRunTutorialTip.Subtitle = step.Subtitle;
        FirstRunTutorialTip.ActionButtonContent = null;
        FirstRunTutorialTip.CloseButtonContent = "Skip";
        FirstRunTutorialTip.PreferredPlacement = step.PreferredPlacement;
        FirstRunTutorialTip.IsOpen = true;
    }

    private void AdvanceFirstRunTutorial()
    {
        if (_firstRunTutorialSteps is null || _firstRunTutorialSteps.Length == 0)
        {
            CompleteFirstRunTutorial();
            return;
        }

        var nextIndex = _firstRunTutorialStepIndex + 1;
        if (nextIndex >= _firstRunTutorialSteps.Length)
        {
            CompleteFirstRunTutorial();
            return;
        }

        _firstRunTutorialStepIndex = nextIndex;
        ShowFirstRunTutorialStep(_firstRunTutorialStepIndex);
    }

    private void CompleteFirstRunTutorial()
    {
        FirstRunTutorialTip.IsOpen = false;
        _isFirstRunTutorialRunning = false;
        _isFirstRunTutorialPaused = false;
        _isFirstRunTutorialQueued = false;
        _isFirstRunTutorialCompletedThisSession = true;
        _firstRunTutorialStepIndex = -1;
        _firstRunTutorialSteps = null;
    }

    private void FirstRunTutorialTip_CloseButtonClick(TeachingTip sender, object args)
    {
        CompleteFirstRunTutorial();
    }

    private TutorialStep[] BuildFirstRunTutorialSteps()
    {
        return
        [
            new TutorialStep(
                PaneToggleButton,
                "Main navigation",
                "Use this button to open and close the main app shell navigation.",
                TeachingTipPlacementMode.Bottom)
        ];
    }

    private void PauseFirstRunTutorial()
    {
        if (!_isFirstRunTutorialRunning && !_isFirstRunTutorialQueued)
        {
            return;
        }

        _isFirstRunTutorialPaused = true;
        _isFirstRunTutorialRunning = false;
        FirstRunTutorialTip.IsOpen = false;
    }

    private void ResumeFirstRunTutorial()
    {
        if (_firstRunTutorialSteps is null || _firstRunTutorialStepIndex < 0)
        {
            return;
        }

        if (!IsTutorialTargetReady(_firstRunTutorialSteps[_firstRunTutorialStepIndex].Target))
        {
            return;
        }

        _isFirstRunTutorialPaused = false;
        _isFirstRunTutorialRunning = true;
        ShowFirstRunTutorialStep(_firstRunTutorialStepIndex);
    }

    public void TryResumeFirstRunTutorial()
    {
        if (_isFirstRunTutorialPaused)
        {
            ResumeFirstRunTutorial();
        }
    }

    private void QueueFirstRunTutorialSubNavigationStep()
    {
        FirstRunTutorialTip.IsOpen = false;
        _isFirstRunTutorialRunning = false;
        _isFirstRunTutorialPaused = false;
        _isFirstRunTutorialQueued = false;
        _isFirstRunTutorialCompletedThisSession = true;
        _isSettingsSubNavigationTipArmed = true;
        _lastShellNavigationPaneState = ShellNavigationView.IsPaneOpen;
        _firstRunTutorialStepIndex = -1;
        _firstRunTutorialSteps = null;
    }

    private SettingsPage? GetCurrentSettingsPage()
    {
        return RootFrame.Content as SettingsPage;
    }

    private void ResetFirstRunTutorialSession()
    {
        _isFirstRunTutorialQueued = false;
        _isFirstRunTutorialRunning = false;
        _isFirstRunTutorialPaused = false;
        _isFirstRunTutorialCompletedThisSession = false;
        _isSettingsSubNavigationTipArmed = false;
        _lastShellNavigationPaneState = ShellNavigationView.IsPaneOpen;
        _firstRunTutorialStepIndex = -1;
        _firstRunTutorialSteps = null;
        FirstRunTutorialTip.IsOpen = false;
    }

    private void ShellNavigationView_PaneOpenStateChanged(DependencyObject sender, DependencyProperty dp)
    {
        HandleShellNavigationPaneStateChanged(false);
    }

    private void HandleShellNavigationPaneStateChanged(bool isOpen)
    {
        if (_isSettingsSubNavigationTipArmed &&
            _lastShellNavigationPaneState &&
            !isOpen &&
            GetCurrentSettingsPage() is SettingsPage settingsPage)
        {
            _isSettingsSubNavigationTipArmed = false;
            DispatcherQueue.TryEnqueue(() => settingsPage.ShowSubNavigationTeachingTip());
        }

        _lastShellNavigationPaneState = isOpen;
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static bool IsTutorialTargetReady(FrameworkElement? target)
    {
        return target is not null &&
               target.IsLoaded &&
               target.Visibility == Visibility.Visible &&
               target.ActualWidth > 0 &&
               target.ActualHeight > 0;
    }

    private void NavigateToTag(string tag)
    {
        var previousTag = RootFrame.Tag as string;
        RootFrame.Tag = tag;

        switch (tag)
        {
            case "checkout":
                if (!LoginRuntime.Auth.CanCheckout)
                {
                    NavigateToFirstAvailablePage();
                    return;
                }

                if (!TryNavigate(typeof(CheckoutPage), "MainWindow.NavigateToTag.checkout"))
                {
                    RootFrame.Tag = previousTag;
                    return;
                }
                break;

            case "products":
                if (!LoginRuntime.Auth.CanManageProducts)
                {
                    NavigateToFirstAvailablePage();
                    return;
                }

                if (!TryNavigate(typeof(ProductsPage), "MainWindow.NavigateToTag.products"))
                {
                    RootFrame.Tag = previousTag;
                    return;
                }
                break;

            case "signout":
                LoginRuntime.Auth.Logout();
                return;

            case "about":
                if (!TryNavigate(typeof(AboutPage), "MainWindow.NavigateToTag.about"))
                {
                    RootFrame.Tag = previousTag;
                    return;
                }
                break;

            case "reports":
                if (!LoginRuntime.Auth.CanViewReports)
                {
                    NavigateToFirstAvailablePage();
                    return;
                }

                if (!TryNavigate(typeof(ReportsPage), "MainWindow.NavigateToTag.reports"))
                {
                    RootFrame.Tag = previousTag;
                    return;
                }
                break;



            case "settings":
                if (!TryNavigate(typeof(SettingsPage), "MainWindow.NavigateToTag.settings"))
                {
                    RootFrame.Tag = previousTag;
                    return;
                }
                break;
        }

        SyncNavigationSelection(tag);
    }

    private void SyncNavigationSelection(string? tag)
    {
        _isSyncingNavigationSelection = true;

        try
        {
            // ListView doesn't expose easy named items without x:Name, assuming selection changed already updated it.
        }
        finally
        {
            _isSyncingNavigationSelection = false;
        }
    }

    private bool TryNavigate(Type pageType, string operationName)
    {
        if (RootFrame.CurrentSourcePageType == pageType)
        {
            return true;
        }

        try
        {
            RootFrame.Navigate(pageType);
            return true;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"{operationName} failed: {ex.Message}");
            LoginRuntime.ReportException(ex, operationName);
            return false;
        }
    }

    private void UpdateShellChrome()
    {
        if (LoginRuntime.Auth.CurrentUser is not { } user)
        {
            PaneToggleButton.Visibility = Visibility.Collapsed;
            UserBadge.Visibility = Visibility.Collapsed;
            UserNameText.Text = string.Empty;
            UserPicture.Initials = string.Empty;
            return;
        }

        PaneToggleButton.Visibility = Visibility.Visible;
        UserBadge.Visibility = Visibility.Visible;

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        UserNameText.Text = displayName;
        UserPicture.Initials = BuildInitials(displayName);
    }

    private void UpdateNavigationAccess()
    {
        CheckoutNavItem.Visibility = LoginRuntime.Auth.CanCheckout
            ? Visibility.Visible
            : Visibility.Collapsed;

        ProductsNavItem.Visibility = LoginRuntime.Auth.CanManageProducts
            ? Visibility.Visible
            : Visibility.Collapsed;

        ReportsNavItem.Visibility = LoginRuntime.Auth.CanViewReports
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void NavigateToFirstAvailablePage()
    {
        if (LoginRuntime.Auth.CurrentUser is null)
        {
            return;
        }

        var targetTag = GetFirstAvailableTag();
        if (!string.IsNullOrWhiteSpace(targetTag))
        {
            NavigateToTag(targetTag);
        }
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


        if (LoginRuntime.Auth.CanManageSettings)
        {
            return "settings";
        }

        return "settings";
    }

    private static string BuildInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        if (parts.Length == 0)
        {
            return "?";
        }

        return new string(parts);
    }

    private sealed class TutorialStep
    {
        public TutorialStep(FrameworkElement target, string title, string subtitle, TeachingTipPlacementMode preferredPlacement)
        {
            Target = target;
            Title = title;
            Subtitle = subtitle;
            PreferredPlacement = preferredPlacement;
        }

        public FrameworkElement Target { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public TeachingTipPlacementMode PreferredPlacement { get; }
    }
}


