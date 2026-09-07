using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using RetailStorePOS.Data;
using RetailStorePOS.WinUiLogin.Common;
using Windows.Globalization;
using RetailStorePOS.Data.Modules.Settings;
using Windows.ApplicationModel;

namespace RetailStorePOS.WinUiLogin;

public partial class App : Application
{
    private const string FallbackAppUserModelId = "Nexill.RetailStorePOS.WinUiLogin";
    private Window? _window;

    public App()
    {
        // Initialize the resource language without touching the database. The
        // persisted app language is applied after LoginRuntime finishes opening
        // the database so the native splash path is not blocked by migrations.
        InitializeLanguage();

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
            // Notification registration is not required to render the login
            // window. In dev/package layouts without a COM notification server
            // this throws and can add seconds plus crash-log pressure to every
            // launch, so keep it out of the startup path.
            StartupTrace.Write("AppNotificationManager.Register deferred");
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
            EnsureLocalLaunchDependencies();

            if (_window is MainWindow existingWindow)
            {
                StartupTrace.Write("App.OnLaunched:reuse existing window");
                existingWindow.Activate();
                StartupTrace.Write("App.OnLaunched:end(existing)");
                return;
            }

            _window = new MainWindow();
            _window.Closed += (_, _) =>
            {
                _window = null;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        LoginRuntime.Telemetry?.StopBackgroundSync();

                        if (!LoginRuntime.IsFreshStartResetRequested && LoginRuntime.Telemetry is not null)
                        {
                            var state = LoginRuntime.Telemetry.GetTelemetryState();

                            // Only log clean close if we have an active run to correlate with
                            if (!string.IsNullOrEmpty(state.ActiveRunId))
                            {
                                await LoginRuntime.Telemetry.LogAppClosedAsync(state.ActiveRunId, "window_closed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteCrashLog("AppClosed", ex);
                        LoginRuntime.ReportException(ex, "WinUiLogin.AppClosed");
                    }
                    finally
                    {
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
        e.Handled = e.Exception is not Microsoft.UI.Xaml.Markup.XamlParseException;
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

    internal static void WriteCrashLog(string phase, Exception ex)
    {
        try
        {
            var logPath = AppDataPaths.Combine("Logs", "winui-login-crash.log");
            EnsureLocalLaunchDependencies(logPath);
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

    private static void EnsureLocalLaunchDependencies()
    {
        EnsureLocalLaunchDependencies(AppDataPaths.Combine("Logs", "winui-login-crash.log"));
    }

    private static void EnsureLocalLaunchDependencies(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("App launch artifacts must remain on local storage.");
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
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetProcessPreferredUILanguages(uint dwFlags, string pwszLanguagesBuffer, out uint pdwNumLanguages);

    private const uint MUI_LANGUAGE_NAME = 0x8;

    private void InitializeLanguage()
    {
        StartupTrace.Write("App.InitializeLanguage:start");

        try
        {
            string? languageTag = null;
            try
            {
                languageTag = ApplicationLanguages.PrimaryLanguageOverride;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(languageTag))
            {
                try
                {
                    var languages = ApplicationLanguages.Languages;
                    languageTag = languages.Count > 0 ? languages[0] : null;
                }
                catch
                {
                }
            }

            var lang = LocalizationHelper.NormalizeLanguageTag(languageTag);

            if (!string.IsNullOrEmpty(lang))
            {
                // 1. FORCE THE PROCESS (Win32) - This fixes x:Uid mixed languages
                try
                {
                    SetProcessPreferredUILanguages(MUI_LANGUAGE_NAME, lang + "\0", out _);
                    StartupTrace.Write($"App.InitializeLanguage:Process language forced to {lang}");
                }
                catch (Exception ex)
                {
                    StartupTrace.Write($"App.InitializeLanguage:Failed to force process language: {ex.Message}");
                }

                // 2. Set the C# override via reactive service
                LocalizationService.Instance.SetLanguage(lang);
                StartupTrace.Write($"App.InitializeLanguage:Override set to {lang} via reactive service");
            }
        }
        catch (RetailStorePOS.Data.Modules.Migrations.DatabaseTooNewException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"App.InitializeLanguage:Failed to load language: {ex.Message}");
            StartupTrace.Write($"App.InitializeLanguage:Falling back to {LocalizationHelper.DefaultLanguage}");
        }
    }
}
