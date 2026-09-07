using Microsoft.UI.Xaml;
using RetailStorePOS.UI.Common;
using System;
using System.Runtime.InteropServices;
using RetailStorePOS.Data.Models;

namespace TestRunner.Sales.App;

public partial class App : Application
{
    private Window? m_window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var ex = e.Exception;
        StartupTrace.Write($"App UNHANDLED EXCEPTION: {ex}");
        System.Diagnostics.Debug.WriteLine($"App UNHANDLED EXCEPTION: {ex}");
        if (ex?.InnerException != null)
        {
            StartupTrace.Write($"App UNHANDLED EXCEPTION INNER: {ex.InnerException}");
            System.Diagnostics.Debug.WriteLine($"App UNHANDLED EXCEPTION INNER: {ex.InnerException}");
        }
        // If we don't set Handled=true, the app will terminate, but at least we get the log.
        // Let's set it to true to see if it can recover.
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            // Force the application and Win32 process into Arabic
            ForceArabicLanguage();

            // Use a sandbox database for the Test Runner so we always get a fresh Start
            LoginRuntime.Initialize("TestRunnerSalesDb");

            // Seed a test cashier so we can test the Sales UI without full login
            SeedTestCashierAndBypassLogin();

            StartupTrace.Write("App.OnLaunched: Creating MainWindow start");
            m_window = new MainWindow();
            StartupTrace.Write("App.OnLaunched: Creating MainWindow end");

            StartupTrace.Write("App.OnLaunched: Activating MainWindow start");
            m_window.Activate();
            StartupTrace.Write("App.OnLaunched: Activating MainWindow end");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"App.OnLaunched FATAL EXCEPTION: {ex}");
            System.Diagnostics.Debug.WriteLine($"App.OnLaunched FATAL EXCEPTION: {ex}");
            if (ex.InnerException != null)
            {
                StartupTrace.Write($"App.OnLaunched INNER EXCEPTION: {ex.InnerException}");
                System.Diagnostics.Debug.WriteLine($"App.OnLaunched INNER EXCEPTION: {ex.InnerException}");
            }
            throw;
        }
    }

    private void SeedTestCashierAndBypassLogin()
    {
        var users = LoginRuntime.Users.GetAll();
        if (!System.Linq.Enumerable.Any(users, u => !u.IsAdmin && u.CanCheckout))
        {
            var testCashier = new User
            {
                Username = "testcashier",
                DisplayName = "Test Cashier",
                IsAdmin = false,
                CanCheckout = true,
                IsActive = true,
                MustChangePassword = false,
                PasswordHash = RetailStorePOS.Data.Modules.UsersAuth.UserRepository.HashPassword("1234"),
                PinHash = RetailStorePOS.Data.Modules.UsersAuth.UserRepository.HashPin("1234")
            };
            LoginRuntime.Users.Create(testCashier);
        }

        // Bypass the login screen entirely by logging in automatically
        var loginResult = LoginRuntime.Auth.LoginWithPin("testcashier", "1234");
        if (!loginResult.Success)
        {
            System.Diagnostics.Debug.WriteLine("Failed to auto-login Test Cashier: " + loginResult.ErrorMessage);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetProcessPreferredUILanguages(uint dwFlags, string pwszLanguagesBuffer, out uint pdwNumLanguages);

    private const uint MUI_LANGUAGE_NAME = 0x8;

    private void ForceArabicLanguage()
    {
        try
        {
            SetProcessPreferredUILanguages(MUI_LANGUAGE_NAME, "ar-SA\0", out _);
            LocalizationService.Instance.SetLanguage("ar-SA");
        }
        catch { }
    }
}
