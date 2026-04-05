using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace RetailStorePOS.App;

public partial class HeroUiShellView : UserControl
{
    private const string HeroUiHostName = "appassets.nexill";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private bool _initializationStarted;
    private bool _navigationCompleted;
    private bool _isSuspended;
    private bool _frontendReady;
    private readonly TaskCompletionSource<bool> _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HeroUiShellView()
    {
        InitializeComponent();
        Loaded += HeroUiShellView_Loaded;
        IsVisibleChanged += HeroUiShellView_IsVisibleChanged;
    }

    public void RefreshShellState()
    {
        if (!IsVisible || _isSuspended || ShellBrowser.CoreWebView2 is null)
        {
            return;
        }

        SendShellSnapshot();
    }

    public async Task PrepareAsync()
    {
        await EnsureShellReadyAsync();

        if (_frontendReady)
        {
            return;
        }

        await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private async void HeroUiShellView_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
        {
            await EnsureShellReadyAsync();
        }
    }

    private async void HeroUiShellView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await EnsureShellReadyAsync();
            return;
        }
    }

    private async System.Threading.Tasks.Task EnsureShellReadyAsync()
    {
        if (_navigationCompleted && ShellBrowser.CoreWebView2 is CoreWebView2 existingWebView)
        {
            if (_isSuspended)
            {
                existingWebView.Resume();
                _isSuspended = false;
            }

            ShellBrowser.Visibility = Visibility.Visible;
            LoadingState.Visibility = _frontendReady ? Visibility.Collapsed : Visibility.Visible;
            SendShellSnapshot();
            return;
        }

        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        ShellBrowser.NavigationCompleted += ShellBrowser_NavigationCompleted;

        var bundleFolder = Path.Combine(AppContext.BaseDirectory, "HeroUi");
        var indexPath = Path.Combine(bundleFolder, "index.html");

        if (!File.Exists(indexPath))
        {
            ShowStatus(
                "HeroUI shell build not found",
                "Run npm install and npm run build in UI/hero-ui-frontend, then rebuild the desktop app.");
            return;
        }

        try
        {
            var options = WebView2EnvironmentFactory.CreateShellOptions();
            var environment = await WebView2EnvironmentFactory.CreateAsync("Shell", options);
            await ShellBrowser.EnsureCoreWebView2Async(environment);

            if (ShellBrowser.CoreWebView2 is CoreWebView2 webView)
            {
                webView.WebMessageReceived -= ShellBrowser_WebMessageReceived;
                webView.WebMessageReceived += ShellBrowser_WebMessageReceived;
                webView.SetVirtualHostNameToFolderMapping(
                    HeroUiHostName,
                    bundleFolder,
                    CoreWebView2HostResourceAccessKind.Allow);
                webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.Settings.AreDefaultContextMenusEnabled = false;
                webView.Settings.AreDevToolsEnabled = false;
                webView.Settings.IsStatusBarEnabled = false;
                webView.Settings.IsZoomControlEnabled = false;
            }

            var bundleVersion = File.GetLastWriteTimeUtc(indexPath).Ticks;
            ShellBrowser.Source = new Uri($"https://{HeroUiHostName}/index.html?mode=shell&v={bundleVersion}");
        }
        catch (Exception ex)
        {
            _initializationStarted = false;
            ShowStatus("Could not load the HeroUI shell", ex.Message);
            AppServices.ReportException(ex, "HeroUiShellView.EnsureShellReadyAsync");
        }
    }

    private void ShellBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ReportHostError(
                "Could not render the HeroUI shell",
                $"WebView2 navigation failed with status: {e.WebErrorStatus}.");
            return;
        }

        _navigationCompleted = true;
        ShellBrowser.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Visible;
        SendShellSnapshot();
    }

    private async System.Threading.Tasks.Task SuspendShellAsync()
    {
        if (ShellBrowser.CoreWebView2 is not CoreWebView2 webView || _isSuspended)
        {
            ShellBrowser.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            await webView.TrySuspendAsync();
            _isSuspended = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to suspend HeroUI shell WebView: {ex.Message}");
            AppServices.ReportException(ex, "HeroUiShellView.SuspendShellAsync");
        }

        ShellBrowser.Visibility = Visibility.Collapsed;
    }

    private void ShellBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var request = JsonSerializer.Deserialize<HeroUiShellRequest>(e.WebMessageAsJson, SerializerOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return;
            }

            HandleRequest(request);
        }
        catch (Exception ex)
        {
            ReportHostError("Unable to process HeroUI shell request", ex.Message);
            AppServices.ReportException(ex, "HeroUiShellView.ShellBrowser_WebMessageReceived");
        }
    }

    private void HandleRequest(HeroUiShellRequest request)
    {
        if (request.Type == "frontendReady")
        {
            _frontendReady = true;
            _readyTcs.TrySetResult(true);
            ShellBrowser.Visibility = Visibility.Visible;
            LoadingState.Visibility = Visibility.Collapsed;
            SendShellSnapshot();
            return;
        }

        if (request.Type == "frontendError")
        {
            _readyTcs.TrySetException(new InvalidOperationException(request.Message ?? "Unknown frontend runtime error."));
            ReportHostError("HeroUI shell runtime error", request.Message ?? "Unknown frontend runtime error.");
            return;
        }

        var mainWindow = Window.GetWindow(this) as MainWindow;
        var mainViewModel = mainWindow?.DataContext as MainViewModel;

        switch (request.Type)
        {
            case "requestShellSnapshot":
                SendShellSnapshot();
                break;

            case "selectShellTab":
                if (mainWindow != null && !string.IsNullOrWhiteSpace(request.TabId))
                {
                    mainWindow.SelectTab(request.TabId);
                }
                break;

            case "minimizeWindow":
                mainWindow?.MinimizeFromShell();
                break;

            case "toggleWindowState":
                mainWindow?.ToggleShellWindowState();
                break;

            case "closeWindow":
                mainWindow?.CloseFromShell();
                break;

            case "lockSession":
                mainWindow?.LockSessionFromShell();
                break;

            case "logout":
                mainWindow?.LogoutFromShell();
                break;

            case "openUpdateLink":
                mainViewModel?.OpenUpdateLinkCommand.Execute(null);
                break;

            case "dismissUpdate":
                mainViewModel?.DismissUpdateCommand.Execute(null);
                break;
        }
    }

    private void SendShellSnapshot()
    {
        if (ShellBrowser.CoreWebView2 is not CoreWebView2 webView)
        {
            return;
        }

        try
        {
            var snapshot = BuildShellSnapshot();
            var envelope = new HeroUiShellEnvelope("shellSnapshot", snapshot);
            webView.PostWebMessageAsJson(JsonSerializer.Serialize(envelope, SerializerOptions));
        }
        catch (Exception ex)
        {
            ReportHostError("Unable to sync shell state", ex.Message);
            AppServices.ReportException(ex, "HeroUiShellView.SendShellSnapshot");
        }
    }

    private HeroUiShellSnapshot BuildShellSnapshot()
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        var mainViewModel = mainWindow?.DataContext as MainViewModel;

        var tabs = new List<HeroUiShellTab>
        {
            new("checkout", "Checkout", AppServices.Auth.CanCheckout),
            new("products", "Products", AppServices.Auth.CanManageProducts),
            new("reports", "Reports", AppServices.Auth.CanViewReports),
            new("users", "Users", AppServices.Auth.CanManageUsers),
            new("settings", "Settings", AppServices.Auth.CanManageSettings),
            new("help", "About", true),
        };

        var selectedTab = mainWindow?.GetSelectedTabId() ?? tabs.First(tab => tab.Enabled).Id;
        var currentUser = AppServices.Auth.CurrentUser;

        return new HeroUiShellSnapshot(
            selectedTab,
            tabs,
            currentUser?.DisplayName ?? "No active session",
            currentUser?.IsAdmin == true ? "Administrator" : currentUser is not null ? "Cashier" : "Signed out",
            mainViewModel?.IsUpdateAvailable == true,
            mainViewModel?.UpdateInfo?.VersionString ?? string.Empty,
            mainWindow?.WindowState == WindowState.Maximized);
    }

    private void ReportHostError(string title, string message)
    {
        ShowStatus(title, message);

        if (ShellBrowser.CoreWebView2 is not CoreWebView2 webView)
        {
            return;
        }

        var envelope = new HeroUiShellEnvelope("hostError", new HeroUiShellError(title, message));
        webView.PostWebMessageAsJson(JsonSerializer.Serialize(envelope, SerializerOptions));
    }

    private void ShowStatus(string title, string message)
    {
        ShellBrowser.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Visible;
        LoadingTitleText.Text = title;
        LoadingMessageText.Text = message;
    }
}

internal sealed record HeroUiShellEnvelope(string Type, object Payload);

internal sealed record HeroUiShellRequest(string Type, string? TabId = null, string? Message = null);

internal sealed record HeroUiShellError(string Title, string Message);

internal sealed record HeroUiShellSnapshot(
    string SelectedTab,
    IReadOnlyList<HeroUiShellTab> Tabs,
    string CurrentUser,
    string CurrentUserRole,
    bool IsUpdateAvailable,
    string UpdateVersion,
    bool IsMaximized);

internal sealed record HeroUiShellTab(string Id, string Label, bool Enabled);
