using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data;

namespace RetailStorePOS.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handling
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        SplashWindow? splash = null;

        try
        {
            splash = new SplashWindow();
            splash.Opacity = 0;
            MainWindow = splash;
            splash.Show();
            await AnimateOpacityAsync(splash, 0, 1, 180);
            splash.UpdateStatus("Starting services", "Loading local database and preparing the store workspace.");
            await splash.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            var databasePath = await Task.Run(() => InitializeApplicationCore());

            splash.UpdateStatus("Preparing interface", "Warming the HeroUI shell and desktop host.");
            await WebView2EnvironmentFactory.PreloadAsync("Shell", WebView2EnvironmentFactory.CreateShellOptions());

            await AppServices.Telemetry.ImportBootstrapLifecycleEventsAsync();
            _ = Task.Run(() => AppServices.Telemetry.LogAppLaunchAsync());

            splash.UpdateStatus("Opening workspace", "Almost ready.");
            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
                Opacity = 0,
                ShowInTaskbar = false
            };

            MainWindow = mainWindow;
            mainWindow.Show();
            await mainWindow.PrepareForStartupAsync();

            mainWindow.ShowInTaskbar = true;
            BringWindowToFront(mainWindow);

            var mainFadeTask = AnimateOpacityAsync(mainWindow, 0, 1, 180);
            var splashFadeTask = AnimateOpacityAsync(splash, splash.Opacity, 0, 160);
            await Task.WhenAll(mainFadeTask, splashFadeTask);

            splash.Close();
        }
        catch (Exception ex)
        {
            splash?.Close();
            var innerMsg = ex.InnerException != null ? $"\n\nInner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}" : "";
            var msg = $"Startup Error: {ex.Message}{innerMsg}\n\n{ex.StackTrace}";
            System.IO.File.WriteAllText("crash.log", msg);
            AppServices.Telemetry?.LogErrorAsync(ex, "Startup").Wait();
            MessageBox.Show(msg, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (AppServices.Telemetry is not null)
        {
            AppServices.Telemetry.LogAppClosedAsync("app_exit").GetAwaiter().GetResult();
            AppServices.Telemetry.StopBackgroundSync();
        }
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var msg = $"Application Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}";
        System.IO.File.WriteAllText("crash.log", msg);
        AppServices.Telemetry?.LogErrorAsync(e.Exception, "DispatcherUnhandledException").Wait();
        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var msg = $"Fatal Error: {ex.Message}\n\n{ex.StackTrace}";
            System.IO.File.WriteAllText("crash.log", msg);
            AppServices.Telemetry?.LogErrorAsync(ex, "CurrentDomain_UnhandledException").Wait();
            MessageBox.Show(msg, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string InitializeApplicationCore()
    {
        SQLitePCL.Batteries_V2.Init();

        var databasePath = DatabasePaths.GetDatabasePath("RetailStorePOS");
        Exception? backupException = null;

        try
        {
            new BackupService(databasePath).PerformBackup();
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(databasePath)!, "backup_error.log");
                var backupError = $"[{DateTime.Now}] Backup Error: {ex.Message}\n{ex.StackTrace}\n\n";
                System.IO.File.AppendAllText(logPath, backupError);
            }
            catch
            {
            }
            backupException = ex;
        }

        DatabaseInitializer.Initialize(databasePath);
        AppServices.Initialize(databasePath);

        if (backupException is not null)
        {
            AppServices.ReportException(backupException, "App.InitializeApplicationCore.Backup");
        }

        return databasePath;
    }

    private static void BringWindowToFront(Window mainWindow)
    {
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
        mainWindow.Focus();
    }

    private static Task AnimateOpacityAsync(Window window, double from, double to, int durationMs)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        window.Opacity = from;

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            window.Opacity = to;
            tcs.TrySetResult(true);
        };

        window.BeginAnimation(Window.OpacityProperty, animation);
        return tcs.Task;
    }
}
