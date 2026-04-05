using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using RetailStorePOS.Data;
using RetailStorePOS.WinUiLogin.Common;
using Windows.ApplicationModel;

namespace RetailStorePOS.WinUiLogin;

public partial class App : Application
{
    private const string FallbackAppUserModelId = "Nexill.RetailStorePOS.WinUiLogin";
    private Window? _window;

    public App()
    {
        StartupTrace.Write("App.ctor:start");

        // Only set explicit AppUserModelId if not running as a packaged app (like during local dev).
        // Store apps have an identity managed by the OS, and overriding it can cause crashes.
        if (!HasPackageIdentity())
        {
            ConfigureAppUserModelId();
        }

        StartupTrace.Write("App.ctor:before InitializeComponent");
        InitializeComponent();
        StartupTrace.Write("App.ctor:after InitializeComponent");

        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        if (HasPackageIdentity())
        {
            try
            {
                StartupTrace.Write("App.ctor:before AppNotificationManager.Register");
                AppNotificationManager.Default.Register();
                StartupTrace.Write("App.ctor:after AppNotificationManager.Register");
            }
            catch (Exception ex)
            {
                // CRITICAL FIX: Microsoft Store Cert environments might reject Toast Notifications
                // or throw a COMException if the manifest is missing the correct COM server extensions.
                // We catch it here so the app doesn't fatally crash before the MainWindow opens!
                StartupTrace.Write($"AppNotificationManager.Register failed: {ex.Message}");
                WriteCrashLog("AppNotificationManager.Register", ex);
            }
        }
        else
        {
            StartupTrace.Write("AppNotificationManager.Register skipped (no package identity)");
        }

        // LoginRuntime.Initialize() has been moved to MainWindow.Loaded
        // to allow the Splash Screen overlay to animate without thread blocking.

        StartupTrace.Write("App.ctor:end");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupTrace.Write("App.OnLaunched:start");
        try
        {
            _window = new MainWindow();
            _window.Closed += (_, _) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (!LoginRuntime.IsFreshStartResetRequested && LoginRuntime.Telemetry is not null)
                        {
                            await LoginRuntime.Telemetry.LogAppClosedAsync("window_closed");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteCrashLog("AppClosed", ex);
                        LoginRuntime.ReportException(ex, "WinUiLogin.AppClosed");
                    }
                    finally
                    {
                        LoginRuntime.Telemetry?.StopBackgroundSync();
                        try
                        {
                            if (HasPackageIdentity())
                            {
                                AppNotificationManager.Default.Unregister();
                            }
                        }
                        catch
                        {
                        }
                    }
                });
            };
            _window.Activate();

            if (LoginRuntime.Telemetry is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        StartupTrace.Write("Telemetry.ImportBootstrapLifecycleEvents:start");
                        await LoginRuntime.Telemetry.ImportBootstrapLifecycleEventsAsync();
                        StartupTrace.Write("Telemetry.ImportBootstrapLifecycleEvents:end");

                        StartupTrace.Write("Telemetry.LogAppLaunch:start");
                        await LoginRuntime.Telemetry.LogAppLaunchAsync();
                        StartupTrace.Write("Telemetry.LogAppLaunch:end");
                    }
                    catch (Exception ex)
                    {
                        WriteCrashLog("Telemetry.StartupSync", ex);
                        LoginRuntime.ReportException(ex, "WinUiLogin.Telemetry.StartupSync");
                    }
                });
            }

            StartupTrace.Write("App.OnLaunched:end");
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnLaunched", ex);
            LoginRuntime.ReportException(ex, "WinUiLogin.OnLaunched");
            throw;
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog("UnhandledException", e.Exception);
        LoginRuntime.ReportException(e.Exception, "WinUiLogin.UnhandledException");
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        WriteCrashLog("CurrentDomain.UnhandledException", exception);
        LoginRuntime.ReportException(exception, "WinUiLogin.CurrentDomain.UnhandledException");
    }

    private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        StartupTrace.Write("CurrentDomain.ProcessExit");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        foreach (var exception in e.Exception.Flatten().InnerExceptions)
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", exception);
            LoginRuntime.ReportException(exception, "WinUiLogin.TaskScheduler.UnobservedTaskException");
        }

        e.SetObserved();
    }

    private static void WriteCrashLog(string phase, Exception ex)
    {
        try
        {
            var logPath = AppDataPaths.Combine("Logs", "winui-login-crash.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:O}] Phase: {phase}");
            builder.AppendLine(ex.ToString());
            builder.AppendLine();

            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(logPath, builder.ToString());
        }
        catch
        {
        }
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ConfigureAppUserModelId()
    {
        try
        {
            var appUserModelId = Assembly.GetEntryAssembly()?
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "RetailStorePOSAppUserModelId")?
                .Value;

            appUserModelId = string.IsNullOrWhiteSpace(appUserModelId)
                ? FallbackAppUserModelId
                : appUserModelId;

            var hresult = SetCurrentProcessExplicitAppUserModelID(appUserModelId);
            StartupTrace.Write($"App.SetCurrentProcessExplicitAppUserModelID:{appUserModelId}:{hresult}");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"App.SetCurrentProcessExplicitAppUserModelID failed:{ex.Message}");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}


