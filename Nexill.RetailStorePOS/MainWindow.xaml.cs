using System.Reflection;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.UI.Login.Views;
using Windows.Graphics;
using Windows.Globalization;
using WinRT.Interop;
using RetailStorePOS.WinUiLogin.ViewModels;
using RetailStorePOS.UI.Sales.Views;
using RetailStorePOS.UI.Settings.Views;
using RetailStorePOS.WinUiLogin.Views;
using XamlEllipse = Microsoft.UI.Xaml.Shapes.Ellipse;

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
    private double _splashProgressValue;
    private Grid _shellRootGrid = null!;
    private Border _appTitleBar = null!;
    private Border _titleBarDragRegion = null!;
    private Button _paneToggleButton = null!;
    private TextBlock _brandTitleText = null!;
    private TextBlock _shellContextText = null!;
    private Button _userBadge = null!;
    private TextBlock _userPictureInitials = null!;
    private XamlEllipse _registerStatusDot = null!;
    private TextBlock _userNameText = null!;
    private TextBlock _registerStatusText = null!;
    private Frame _rootFrame = null!;
    private Border _splashOverlay = null!;
    private Border _splashProgressTrack = null!;
    private Border _splashProgressFill = null!;
    private TextBlock _splashVersionText = null!;
    private TextBlock _splashCopyrightText = null!;
    private TextBlock _splashStatusTitle = null!;
    private TextBlock _splashStatusDetail = null!;
    private TextBlock _splashTitleText = null!;
    private TextBlock _splashSubtitleText = null!;
    private readonly DispatcherQueueTimer _sessionIdleTimer;
    private TimeSpan _sessionIdleTimeout = TimeSpan.FromMinutes(30);
    private string? _lockedRouteTag;
    private bool _isBootstrapPasswordChangeDialogOpen;
    private bool? _cachedHasActiveRegisterSession;
    private bool _isRegisterStatusRefreshInFlight;
    private DateTimeOffset _lastRegisterStatusRefreshAt = DateTimeOffset.MinValue;
    private CashInOutDialog? _activeCashInOutDialog;
    private CloseRegisterDialog? _activeCloseRegisterDialog;
    private MenuFlyout _userMenuFlyout = null!;
    private MenuFlyoutItem _cashInOutMenuItem = null!;
    private MenuFlyoutItem _closeRegisterMenuItem = null!;
    private MenuFlyoutItem _logoutMenuItem = null!;
    private TeachingTip _firstRunTutorialTip = null!;
    private static readonly TimeSpan RegisterStatusRefreshInterval = TimeSpan.FromSeconds(2);
    private readonly System.Threading.CancellationTokenSource _verificationCts = new();

    public MainWindow()
    {
        StartupTrace.Write("MainWindow.ctor:start");
        Current = this;
        try
        {
            StartupTrace.Write("MainWindow.ctor:before InitializeComponent");
            InitializeComponent();
            StartupTrace.Write("MainWindow.ctor:after InitializeComponent");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.InitializeComponent failed: {ex}");
            App.WriteCrashLog("MainWindow.InitializeComponent", ex);
            throw;
        }

        _shellRootGrid = new Grid
        {
            Background = Brush(255, 45, 27, 105),
            RequestedTheme = ElementTheme.Dark
        };
        Content = _shellRootGrid;

        BuildShellUi();
        InitializeUserMenuFlyout();
        InitializeFirstRunTutorialTip();
        ApplyShellLocalization();
        _splashProgressTrack.SizeChanged += (_, _) => UpdateSplashProgressFill();
        ConfigureWindow();

        _sessionIdleTimer = DispatcherQueue.CreateTimer();
        _sessionIdleTimer.Interval = TimeSpan.FromSeconds(30);
        _sessionIdleTimer.IsRepeating = true;
        _sessionIdleTimer.Tick += SessionIdleTimer_Tick;

        _rootFrame.Navigated += RootFrame_Navigated;
        _shellRootGrid.Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        _shellRootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ShellRootGrid_PointerPressed), true);
        _shellRootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ShellRootGrid_KeyDown), true);

        StartupTrace.Write("MainWindow.ctor:end");
    }

    private static readonly SizeInt32 SplashSize = new(580, 380);
    private static readonly SizeInt32 AppSize = new(1365, 768);

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));
    }

    private void BuildShellUi()
    {
        _shellRootGrid.Children.Clear();
        _shellRootGrid.RowDefinitions.Clear();
        _shellRootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        _shellRootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _appTitleBar = new Border
        {
            Background = Brush(255, 249, 251, 253),
            BorderBrush = Brush(255, 226, 232, 240),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        Grid.SetRow(_appTitleBar, 0);
        _shellRootGrid.Children.Add(_appTitleBar);

        var titleBarGrid = new Grid { FlowDirection = FlowDirection.LeftToRight };
        _appTitleBar.Child = titleBarGrid;

        _titleBarDragRegion = new Border { Background = Brush(255, 249, 251, 253) };
        titleBarGrid.Children.Add(_titleBarDragRegion);

        var titleContentGrid = new Grid
        {
            ColumnSpacing = 12,
            Margin = new Thickness(10, 0, 128, 0)
        };
        titleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        titleBarGrid.Children.Add(titleContentGrid);

        var brandRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleContentGrid.Children.Add(brandRow);

        _paneToggleButton = new Button
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(8),
            Background = Brush(255, 45, 27, 105),
            BorderBrush = Brush(0, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            Content = new TextBlock
            {
                Text = "Menu",
                FontSize = 11,
                Foreground = Brush(255, 75, 85, 99),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _paneToggleButton.Click += PaneToggleButton_Click;
        brandRow.Children.Add(_paneToggleButton);

        brandRow.Children.Add(new Border
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(3),
            Background = Brush(255, 246, 250, 253),
            BorderBrush = Brush(255, 215, 225, 234),
            BorderThickness = new Thickness(0.3),
            Child = new TextBlock
            {
                Text = "N",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush(255, 45, 27, 105),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var brandText = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        _brandTitleText = new TextBlock
        {
            Text = "Retail Store POS",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(255, 36, 49, 66)
        };
        _shellContextText = new TextBlock
        {
            Text = "Reports workspace",
            FontSize = 11,
            Foreground = Brush(255, 112, 128, 149)
        };
        brandText.Children.Add(_brandTitleText);
        brandText.Children.Add(_shellContextText);
        brandRow.Children.Add(brandText);

        _userBadge = new Button
        {
            Visibility = Visibility.Collapsed,
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(1, 1, 50, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_userBadge, 1);
        titleContentGrid.Children.Add(_userBadge);

        var userGrid = new Grid { ColumnSpacing = 10 };
        userGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        userGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _userBadge.Content = userGrid;

        var avatarGrid = new Grid
        {
            Width = 32,
            Height = 32
        };
        userGrid.Children.Add(avatarGrid);

        _userPictureInitials = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(255, 36, 49, 66),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarGrid.Children.Add(new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = Brush(255, 229, 231, 235),
            Child = _userPictureInitials
        });

        _registerStatusDot = new XamlEllipse
        {
            Width = 10,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = Brush(255, 148, 163, 184),
            Stroke = Brush(255, 249, 251, 253),
            StrokeThickness = 2
        };
        avatarGrid.Children.Add(_registerStatusDot);

        var userText = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(userText, 1);
        _userNameText = new TextBlock
        {
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(255, 36, 49, 66),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _registerStatusText = new TextBlock
        {
            Text = "No active register",
            FontSize = 11,
            Foreground = Brush(255, 112, 128, 149),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        userText.Children.Add(_userNameText);
        userText.Children.Add(_registerStatusText);
        userGrid.Children.Add(userText);

        _rootFrame = new Frame();
        Grid.SetRow(_rootFrame, 1);
        _shellRootGrid.Children.Add(_rootFrame);

        _splashOverlay = BuildSplashOverlay();
        Grid.SetRowSpan(_splashOverlay, 2);
        _shellRootGrid.Children.Add(_splashOverlay);
    }

    private Border BuildSplashOverlay()
    {
        var overlay = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = ColorHelper.FromArgb(255, 45, 27, 105), Offset = 0.0 },
                    new GradientStop { Color = ColorHelper.FromArgb(255, 67, 56, 202), Offset = 0.45 },
                    new GradientStop { Color = ColorHelper.FromArgb(255, 124, 58, 237), Offset = 0.75 },
                    new GradientStop { Color = ColorHelper.FromArgb(255, 109, 40, 217), Offset = 1.0 }
                }
            }
        };

        var overlayGrid = new Grid();
        overlay.Child = overlayGrid;

        overlayGrid.Children.Add(new XamlEllipse
        {
            Width = 600,
            Height = 600,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -200, -150, 0),
            Fill = Brush(24, 255, 255, 255)
        });
        overlayGrid.Children.Add(new XamlEllipse
        {
            Width = 400,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(-120, 0, 0, -120),
            Fill = Brush(16, 0, 0, 128)
        });

        var splashBrand = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 18
        };
        overlayGrid.Children.Add(splashBrand);

        splashBrand.Children.Add(new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(10),
            Background = Brush(32, 255, 255, 255),
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "N",
                FontSize = 34,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush(255, 255, 255, 255),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        _splashTitleText = new TextBlock
        {
            Text = "Retail Store POS",
            FontSize = 30,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(255, 255, 255, 255),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _splashSubtitleText = new TextBlock
        {
            Text = "Preparing workspace",
            FontSize = 13,
            Foreground = Brush(255, 196, 181, 253),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        splashBrand.Children.Add(_splashTitleText);
        splashBrand.Children.Add(_splashSubtitleText);

        var bottomGrid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(32, 0, 32, 28)
        };
        bottomGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
        bottomGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        bottomGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
        overlayGrid.Children.Add(bottomGrid);

        var statusGrid = new Grid();
        Grid.SetRow(statusGrid, 0);
        bottomGrid.Children.Add(statusGrid);

        var statusText = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Spacing = 2
        };
        _splashStatusTitle = new TextBlock
        {
            Text = "Starting",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(255, 233, 213, 255)
        };
        _splashStatusDetail = new TextBlock
        {
            Text = "Loading services",
            FontSize = 10,
            Foreground = Brush(255, 167, 139, 250)
        };
        statusText.Children.Add(_splashStatusTitle);
        statusText.Children.Add(_splashStatusDetail);
        statusGrid.Children.Add(statusText);

        var versionText = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 2
        };
        _splashVersionText = new TextBlock
        {
            Text = "vX.X.X",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Light,
            Foreground = Brush(255, 229, 231, 235),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _splashCopyrightText = new TextBlock
        {
            Text = "© XXXX",
            FontSize = 10,
            Foreground = Brush(255, 167, 139, 250),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        versionText.Children.Add(_splashVersionText);
        versionText.Children.Add(_splashCopyrightText);
        statusGrid.Children.Add(versionText);

        _splashProgressTrack = new Border
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush(255, 67, 56, 202)
        };
        Grid.SetRow(_splashProgressTrack, 2);
        _splashProgressFill = new Border
        {
            Width = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brush(255, 233, 213, 255)
        };
        _splashProgressTrack.Child = _splashProgressFill;
        bottomGrid.Children.Add(_splashProgressTrack);

        return overlay;
    }

    private void SetSplashProgress(double value)
    {
        _splashProgressValue = Math.Clamp(value, 0, 100);
        UpdateSplashProgressFill();
    }

    private void UpdateSplashProgressFill()
    {
        _splashProgressFill.Width = _splashProgressTrack.ActualWidth * (_splashProgressValue / 100d);
    }

    private void InitializeFirstRunTutorialTip()
    {
        _firstRunTutorialTip = new TeachingTip
        {
            Title = "Quick setup",
            Subtitle = "Review the settings sections to finish setup.",
            CloseButtonContent = "Close",
            IsLightDismissEnabled = false,
            PreferredPlacement = TeachingTipPlacementMode.Right,
            IsOpen = false
        };

        _firstRunTutorialTip.CloseButtonClick += FirstRunTutorialTip_CloseButtonClick;
        Grid.SetRowSpan(_firstRunTutorialTip, 2);
        Canvas.SetZIndex(_firstRunTutorialTip, 300);
        _shellRootGrid.Children.Add(_firstRunTutorialTip);
    }

    private void InitializeUserMenuFlyout()
    {
        _cashInOutMenuItem = new MenuFlyoutItem { Text = "Cash in/out" };
        _cashInOutMenuItem.Click += CashInOut_Click;

        _closeRegisterMenuItem = new MenuFlyoutItem { Text = "Close register" };
        _closeRegisterMenuItem.Click += CloseRegister_Click;

        _logoutMenuItem = new MenuFlyoutItem { Text = "Log out" };
        _logoutMenuItem.Click += Logout_Click;

        _userMenuFlyout = new MenuFlyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight
        };

        _userMenuFlyout.Opening += UserMenuFlyout_Opening;
        _userMenuFlyout.Items.Add(_cashInOutMenuItem);
        _userMenuFlyout.Items.Add(_closeRegisterMenuItem);
        _userMenuFlyout.Items.Add(new MenuFlyoutSeparator());
        _userMenuFlyout.Items.Add(_logoutMenuItem);
        _userBadge.Flyout = _userMenuFlyout;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
        if (_isRuntimeBootstrapComplete || _isRuntimeBootstrapInProgress)
        {
            return;
        }

        _isRuntimeBootstrapInProgress = true;
        _shellRootGrid.Loaded -= MainWindow_Loaded;
        DispatcherTimer? pbTimer = null;

        try
        {
            // Start the visual progress bar animation
            pbTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            pbTimer.Tick += (s, args) =>
            {
                if (_splashProgressValue < 95)
                {
                    SetSplashProgress(_splashProgressValue + 1.5);
                }
            };
            pbTimer.Start();

            // Run the heavy database and runtime initialization off the UI thread
            await Task.Run(() => LoginRuntime.Initialize());
            LoginRuntime.ApplyPersistedLanguageSetting();

            // Snap progress bar to 100% immediately so startup is not artificially delayed.
            pbTimer.Stop();
            SetSplashProgress(100);

            // Hide the window completely so resizing doesn't flash on screen
            _appWindow?.Hide();

            // Prepare the main app UI invisibly
            _shellRootGrid.RequestedTheme = ElementTheme.Light;
            _shellRootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 247, 250, 252));
            TransitionToAppWindow();

            // Initialize the root UI state
            LoginRuntime.Auth.LoginStateChanged += Auth_LoginStateChanged;
            _sessionIdleTimeout = TimeSpan.FromMinutes(LoginRuntime.Settings.GetSessionIdleTimeoutMinutes());
            _sessionIdleTimer.Start();
            UpdateShellChrome();
            UpdateNavigationAccess();

            // Prepare the frame to fade in
            _rootFrame.Opacity = 0.0;
            _rootFrame.Navigate(typeof(LoginPage));

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
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeInAnim, _rootFrame);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            inStoryboard.Begin();

            // Clean up splash overlay
            _splashOverlay.Visibility = Visibility.Collapsed;
            _isRuntimeBootstrapComplete = true;

            // Start telemetry background sync and log app launch
            try
            {
                LoginRuntime.Telemetry?.StartBackgroundSync();
                var runId = Guid.NewGuid().ToString("N");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var telemetry = LoginRuntime.Telemetry;
                        if (telemetry is not null)
                        {
                            await telemetry.LogAppLaunchAsync(runId);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoginRuntime.ReportException(ex, "WinUiLogin.Telemetry.AppLaunch");
                    }
                });
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"Telemetry startup failed: {ex.Message}");
                LoginRuntime.ReportException(ex, "WinUiLogin.Telemetry.Startup");
            }

            // Trigger post-launch startup checks and license verification
            _ = RunPostLaunchStartupChecksAsync();
            _ = RunPostLaunchLicenseVerificationAsync();
        }
        catch (Exception ex)
        {
            pbTimer?.Stop();
            StartupTrace.Write($"MainWindow.Loaded bootstrap failed: {ex}");
            LoginRuntime.ReportException(ex, "WinUiLogin.RuntimeBootstrap");
            _splashStatusTitle.Text = LocalizationHelper.GetString("MainWindow_Status_StartupError");
            _splashStatusDetail.Text = ex.Message;
        }
        finally
        {
            pbTimer?.Stop();
            _isRuntimeBootstrapInProgress = false;
        }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in async void handler: {ex.Message}");
        }
    }

    private async Task RunPostLaunchStartupChecksAsync()
    {
        await Task.Yield();

        try
        {
            var timeValidation = new RetailStorePOS.UI.Common.Services.TimeValidationService(LoginRuntime.ConnectionFactory);
            var timeCheckPassed = await timeValidation.IsSystemTimeValidAsync();

            while (!timeCheckPassed)
            {
                var dialog = new RetailStorePOS.UI.Platform.Views.TimeSyncDialog { XamlRoot = _shellRootGrid.XamlRoot };
                await dialog.ShowAsync();

                if (dialog.Result == RetailStorePOS.UI.Platform.Views.TimeSyncDialogResult.Continue)
                {
                    if (LoginRuntime.Telemetry is not null)
                    {
                        await LoginRuntime.Telemetry.LogTimeWarningIgnoredAsync(DateTime.UtcNow);
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

            var crashDialog = new RetailStorePOS.UI.Platform.Views.CrashFeedbackDialog { XamlRoot = _shellRootGrid.XamlRoot };
            var result = await crashDialog.ShowAsync();

            if (result == ContentDialogResult.Primary && LoginRuntime.Telemetry is not null)
            {
                await LoginRuntime.Telemetry.LogCrashFeedbackAsync(
                    crashDialog.SelectedCategory,
                    crashDialog.FeedbackDetails);
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

    private async Task RunPostLaunchLicenseVerificationAsync()
    {
        await Task.Yield();
        var token = _verificationCts.Token;

        try
        {
            // Check snapshot validity immediately without waiting
            var snapshot = LoginRuntime.License.GetCurrentSnapshot();
            if (snapshot != null && snapshot.IsCurrentlyValid(DateTimeOffset.UtcNow))
            {
                StartupTrace.Write("License: Cached snapshot is currently valid. Skipping post-launch verification.");
                return;
            }

            // Delay 3 seconds before background verification, tied to window cancellation
            await Task.Delay(3000, token);

            // DPAPI and SQLite access are blocking operations. Keep them off
            // the UI thread and migrate the legacy plaintext setting only
            // after reading the protected value back successfully.
            var storedLicenseKey = await Task.Run(ReadAndMigrateStoredLicenseKey, token);
            RetailStorePOS.UI.Common.Services.LicenseActivationResult result;

            if (string.IsNullOrWhiteSpace(storedLicenseKey))
            {
                StartupTrace.Write("License: No stored license key found. Attempting install-based reissue.");
                result = await LoginRuntime.License.ReissueByInstallAsync(token);
            }
            else
            {
                StartupTrace.Write("License: Running background post-launch verification.");
                result = await LoginRuntime.License.ActivateAsync(storedLicenseKey.Trim(), token);
            }

            StartupTrace.Write($"License: Background verification complete. Outcome: {result.Outcome}, ErrorCode: {result.ErrorCode}");

            if (LoginRuntime.Telemetry is not null)
            {
                await LoginRuntime.Telemetry.LogLicenseVerificationAsync(
                    result.Outcome.ToString(),
                    result.ErrorCode);

                if (result.Outcome == RetailStorePOS.UI.Common.Services.LicenseActivationOutcome.SignatureInvalid)
                {
                    await LoginRuntime.Telemetry.LogErrorAsync(
                        new System.Security.Cryptography.CryptographicException("Local signature verification failed during background check."),
                        "license_signature_failure");
                }
            }
        }
        catch (OperationCanceledException)
        {
            StartupTrace.Write("License: Background verification canceled.");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.RunPostLaunchLicenseVerificationAsync failed: {ex.Message}");
            LoginRuntime.ReportException(ex, "WinUiLogin.License.PostLaunchVerification");
        }
    }

    private static string ReadAndMigrateStoredLicenseKey()
    {
        var protectedKey = SecureStorageService.GetSecret("LicenseKey");
        if (!string.IsNullOrWhiteSpace(protectedKey))
        {
            return protectedKey.Trim();
        }

        var legacyKey = LoginRuntime.Settings.GetSetting("license.key", string.Empty).Trim();
        if (legacyKey.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            SecureStorageService.StoreSecret("LicenseKey", legacyKey);
            var persistedKey = SecureStorageService.GetSecret("LicenseKey");
            if (string.Equals(persistedKey, legacyKey, StringComparison.Ordinal))
            {
                LoginRuntime.Settings.SetSetting("license.key", string.Empty);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or CryptographicException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException)
        {
            LoginRuntime.ReportException(ex, "License.LegacyKeyMigration");
        }

        return legacyKey;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            _verificationCts.Cancel();
            _verificationCts.Dispose();
        }
        catch { /* best effort */ }

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
        SetTitleBar(_titleBarDragRegion);
        _appTitleBar.Visibility = Visibility.Visible;

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
        _appTitleBar.Visibility = Visibility.Collapsed;

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
            _shellRootGrid.FlowDirection = FlowDirection.RightToLeft;
        }

        // Bind Splash screen text to MSBuild properties compiled into Assembly metadata
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var copyright = assembly?.GetCustomAttribute<System.Reflection.AssemblyCopyrightAttribute>()?.Copyright;

        var cleanVersion = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Split('+')[0];
        _splashVersionText.Text = string.Format(LocalizationHelper.GetString("Splash_Version_Format"), cleanVersion);
        _splashCopyrightText.Text = string.IsNullOrWhiteSpace(copyright) ? LocalizationHelper.GetString("Splash_Copyright_Default") : copyright;
    }

    private void TrySetWindowIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico");
            if (!System.IO.File.Exists(iconPath))
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

    private void ShellRootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        RecordUserActivity();

        if (!_isFirstRunTutorialRunning ||
            _firstRunTutorialStepIndex != 0 ||
            _rootFrame.CurrentSourcePageType != typeof(SettingsPage))
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (IsDescendantOf(source, _paneToggleButton) ||
            IsDescendantOf(source, _firstRunTutorialTip))
        {
            return;
        }

        QueueFirstRunTutorialSubNavigationStep();
    }

    private void ShellRootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        RecordUserActivity();
    }

    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(LoginPage))
        {
            ResetFirstRunTutorialSession();
            _rootFrame.Tag = null;
            UpdateShellChrome();
            return;
        }

        UpdateShellChrome();

        if (_isFirstRunTutorialRunning && _rootFrame.CurrentSourcePageType != typeof(SettingsPage))
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
                    HideActiveSensitiveDialogs();

                    if (_rootFrame.CurrentSourcePageType != typeof(LoginPage))
                    {
                        StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-locked-login");
                        _lockedRouteTag ??= _rootFrame.Tag as string;
                        _rootFrame.Navigate(typeof(LoginPage));
                    }

                    return;
                }

                if (LoginRuntime.Auth.CurrentUser is null && _rootFrame.CurrentSourcePageType != typeof(LoginPage))
                {
                    QueueRegisterCashierAssignmentCloseIfAny();
                    StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-login");
                    _lockedRouteTag = null;
                    _rootFrame.Tag = null;
                    _rootFrame.Navigate(typeof(LoginPage));
                    return;
                }

                if (IsBootstrapPasswordChangeRequired())
                {
                    StartupTrace.Write("MainWindow.Auth_LoginStateChanged:navigate-bootstrap-password-change");
                    NavigateToTag("users");
                    ShowBootstrapPasswordChangeRequiredDialog();
                    return;
                }

                if (LoginRuntime.Auth.CurrentUser?.Id is > 0)
                {
                    QueueRegisterCashierAssignmentSync();
                }

                if (LoginRuntime.Auth.CurrentUser is not null && _rootFrame.CurrentSourcePageType == typeof(LoginPage))
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
        try
        {
        if (_isFirstRunTutorialCompletedThisSession ||
            LoginRuntime.Auth.CurrentUser is null ||
            _rootFrame.CurrentSourcePageType != typeof(SettingsPage) ||
            LoginRuntime.Settings.IsFirstRunTutorialCleared() ||
            GetCurrentSettingsPage() is null)
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
                _rootFrame.CurrentSourcePageType != typeof(SettingsPage) ||
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
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in async void handler: {ex.Message}");
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
            _firstRunTutorialTip.IsOpen = false;
            return;
        }

        step.Target.UpdateLayout();

        _firstRunTutorialTip.IsOpen = false;
        _firstRunTutorialTip.Target = step.Target;
        _firstRunTutorialTip.Title = step.Title;
        _firstRunTutorialTip.Subtitle = step.Subtitle;
        _firstRunTutorialTip.ActionButtonContent = null;
        _firstRunTutorialTip.CloseButtonContent = LocalizationHelper.GetString("MainWindow_Tutorial_CloseButton.Content");
        _firstRunTutorialTip.PreferredPlacement = step.PreferredPlacement;
        _firstRunTutorialTip.IsOpen = true;
    }

    private void CompleteFirstRunTutorial()
    {
        _firstRunTutorialTip.IsOpen = false;
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
                _paneToggleButton,
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
        _firstRunTutorialTip.IsOpen = false;
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
        _firstRunTutorialTip.IsOpen = false;
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
        return _rootFrame.Content as SettingsPage;
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
        _firstRunTutorialTip.IsOpen = false;
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
            try
            {
                LoginRuntime.Authorization.RequireAuthenticated();
                LoginRuntime.Auth.Logout();
            }
            catch (UnauthorizedAccessException)
            {
                UpdateShellChrome();
                UpdateCloseRegisterMenuState(forceRefresh: true);
            }
            return;
        }

        if (IsBootstrapPasswordChangeRequired() &&
            !string.Equals(tag, "users", StringComparison.OrdinalIgnoreCase))
        {
            StartupTrace.Write($"MainWindow.NavigateToTag:blocked-bootstrap-password-change:{tag}");
            tag = "users";
            ShowBootstrapPasswordChangeRequiredDialog();
        }

        _rootFrame.Tag = tag;

        if (GetCurrentSettingsPage() is SettingsPage settingsPage)
        {
            settingsPage.NavigateToTag(tag);
            UpdateShellChrome();
            return;
        }

        try
        {
            _rootFrame.Navigate(typeof(SettingsPage), tag);
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.NavigateToTag({tag}) failed: {ex}");
            LoginRuntime.ReportException(ex, $"MainWindow.NavigateToTag.{tag}");
        }
    }

    private void UpdateShellChrome()
    {
        ViewModel.IsLoginPage = _rootFrame.CurrentSourcePageType == typeof(LoginPage);
        ViewModel.CurrentTag = _rootFrame.Tag as string;

        ApplyShellLocalization();

        if (!LoginRuntime.Auth.IsLoggedIn || LoginRuntime.Auth.CurrentUser is not { } user)
        {
            _cachedHasActiveRegisterSession = null;
            _lastRegisterStatusRefreshAt = DateTimeOffset.MinValue;
            _userMenuFlyout.Hide();
            _paneToggleButton.Visibility = Visibility.Collapsed;
            _userBadge.Visibility = Visibility.Collapsed;
            _userBadge.IsEnabled = false;
            _cashInOutMenuItem.IsEnabled = false;
            _closeRegisterMenuItem.IsEnabled = false;
            _logoutMenuItem.IsEnabled = false;
            _userNameText.Text = string.Empty;
            _registerStatusText.Text = string.Empty;
            _registerStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 148, 163, 184));
            _userPictureInitials.Text = string.Empty;
            return;
        }

        _paneToggleButton.Visibility = Visibility.Collapsed;
        _userBadge.Visibility = Visibility.Visible;
        _userBadge.IsEnabled = true;
        _logoutMenuItem.IsEnabled = true;

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.Username
            : user.DisplayName;
        _userNameText.Text = displayName;
        _userPictureInitials.Text = BuildInitials(displayName);
        UpdateCloseRegisterMenuState();
    }

    private void UserMenuFlyout_Opening(object? sender, object e)
    {
        if (!LoginRuntime.Auth.IsLoggedIn)
        {
            ApplyRegisterSessionState(hasActiveSession: false);
            return;
        }

        UpdateCloseRegisterMenuState(forceRefresh: true);
    }

    private void UpdateCloseRegisterMenuState(bool forceRefresh = false)
    {
        if (!LoginRuntime.Auth.IsLoggedIn || LoginRuntime.Auth.CurrentUser is null)
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
        _cashInOutMenuItem.IsEnabled = hasActiveSession;
        _closeRegisterMenuItem.IsEnabled = hasActiveSession;
        ViewModel.IsRegisterActive = hasActiveSession;
        _registerStatusText.Text = ViewModel.RegisterStatus;
        _registerStatusDot.Fill = new SolidColorBrush(
            hasActiveSession
                ? ColorHelper.FromArgb(255, 22, 163, 74)
                : ColorHelper.FromArgb(255, 148, 163, 184));
    }

    private void ApplyRegisterSessionUnavailableState()
    {
        _cashInOutMenuItem.IsEnabled = false;
        _closeRegisterMenuItem.IsEnabled = false;
        ViewModel.IsRegisterActive = false;
        _registerStatusText.Text = LocalizationHelper.GetString("MainWindow_Status_RegisterUnavailable");
        _registerStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68));
    }

    private void QueueRegisterCashierAssignmentSync()
    {
        var userId = LoginRuntime.Auth.CurrentUser?.Id;
        if (userId is not > 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var activeSession = LoginRuntime.RegisterSessions.GetActiveSession();
                if (activeSession is null)
                {
                    return;
                }

                LoginRuntime.RegisterSessions.EnsureCashierAssignment(activeSession.Id, userId);
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"MainWindow.QueueRegisterCashierAssignmentSync failed: {ex}");
                LoginRuntime.ReportException(ex, "MainWindow.QueueRegisterCashierAssignmentSync");
            }
        });
    }

    private void QueueRegisterCashierAssignmentCloseIfAny()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var activeSession = LoginRuntime.RegisterSessions.GetActiveSession();
                if (activeSession is null)
                {
                    return;
                }

                LoginRuntime.RegisterSessions.CloseActiveCashierAssignments(activeSession.Id);
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"MainWindow.QueueRegisterCashierAssignmentCloseIfAny failed: {ex}");
                LoginRuntime.ReportException(ex, "MainWindow.QueueRegisterCashierAssignmentCloseIfAny");
            }
        });
    }

    private void QueueRegisterStatusRefresh(bool forceRefresh)
    {
        if (!LoginRuntime.Auth.IsLoggedIn || LoginRuntime.Auth.CurrentUser is not { } user)
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
        _shellRootGrid.FlowDirection = LocalizationHelper.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        _brandTitleText.Text = ViewModel.BrandTitle;
        _shellContextText.Text = ViewModel.ShellContext;
        _registerStatusText.Text = ViewModel.RegisterStatus;
        _cashInOutMenuItem.Text = ViewModel.MainWindow_CashInOut_Text;
        _closeRegisterMenuItem.Text = ViewModel.MainWindow_CloseRegister_Text;
        _logoutMenuItem.Text = ViewModel.MainWindow_Logout_Text;
        _firstRunTutorialTip.Title = ViewModel.MainWindow_Tutorial_Title;
        _firstRunTutorialTip.Subtitle = ViewModel.MainWindow_Tutorial_Subtitle;
        _firstRunTutorialTip.CloseButtonContent = ViewModel.MainWindow_Tutorial_CloseButtonContent;
        _splashTitleText.Text = ViewModel.SplashTitle;
        _splashSubtitleText.Text = ViewModel.SplashSubtitle;
        _splashStatusTitle.Text = ViewModel.SplashStatusTitle;
        _splashStatusDetail.Text = ViewModel.SplashStatusDetail;
    }

    private void HideActiveSensitiveDialogs()
    {
        try
        {
            _activeCashInOutDialog?.Hide();
        }
        catch
        {
        }

        try
        {
            _activeCloseRegisterDialog?.Hide();
        }
        catch
        {
        }
    }

    private async void CashInOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LoginRuntime.Authorization.RequireAuthenticated();
            var summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
            if (summary == null)
            {
                UpdateCloseRegisterMenuState(forceRefresh: true);
                return;
            }

            await ShowCashInOutDialogAsync(summary.SessionId);
            UpdateCloseRegisterMenuState(forceRefresh: true);
        }
        catch (UnauthorizedAccessException)
        {
            UpdateShellChrome();
            UpdateCloseRegisterMenuState(forceRefresh: true);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.CashInOut_Click");
        }
    }

    private async void CloseRegister_Click(object sender, RoutedEventArgs e)
    {
        try
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
            LoginRuntime.Authorization.RequireAuthenticated();
            var summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
            if (summary == null)
            {
                UpdateCloseRegisterMenuState(forceRefresh: true);
                return;
            }

            var dialog = new CloseRegisterDialog(summary)
            {
                XamlRoot = this._shellRootGrid.XamlRoot
            };

            while (true)
            {
                LoginRuntime.Authorization.RequireAuthenticated();
                var dialogWidthSnapshot = PushContentDialogWidth(Math.Max(520d, this._shellRootGrid.XamlRoot.Size.Width * 0.5));
                try
                {
                    _activeCloseRegisterDialog = dialog;
                    await dialog.ShowAsync();
                }
                finally
                {
                    _activeCloseRegisterDialog = null;
                    PopContentDialogWidth(dialogWidthSnapshot);
                }

                LoginRuntime.Authorization.RequireAuthenticated();
                var result = dialog.ActionResult;
                if (result == ContentDialogResult.Secondary)
                {
                    var changed = await ShowCashInOutDialogAsync(summary.SessionId);
                    LoginRuntime.Authorization.RequireAuthenticated();
                    if (changed)
                    {
                        LoginRuntime.Authorization.RequireAuthenticated();
                        summary = LoginRuntime.RegisterSessions.GetActiveCloseSummary();
                        if (summary == null)
                        {
                            UpdateCloseRegisterMenuState(forceRefresh: true);
                            return;
                        }

                        dialog = new CloseRegisterDialog(summary, dialog.CountedCashText, dialog.Note)
                        {
                            XamlRoot = this._shellRootGrid.XamlRoot
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
                    LoginRuntime.Authorization.RequireAuthenticated();
                    LoginRuntime.RegisterSessions.CloseRegister(
                        summary.SessionId,
                        dialog.CountedCashCents,
                        dialog.Note,
                        LoginRuntime.Auth.CurrentUser?.Id);

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
        catch (UnauthorizedAccessException)
        {
            UpdateShellChrome();
            UpdateCloseRegisterMenuState(forceRefresh: true);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.CloseRegister_Click");
        }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in async void handler: {ex.Message}");
        }
    }

    private async Task<bool> ShowCashInOutDialogAsync(long sessionId)
    {
        var cashDialog = new CashInOutDialog
        {
            XamlRoot = this._shellRootGrid.XamlRoot
        };

        while (true)
        {
            LoginRuntime.Authorization.RequireAuthenticated();
            try
            {
                _activeCashInOutDialog = cashDialog;
                var cashResult = await cashDialog.ShowAsync();
                if (cashResult != ContentDialogResult.Primary)
                {
                    return false;
                }
            }
            finally
            {
                _activeCashInOutDialog = null;
            }

            try
            {
                LoginRuntime.Authorization.RequireAuthenticated();
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
            catch (UnauthorizedAccessException)
            {
                throw;
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
            LoginRuntime.Authorization.RequireAuthenticated();
            _lockedRouteTag = null;
            LoginRuntime.Auth.Logout();
        }
        catch (UnauthorizedAccessException)
        {
            UpdateShellChrome();
            UpdateCloseRegisterMenuState(forceRefresh: true);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.Logout_Click");
        }
    }

    private string GetShellContextText()
    {
        if (_rootFrame.CurrentSourcePageType == typeof(LoginPage))
        {
            return LocalizationHelper.GetString("MainWindow_Context_SecureSignIn");
        }

        return (_rootFrame.Tag as string) switch
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
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "MainWindow.ShowBootstrapPasswordChangeRequiredDialog");
        }
        finally
        {
            _isBootstrapPasswordChangeDialogOpen = false;
        }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in async void handler: {ex.Message}");
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
        _rootFrame.Tag = tag;
        UpdateShellChrome();
    }

    public void RefreshShellChrome()
    {
        UpdateShellChrome();
        UpdateCloseRegisterMenuState(forceRefresh: true);
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
        _rootFrame.Content = null;
        _rootFrame.BackStack.Clear();

        if (_rootFrame.CurrentSourcePageType == typeof(LoginPage) || LoginRuntime.Auth.CurrentUser is null)
        {
            _rootFrame.Tag = null;
            _rootFrame.Navigate(typeof(LoginPage));
            return;
        }

        var currentTag = _rootFrame.Tag as string;
        if (string.IsNullOrWhiteSpace(currentTag))
        {
            currentTag = GetFirstAvailableTag();
        }

        _rootFrame.Tag = currentTag;
        _rootFrame.Navigate(typeof(SettingsPage), currentTag);
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
            _lockedRouteTag ??= _rootFrame.Tag as string;
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
