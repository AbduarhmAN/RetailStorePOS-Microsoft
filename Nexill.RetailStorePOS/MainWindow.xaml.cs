using System.Reflection;
using Microsoft.UI.Dispatching;
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
using Windows.Globalization;
using WinRT.Interop;

using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = new();
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWM_COLOR_NONE = unchecked((int)0x00691B2D); // Color match to #2D1B69
    private const int DWM_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_THICKFRAME = 0x00040000;
    public static new MainWindow? Current { get; private set; }

    private AppWindow? _appWindow;
    private bool _isFirstRunTutorialQueued;
    private bool _isFirstRunTutorialRunning;
    private bool _isFirstRunTutorialPaused;
    private bool _isFirstRunTutorialCompletedThisSession;
    private bool _isSettingsSubNavigationTipArmed;
    private int _firstRunTutorialStepIndex = -1;
    private TutorialStep[]? _firstRunTutorialSteps;
    private bool _isRuntimeBootstrapInProgress;
    private bool _isRuntimeBootstrapComplete;
    private readonly DispatcherQueueTimer _sessionIdleTimer;
    private TimeSpan _sessionIdleTimeout = TimeSpan.FromMinutes(30);
    private string? _lockedRouteTag;
    private bool _isBootstrapPasswordChangeDialogOpen;
    private bool? _cachedHasActiveRegisterSession;
    private bool _isRegisterStatusRefreshInFlight;
    private DateTimeOffset _lastRegisterStatusRefreshAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan RegisterStatusRefreshInterval = TimeSpan.FromSeconds(2);

    public MainWindow()
    {
        StartupTrace.Write("MainWindow.ctor:start");
        Current = this;
        InitializeComponent();
        ConfigureWindow();

        _sessionIdleTimer = DispatcherQueue.CreateTimer();
        _sessionIdleTimer.Interval = TimeSpan.FromSeconds(30);
        _sessionIdleTimer.IsRepeating = true;
        _sessionIdleTimer.Tick += SessionIdleTimer_Tick;

        RootFrame.Navigated += RootFrame_Navigated;
        RootGrid.Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), true);
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

        StartupTrace.Write("MainWindow.ctor:end");
    }

    private static readonly SizeInt32 SplashSize = new(580, 380);
    private static readonly SizeInt32 AppSize = new(1365, 768);

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isRuntimeBootstrapComplete || _isRuntimeBootstrapInProgress)
        {
            return;
        }

        _isRuntimeBootstrapInProgress = true;
        RootGrid.Loaded -= MainWindow_Loaded;
        DispatcherTimer? pbTimer = null;

        try
        {
            // Start the visual progress bar animation
            pbTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            pbTimer.Tick += (s, args) =>
            {
                if (SplashProgressBar.Value < 95) SplashProgressBar.Value += 1.5;
            };
            pbTimer.Start();

            // Run the heavy database and runtime initialization off the UI thread
            await Task.Run(() => LoginRuntime.Initialize());

            if (LoginRuntime.Telemetry is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        StartupTrace.Write("Telemetry.ImportBootstrapLifecycleEvents:start");
                        await LoginRuntime.Telemetry.ImportBootstrapLifecycleEventsAsync();
                        StartupTrace.Write("Telemetry.ImportBootstrapLifecycleEvents:end");
                    }
                    catch (Exception ex)
                    {
                        StartupTrace.Write($"Telemetry Startup Error: {ex.Message}");
                        LoginRuntime.ReportException(ex, "WinUiLogin.Telemetry.StartupSync");
                    }
                });
            }

            // Snap progress bar to 100% immediately so startup is not artificially delayed.
            pbTimer.Stop();
            SplashProgressBar.Value = 100;

            // Hide the window completely so resizing doesn't flash on screen
            _appWindow?.Hide();

            // Prepare the main app UI invisibly
            RootGrid.RequestedTheme = ElementTheme.Light;
            RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 247, 250, 252));
            TransitionToAppWindow();

            // Initialize the root UI state
            LoginRuntime.Auth.LoginStateChanged += Auth_LoginStateChanged;
            _sessionIdleTimeout = TimeSpan.FromMinutes(LoginRuntime.Settings.GetSessionIdleTimeoutMinutes());
            _sessionIdleTimer.Start();
            UpdateShellChrome();
            UpdateNavigationAccess();

            // Prepare the frame to fade in
            RootFrame.Opacity = 0.0;
            RootFrame.Navigate(typeof(LoginPage));
            _ = LoginRuntime.WarmProductSearchIndexAsync();

            // Pop the window back up on screen
            _appWindow?.Show();

            // Fade IN the main login screen
            var fadeInAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            var inStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            inStoryboard.Children.Add(fadeInAnim);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeInAnim, RootFrame);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            inStoryboard.Begin();

            // Clean up splash overlay
            SplashOverlay.Visibility = Visibility.Collapsed;
            _isRuntimeBootstrapComplete = true;
            _ = RunPostLaunchStartupChecksAsync();
        }
        catch (Exception ex)
        {
            pbTimer?.Stop();
            StartupTrace.Write($"MainWindow.Loaded bootstrap failed: {ex}");
            LoginRuntime.ReportException(ex, "WinUiLogin.RuntimeBootstrap");
            SplashStatusTitle.Text = LocalizationHelper.GetString("MainWindow_Status_StartupError");
            SplashStatusDetail.Text = ex.Message;
        }
        finally
        {
            pbTimer?.Stop();
            _isRuntimeBootstrapInProgress = false;
        }
    }

    private async Task RunPostLaunchStartupChecksAsync()
    {
        await Task.Yield();

        try
        {
            var timeValidation = new RetailStorePOS.App.Services.TimeValidationService(LoginRuntime.ConnectionFactory);
            var timeCheckPassed = await timeValidation.IsSystemTimeValidAsync();

            while (!timeCheckPassed)
            {
                var dialog = new RetailStorePOS.WinUiLogin.Views.TimeSyncDialog { XamlRoot = RootGrid.XamlRoot };
                await dialog.ShowAsync();

                if (dialog.Result == RetailStorePOS.WinUiLogin.Views.TimeSyncDialogResult.Continue)
                {
                    if (LoginRuntime.Telemetry is not null)
                    {
                        await LoginRuntime.Telemetry.LogGenericEventAsync("time_warning_ignored", new
                        {
                            local_time = DateTime.UtcNow.ToString("O")
                        });
                    }

                    break;
                }

                timeCheckPassed = await timeValidation.IsSystemTimeValidAsync();
            }

            if (!LoginRuntime.WasRapidReopenAfterUncleanExit)
            {
                return;
            }

            var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();
            if (prefs.SuppressCrashFeedbackPrompt)
            {
                return;
            }

            var crashDialog = new CrashFeedbackDialog { XamlRoot = RootGrid.XamlRoot };
            var result = await crashDialog.ShowAsync();

            if (result == ContentDialogResult.Primary && LoginRuntime.Telemetry is not null)
            {
                await LoginRuntime.Telemetry.LogGenericEventAsync("crash_feedback", new
                {
                    category = crashDialog.SelectedCategory,
                    details = crashDialog.FeedbackDetails
                });
            }

            if (crashDialog.SuppressFuturePrompts)
            {
                await LoginRuntime.LocalPreferences.UpdateSuppressCrashFeedbackAsync(true);
            }
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.RunPostLaunchStartupChecksAsync failed: {ex}");
            LoginRuntime.ReportException(ex, "MainWindow.RunPostLaunchStartupChecksAsync");
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _sessionIdleTimer.Stop();
        _sessionIdleTimer.Tick -= SessionIdleTimer_Tick;
        if (_isRuntimeBootstrapComplete)
        {
            LoginRuntime.Auth.LoginStateChanged -= Auth_LoginStateChanged;
        }
        Current = null;
        Closed -= MainWindow_Closed;
    }

    private void TransitionToAppWindow()
    {
        if (_appWindow is null)
            return;

        // Restore title bar and border
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, true);
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        // Restore the default Windows 11 1px border for the main app
        int styleTrans = GetWindowLong(WindowNative.GetWindowHandle(this), GWL_STYLE);
        SetWindowLong(WindowNative.GetWindowHandle(this), GWL_STYLE, styleTrans | WS_BORDER | WS_THICKFRAME);
        int colorDefault = DWM_COLOR_DEFAULT;
        DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), DWMWA_BORDER_COLOR, ref colorDefault, sizeof(int));
        int doRound = DWMWCP_DEFAULT;
        DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), DWMWA_WINDOW_CORNER_PREFERENCE, ref doRound, sizeof(int));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        AppTitleBar.Visibility = Visibility.Visible;

        // Resize and center to full app size
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow.Resize(AppSize);
        CenterWindow(windowId, AppSize);

        // Restore app title bar colors
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 98, 113, 135);
            titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 154, 165, 181);
            titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 241, 245, 249);
            titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 226, 232, 240);
        }
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        TrySetWindowIcon();

        // Start as a borderless splash card
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Hide the app title bar during splash
        AppTitleBar.Visibility = Visibility.Collapsed;

        // Size the window to exactly the splash card dimensions
        _appWindow.Resize(SplashSize);

        // Remove the default Windows 11 1px border during splash screen
        int styleConfig = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, styleConfig & ~WS_BORDER & ~WS_THICKFRAME);
        int colorNone = DWM_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));
        int doNotRound = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(int));

        CenterWindow(windowId, SplashSize);

        if (LocalizationHelper.IsRtl)
        {
            RootGrid.FlowDirection = FlowDirection.RightToLeft;
        }

        // Bind Splash screen text to MSBuild properties compiled into Assembly metadata
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var copyright = assembly?.GetCustomAttribute<System.Reflection.AssemblyCopyrightAttribute>()?.Copyright;

        var cleanVersion = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Split('+')[0];
        SplashVersionText.Text = string.Format(LocalizationHelper.GetString("Splash_Version_Format"), cleanVersion);
        SplashCopyrightText.Text = string.IsNullOrWhiteSpace(copyright) ? LocalizationHelper.GetString("Splash_Copyright_Default") : copyright;
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

        if (GetCurrentSettingsPage() is not SettingsPage settingsPage)
        {
            return;
        }

        var wasOpen = settingsPage.IsNavigationPaneOpen;
        settingsPage.ToggleNavigationPane();

        if (_isFirstRunTutorialRunning && _firstRunTutorialStepIndex == 0)
        {
            QueueFirstRunTutorialSubNavigationStep();
        }

        if (_isSettingsSubNavigationTipArmed &&
            wasOpen &&
            !settingsPage.IsNavigationPaneOpen)
        {
            _isSettingsSubNavigationTipArmed = false;
            DispatcherQueue.TryEnqueue(() => settingsPage.ShowSubNavigationTeachingTip());
        }
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        RecordUserActivity();

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

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        RecordUserActivity();
    }

    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(LoginPage))
        {
            ResetFirstRunTutorialSession();
            RootFrame.Tag = null;
            UpdateShellChrome();
            return;
        }

        UpdateShellChrome();

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
            StartupTrace.Write("MainWindow.Auth_LoginStateChanged:start");
            try
            {
                UpdateShellChrome();
                UpdateNavigationAccess();

                if (LoginRuntime.Auth.IsLocked)
                {
                    if (RootFrame.CurrentSourcePageType != typeof(LoginPage))
                    {
                        StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-locked-login");
                        _lockedRouteTag ??= RootFrame.Tag as string;
                        RootFrame.Navigate(typeof(LoginPage));
                    }

                    return;
                }

                if (LoginRuntime.Auth.CurrentUser is null && RootFrame.CurrentSourcePageType != typeof(LoginPage))
                {
                    StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-login");
                    _lockedRouteTag = null;
                    RootFrame.Tag = null;
                    RootFrame.Navigate(typeof(LoginPage));
                    return;
                }

                if (IsBootstrapPasswordChangeRequired())
                {
                    StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-bootstrap-password-change");
                    NavigateToTag("users");
                    ShowBootstrapPasswordChangeRequiredDialog();
                    return;
                }

                if (LoginRuntime.Auth.CurrentUser is not null && RootFrame.CurrentSourcePageType == typeof(LoginPage))
                {
                    if (!string.IsNullOrWhiteSpace(_lockedRouteTag))
                    {
                        StartupTrace.Write($"MainWindow.Auth_LoginStateChanged:navigate-locked-route:{_lockedRouteTag}");
                        var lockedRouteTag = _lockedRouteTag;
                        _lockedRouteTag = null;
                        NavigateToTag(lockedRouteTag!);
                        return;
                    }

                    StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-first-available");
                    NavigateToFirstAvailablePage();
                    return;
                }

                GetCurrentSettingsPage()?.RefreshNavigationAccess();
                StartupTrace.Write("MainWindow.Auth_LoginStateChanged:end");
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"MainWindow.Auth_LoginStateChanged failed: {ex}");
                LoginRuntime.ReportException(ex, "MainWindow.Auth_LoginStateChanged");
            }
        });
    }

    private async void MaybeStartFirstRunTutorial()
    {
        if (_isFirstRunTutorialCompletedThisSession ||
            LoginRuntime.Auth.CurrentUser is null ||
            RootFrame.CurrentSourcePageType != typeof(SettingsPage) ||
            LoginRuntime.Settings.IsFirstRunTutorialCleared() ||
            GetCurrentSettingsPage() is not SettingsPage settingsPage)
        {
            return;
        }

        if (_isFirstRunTutorialQueued) return;
        _isFirstRunTutorialQueued = true;
        try
        {
            await Task.Delay(250);

            var currentSettingsPage = GetCurrentSettingsPage();

            if (LoginRuntime.Auth.CurrentUser is null ||
                _isFirstRunTutorialCompletedThisSession ||
                RootFrame.CurrentSourcePageType != typeof(SettingsPage) ||
                LoginRuntime.Settings.IsFirstRunTutorialCleared() ||
                currentSettingsPage is null)
            {
                return;
            }

            _firstRunTutorialSteps = BuildFirstRunTutorialSteps();
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
        FirstRunTutorialTip.CloseButtonContent = LocalizationHelper.GetString("MainWindow_Tutorial_CloseButton.Content");
        FirstRunTutorialTip.PreferredPlacement = step.PreferredPlacement;
        FirstRunTutorialTip.IsOpen = true;
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

    private TutorialStep[] BuildFirstRunTutorialSteps()
    {
        return
        [
            new TutorialStep(
                PaneToggleButton,
                LocalizationHelper.GetString("MainWindow_Tutorial_Step0_Title"),
                LocalizationHelper.GetString("MainWindow_Tutorial_Step0_Subtitle"),
                TeachingTipPlacementMode.Bottom)
        ];
    }

    private void FirstRunTutorialTip_CloseButtonClick(TeachingTip sender, object args)
    {
        CompleteFirstRunTutorial();
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
        _firstRunTutorialStepIndex = -1;
        _firstRunTutorialSteps = null;
        FirstRunTutorialTip.IsOpen = false;
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
        if (tag == "signout")
        {
            LoginRuntime.Auth.Logout();
            return;
        }

        if (IsBootstrapPasswordChangeRequired() &&
            !string.Equals(tag, "users", StringComparison.OrdinalIgnoreCase))
        {
            StartupTrace.Write($"MainWindow.NavigateToTag:blocked-bootstrap-password-change:{tag}");
            tag = "users";
            ShowBootstrapPasswordChangeRequiredDialog();
        }

        RootFrame.Tag = tag;

        if (GetCurrentSettingsPage() is SettingsPage settingsPage)
        {
            settingsPage.NavigateToTag(tag);
            UpdateShellChrome();
            return;
        }

        try
        {
            RootFrame.Navigate(typeof(SettingsPage), tag);
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.NavigateToTag({tag}) failed: {ex}");
            LoginRuntime.ReportException(ex, $"MainWindow.NavigateToTag.{tag}");
        }
    }

    private void UpdateShellChrome()
    {
        ViewModel.IsLoginPage = RootFrame.CurrentSourcePageType == typeof(LoginPage);
        ViewModel.CurrentTag = RootFrame.Tag as string;

        ApplyShellLocalization();

        if (LoginRuntime.Auth.CurrentUser is not { } user)
        {
            PaneToggleButton.Visibility = Visibility.Collapsed;
            UserBadge.Visibility = Visibility.Collapsed;
            CashInOutMenuItem.IsEnabled = false;
            CloseRegisterMenuItem.IsEnabled = false;
            UserNameText.Text = string.Empty;
            RegisterStatusText.Text = string.Empty;
            RegisterStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 148, 163, 184));
            UserPicture.Initials = string.Empty;
            return;
        }

        PaneToggleButton.Visibility = Visibility.Collapsed;
        UserBadge.Visibility = Visibility.Visible;

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.Username
            : user.DisplayName;
        UserNameText.Text = displayName;
        UserPicture.Initials = BuildInitials(displayName);
        UpdateCloseRegisterMenuState();
    }

    private void UserMenuFlyout_Opening(object sender, object e)
    {
        UpdateCloseRegisterMenuState(forceRefresh: true);
    }

    private void UpdateCloseRegisterMenuState(bool forceRefresh = false)
    {
        if (LoginRuntime.Auth.CurrentUser is null)
        {
            _cachedHasActiveRegisterSession = false;
            ApplyRegisterSessionState(hasActiveSession: false);
            return;
        }

        try
        {
            ApplyRegisterSessionState(_cachedHasActiveRegisterSession ?? false);
            QueueRegisterStatusRefresh(forceRefresh);
        }
        catch (Exception ex)
        {
            ApplyRegisterSessionUnavailableState();
            LoginRuntime.ReportException(ex, "MainWindow.UpdateCloseRegisterMenuState");
        }
    }

    private void ApplyRegisterSessionState(bool hasActiveSession)
    {
        CashInOutMenuItem.IsEnabled = hasActiveSession;
        CloseRegisterMenuItem.IsEnabled = hasActiveSession;
        ViewModel.IsRegisterActive = hasActiveSession;
        RegisterStatusText.Text = ViewModel.RegisterStatus;
        RegisterStatusDot.Fill = new SolidColorBrush(
            hasActiveSession
                ? ColorHelper.FromArgb(255, 22, 163, 74)
                : ColorHelper.FromArgb(255, 148, 163, 184));
    }

    private void ApplyRegisterSessionUnavailableState()
    {
        CashInOutMenuItem.IsEnabled = false;
        CloseRegisterMenuItem.IsEnabled = false;
        ViewModel.IsRegisterActive = false;
        RegisterStatusText.Text = LocalizationHelper.GetString("MainWindow_Status_RegisterUnavailable");
        RegisterStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68));
    }

    private void QueueRegisterStatusRefresh(bool forceRefresh)
    {
        if (LoginRuntime.Auth.CurrentUser is not { } user)
        {
            return;
        }

        if (_isRegisterStatusRefreshInFlight)
        {
            return;
        }

        if (!forceRefresh &&
            _cachedHasActiveRegisterSession is not null &&
            _lastRegisterStatusRefreshAt != DateTimeOffset.MinValue &&
            DateTimeOffset.UtcNow - _lastRegisterStatusRefreshAt < RegisterStatusRefreshInterval)
        {
            return;
        }

        _isRegisterStatusRefreshInFlight = true;
        _ = RefreshRegisterStatusAsync(user.Id);
    }

    private async Task RefreshRegisterStatusAsync(long expectedUserId)
    {
        Exception? error = null;
        var hasActiveSession = false;

        try
        {
            hasActiveSession = await Task.Run(() => LoginRuntime.RegisterSessions.GetActiveSession() is not null);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var refreshedAt = DateTimeOffset.UtcNow;
        DispatcherQueue.TryEnqueue(() =>
        {
            _isRegisterStatusRefreshInFlight = false;

            if (LoginRuntime.Auth.CurrentUser?.Id != expectedUserId)
            {
                return;
            }

            if (error is not null)
            {
                ApplyRegisterSessionUnavailableState();
                LoginRuntime.ReportException(error, "MainWindow.RefreshRegisterStatus");
                return;
            }

            _cachedHasActiveRegisterSession = hasActiveSession;
            _lastRegisterStatusRefreshAt = refreshedAt;
            ApplyRegisterSessionState(hasActiveSession);
        });
    }

    private void ApplyShellLocalization()
    {
        Title = LocalizationHelper.GetString("MainWindow_Title");
        RootGrid.FlowDirection = LocalizationHelper.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private async void CashInOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
            if (summary == null)
            {
                UpdateCloseRegisterMenuState(forceRefresh: true);
                return;
            }

            await ShowCashInOutDialogAsync(summary.SessionId);
            UpdateCloseRegisterMenuState(forceRefresh: true);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.CashInOut_Click");
        }
    }

    private async void CloseRegister_Click(object sender, RoutedEventArgs e)
    {
        static (bool HadMinWidth, object? MinWidth, bool HadMaxWidth, object? MaxWidth) PushContentDialogWidth(double width)
        {
            var resources = Application.Current.Resources;
            var hadMinWidth = resources.TryGetValue("ContentDialogMinWidth", out var minWidth);
            var hadMaxWidth = resources.TryGetValue("ContentDialogMaxWidth", out var maxWidth);

            resources["ContentDialogMinWidth"] = width;
            resources["ContentDialogMaxWidth"] = width;

            return (hadMinWidth, minWidth, hadMaxWidth, maxWidth);
        }

        static void PopContentDialogWidth((bool HadMinWidth, object? MinWidth, bool HadMaxWidth, object? MaxWidth) snapshot)
        {
            var resources = Application.Current.Resources;

            if (snapshot.HadMinWidth)
            {
                resources["ContentDialogMinWidth"] = snapshot.MinWidth!;
            }
            else
            {
                resources.Remove("ContentDialogMinWidth");
            }

            if (snapshot.HadMaxWidth)
            {
                resources["ContentDialogMaxWidth"] = snapshot.MaxWidth!;
            }
            else
            {
                resources.Remove("ContentDialogMaxWidth");
            }
        }

        try
        {
            var summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
            if (summary == null)
            {
                UpdateCloseRegisterMenuState(forceRefresh: true);
                return;
            }

            var dialog = new CloseRegisterDialog(summary)
            {
                XamlRoot = this.RootGrid.XamlRoot
            };

            while (true)
            {
                var dialogWidthSnapshot = PushContentDialogWidth(Math.Max(520d, this.RootGrid.XamlRoot.Size.Width * 0.5));
                try
                {
                    await dialog.ShowAsync();
                }
                finally
                {
                    PopContentDialogWidth(dialogWidthSnapshot);
                }

                var result = dialog.ActionResult;
                if (result == ContentDialogResult.Secondary)
                {
                    var changed = await ShowCashInOutDialogAsync(summary.SessionId);
                    if (changed)
                    {
                        summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
                        if (summary == null)
                        {
                            UpdateCloseRegisterMenuState(forceRefresh: true);
                            return;
                        }

                        dialog = new CloseRegisterDialog(summary, dialog.CountedCashText, dialog.Note)
                        {
                            XamlRoot = this.RootGrid.XamlRoot
                        };
                    }

                    continue;
                }

                if (result != ContentDialogResult.Primary)
                {
                    break;
                }

                try
                {
                    LoginRuntime.RegisterSessions.CloseRegister(
                        summary.SessionId,
                        dialog.CountedCashCents,
                        dialog.Note);

                    LoginRuntime.Audit.Log("REGISTER_CLOSED",
                        $"SessionId: {summary.SessionId}, Counted: {dialog.CountedCashCents}, Note: {dialog.Note}",
                        LoginRuntime.Auth.CurrentUser?.Id);
                    LoginRuntime.Telemetry?.TouchCurrentRun("register_closed");

                    LoginRuntime.Auth.Logout();
                    break;
                }
                catch (Exception ex)
                {
                    dialog.ShowError(ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.CloseRegister_Click");
        }
    }

    private async Task<bool> ShowCashInOutDialogAsync(long sessionId)
    {
        var cashDialog = new CashInOutDialog
        {
            XamlRoot = this.RootGrid.XamlRoot
        };

        while (true)
        {
            var cashResult = await cashDialog.ShowAsync();
            if (cashResult != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
                LoginRuntime.RegisterSessions.AddCashAdjustment(
                    sessionId,
                    LoginRuntime.Auth.CurrentUser?.Id,
                    cashDialog.IsCashIn,
                    cashDialog.AmountCents,
                    cashDialog.Reason);

                LoginRuntime.Audit.Log(
                    cashDialog.IsCashIn ? "REGISTER_CASH_IN" : "REGISTER_CASH_OUT",
                    $"SessionId: {sessionId}, Amount: {cashDialog.AmountCents}, Reason: {cashDialog.Reason}",
                    LoginRuntime.Auth.CurrentUser?.Id);
                LoginRuntime.Telemetry?.TouchCurrentRun(
                    cashDialog.IsCashIn ? "register_cash_in" : "register_cash_out");

                return true;
            }
            catch (Exception ex)
            {
                cashDialog.ShowError(ex.Message);
            }
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _lockedRouteTag = null;
            LoginRuntime.Auth.Logout();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.Logout_Click");
        }
    }

    private string GetShellContextText()
    {
        if (RootFrame.CurrentSourcePageType == typeof(LoginPage))
        {
            return LocalizationHelper.GetString("MainWindow_Context_SecureSignIn");
        }

        return (RootFrame.Tag as string) switch
        {
            "checkout" => LocalizationHelper.GetString("MainWindow_Context_Checkout"),
            "products" => LocalizationHelper.GetString("MainWindow_Context_Products"),
            "reports" => LocalizationHelper.GetString("MainWindow_Context_Reports"),
            "store" => LocalizationHelper.GetString("MainWindow_Context_StoreSettings"),
            "tax" => LocalizationHelper.GetString("MainWindow_Context_Tax"),
            "users" => LocalizationHelper.GetString("MainWindow_Context_Users"),
            "prefs" => LocalizationHelper.GetString("MainWindow_Context_Preferences"),
            "settings" => LocalizationHelper.GetString("MainWindow_Context_StoreSettings"),
            "about" => LocalizationHelper.GetString("MainWindow_Context_About"),
            _ => LocalizationHelper.GetString("MainWindow_Context_Default")
        };
    }

    private void UpdateNavigationAccess()
    {
        GetCurrentSettingsPage()?.RefreshNavigationAccess();
    }

    private bool IsBootstrapPasswordChangeRequired()
    {
        return LoginRuntime.Auth.CurrentUser?.MustChangePassword == true;
    }

    private async void ShowBootstrapPasswordChangeRequiredDialog()
    {
        if (_isBootstrapPasswordChangeDialogOpen ||
            !IsBootstrapPasswordChangeRequired() ||
            Content.XamlRoot is null)
        {
            return;
        }

        _isBootstrapPasswordChangeDialogOpen = true;

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Password change required",
                Content = "You must change your password before continuing.",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _isBootstrapPasswordChangeDialogOpen = false;
        }
    }

    internal void NavigateToFirstAvailablePage(string? excludeTag = null)
    {
        if (LoginRuntime.Auth.CurrentUser is null)
        {
            return;
        }

        var targetTag = GetFirstAvailableTag(excludeTag);
        StartupTrace.Write($"MainWindow.NavigateToFirstAvailablePage:{targetTag ?? "<null>"}:exclude={excludeTag ?? "<null>"}");
        if (!string.IsNullOrWhiteSpace(targetTag))
        {
            NavigateToTag(targetTag);
        }
    }

    private string GetFirstAvailableTag(string? excludeTag = null)
    {
        if (LoginRuntime.Auth.CanCheckout && excludeTag != "checkout")
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
            return "store";
        }

        if (LoginRuntime.Auth.CanManageUsers)
        {
            return "users";
        }

        return "prefs";
    }

    public void SetCurrentRouteTag(string? tag)
    {
        RootFrame.Tag = tag;
        UpdateShellChrome();
    }

    internal void ApplyAppLanguage(string languageTag)
    {
        try
        {
            LocalizationService.Instance.SetLanguage(languageTag);

            ReloadCurrentRouteForLanguage();
            UpdateShellChrome();
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.ApplyAppLanguage({languageTag}) failed: {ex.Message}");
            LoginRuntime.ReportException(ex, "MainWindow.ApplyAppLanguage");
        }
    }

    public void RefreshSessionIdleTimeoutSetting()
    {
        if (!_isRuntimeBootstrapComplete)
        {
            return;
        }

        _sessionIdleTimeout = TimeSpan.FromMinutes(LoginRuntime.Settings.GetSessionIdleTimeoutMinutes());
        RecordUserActivity();
    }

    private void ReloadCurrentRouteForLanguage()
    {
        // Force page re-creation: WinUI Frame won't recreate if same type is current
        RootFrame.Content = null;
        RootFrame.BackStack.Clear();

        if (RootFrame.CurrentSourcePageType == typeof(LoginPage) || LoginRuntime.Auth.CurrentUser is null)
        {
            RootFrame.Tag = null;
            RootFrame.Navigate(typeof(LoginPage));
            return;
        }

        var currentTag = RootFrame.Tag as string;
        if (string.IsNullOrWhiteSpace(currentTag))
        {
            currentTag = GetFirstAvailableTag();
        }

        RootFrame.Tag = currentTag;
        RootFrame.Navigate(typeof(SettingsPage), currentTag);
    }

    private static string BuildInitials(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = new List<char>(2);

        foreach (var part in parts)
        {
            initials.Add(char.ToUpperInvariant(part[0]));
            if (initials.Count == 2)
            {
                break;
            }
        }

        if (initials.Count == 0)
        {
            return "?";
        }

        return new string(initials.ToArray());
    }

    private void SessionIdleTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (!_isRuntimeBootstrapComplete)
        {
            return;
        }

        if (LoginRuntime.Auth.IsSessionIdleTimeoutExceeded(_sessionIdleTimeout, DateTime.UtcNow))
        {
            _lockedRouteTag ??= RootFrame.Tag as string;
            LoginRuntime.Auth.Lock();
        }
    }

    private void RecordUserActivity()
    {
        if (!_isRuntimeBootstrapComplete)
        {
            return;
        }

        LoginRuntime.Auth.MarkActivity();
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
