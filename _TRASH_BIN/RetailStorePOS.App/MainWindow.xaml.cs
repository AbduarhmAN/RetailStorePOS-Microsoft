using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace RetailStorePOS.App;

public partial class MainWindow : Window
{
    private LoginView? _loginView;
    private LoginViewModel? _loginViewModel;
    private MainViewModel? _mainViewModel;
    private Task? _shellWarmupTask;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;

        // Subscribe to login state changes
        AppServices.Auth.LoginStateChanged += OnLoginStateChanged;

        // Show login or main content based on current state
        UpdateLoginState();
    }

    public async Task PrepareForStartupAsync()
    {
        if (!IsLoaded)
        {
            var loadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                Loaded -= handler;
                loadedTcs.TrySetResult(true);
            };

            Loaded += handler;
            await loadedTcs.Task;
        }

        await Dispatcher.InvokeAsync(() => UpdateLayout());
        EnsureShellWarmup();
    }

    public void EnsureShellWarmup()
    {
        if (_shellWarmupTask != null)
        {
            return;
        }

        _shellWarmupTask = ShellHost.PrepareAsync();
        _shellWarmupTask.ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Shell warmup failed: {task.Exception.GetBaseException().Message}");
            }
        }, TaskScheduler.Default);
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        UpdateLoginState();
    }

    private void UpdateLoginState()
    {
        if (AppServices.Auth.NeedsSetup)
        {
            ShowSetupWizard();
        }
        else if (!AppServices.Auth.IsLoggedIn)
        {
            ShowLoginView();
        }
        else
        {
            ShowMainContent();
        }
    }

    private void ShowSetupWizard()
    {
        // For now, create first admin directly (simplified setup)
        var result = MessageBox.Show(
            "Welcome to RetailStorePOS POS!\n\nNo users exist. Would you like to create the first admin account?\n\nDefault: admin / 1234",
            "First Time Setup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            AppServices.Auth.CreateFirstAdmin("admin", "Administrator", "1234", "1234");
        }
    }

    private void ShowLoginView()
    {
        if (_loginView == null)
        {
            _loginViewModel = new LoginViewModel(AppServices.Auth);
            _loginView = new LoginView { DataContext = _loginViewModel };
        }

        _loginView.ResetInputs();

        MainContentArea.Visibility = Visibility.Collapsed;
        ContentShell.Visibility = Visibility.Collapsed;
        ShellHost.Visibility = Visibility.Collapsed;

        if (!MainGrid.Children.Contains(_loginView))
        {
            Grid.SetRow(_loginView, 2);
            MainGrid.Children.Add(_loginView);
        }
        _loginView.HorizontalAlignment = HorizontalAlignment.Stretch;
        _loginView.VerticalAlignment = VerticalAlignment.Stretch;
        _loginView.Visibility = Visibility.Visible;
    }

    private void ShowMainContent()
    {
        if (_loginView != null)
        {
            _loginView.Visibility = Visibility.Collapsed;
        }

        MainContentArea.Visibility = Visibility.Visible;
        ContentShell.Visibility = Visibility.Visible;
        ShellHost.Visibility = Visibility.Visible;

        EnsureShellWarmup();
        EnsureValidTabSelection();
        ShellHost.RefreshShellState();
    }

    private void EnsureValidTabSelection()
    {
        var checkedAndAllowed =
            (TabCheckout.IsChecked == true && AppServices.Auth.CanCheckout) ||
            (TabProducts.IsChecked == true && AppServices.Auth.CanManageProducts) ||
            (TabReports.IsChecked == true && AppServices.Auth.CanViewReports) ||
            (TabUsers.IsChecked == true && AppServices.Auth.CanManageUsers) ||
            (TabSettings.IsChecked == true && AppServices.Auth.CanManageSettings) ||
            (TabHelp.IsChecked == true);

        if (checkedAndAllowed)
        {
            return;
        }

        // Navigate to the first tab the user is allowed to see
        if (AppServices.Auth.CanCheckout) { TabCheckout.IsChecked = true; return; }
        if (AppServices.Auth.CanManageProducts) { TabProducts.IsChecked = true; return; }
        if (AppServices.Auth.CanViewReports) { TabReports.IsChecked = true; return; }
        if (AppServices.Auth.CanManageUsers) { TabUsers.IsChecked = true; return; }
        if (AppServices.Auth.CanManageSettings) { TabSettings.IsChecked = true; return; }
        if (TabHelp.Visibility == Visibility.Visible) { TabHelp.IsChecked = true; return; }

        // No permitted tabs — deselect all (user has no permissions)
        TabCheckout.IsChecked = false;
        TabProducts.IsChecked = false;
        TabReports.IsChecked = false;
        TabUsers.IsChecked = false;
        TabSettings.IsChecked = false;
        TabHelp.IsChecked = false;
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Auth.Logout();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLoginView();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                if (TabCheckout.Visibility == Visibility.Visible)
                    TabCheckout.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                if (TabProducts.Visibility == Visibility.Visible)
                    TabProducts.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                if (TabUsers.Visibility == Visibility.Visible)
                    TabUsers.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D4:
            case Key.NumPad4:
                if (TabSettings.Visibility == Visibility.Visible)
                    TabSettings.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D5:
            case Key.NumPad5:
                if (TabHelp.Visibility == Visibility.Visible)
                    TabHelp.IsChecked = true;
                e.Handled = true;
                break;
        }
    }

    #region Win32 Interop for WM_GETMINMAXINFO

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        // Prevent the display from turning off or going to sleep while the POS is running
        try { SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED); } catch { }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            RECT rcWorkArea = monitorInfo.rcWork;
            RECT rcMonitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    #endregion

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            windowChrome.ResizeBorderThickness = new Thickness(0);
        }
        else
        {
            windowChrome.ResizeBorderThickness = new Thickness(6);
        }

        ShellHost.RefreshShellState();
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
        }

        _mainViewModel = e.NewValue as MainViewModel;

        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsUpdateAvailable) or nameof(MainViewModel.UpdateInfo))
        {
            ShellHost.RefreshShellState();
        }
    }

    private void MainTab_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && ShellHost != null)
        {
            ShellHost.RefreshShellState();
        }
    }

    public string GetSelectedTabId()
    {
        if (TabCheckout.IsChecked == true) return "checkout";
        if (TabProducts.IsChecked == true) return "products";
        if (TabReports.IsChecked == true) return "reports";
        if (TabUsers.IsChecked == true) return "users";
        if (TabSettings.IsChecked == true) return "settings";
        return "help";
    }

    public void SelectTab(string tabId)
    {
        switch (tabId)
        {
            case "checkout" when AppServices.Auth.CanCheckout:
                TabCheckout.IsChecked = true;
                break;
            case "products" when AppServices.Auth.CanManageProducts:
                TabProducts.IsChecked = true;
                break;
            case "reports" when AppServices.Auth.CanViewReports:
                TabReports.IsChecked = true;
                break;
            case "users" when AppServices.Auth.CanManageUsers:
                TabUsers.IsChecked = true;
                break;
            case "settings" when AppServices.Auth.CanManageSettings:
                TabSettings.IsChecked = true;
                break;
            case "help":
                TabHelp.IsChecked = true;
                break;
        }
    }

    public void MinimizeFromShell() => WindowState = WindowState.Minimized;

    public void ToggleShellWindowState() => ToggleWindowState();

    public void CloseFromShell() => Close();

    public void LockSessionFromShell() => ShowLoginView();

    public void LogoutFromShell() => AppServices.Auth.Logout();

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
