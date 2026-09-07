using Microsoft.UI.Xaml;
using RetailStorePOS.UI.Common;
using System;
using System.Runtime.InteropServices;
using RetailStorePOS.Data.Models;

namespace TestRunner.Products.App;

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
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            // Use a sandbox database for the Test Runner so we always get a fresh Start
            LoginRuntime.Initialize("TestRunnerProductsDb");

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
                StartupTrace.Write($"App.OnLaunched FATAL EXCEPTION INNER: {ex.InnerException}");
                System.Diagnostics.Debug.WriteLine($"App.OnLaunched FATAL EXCEPTION INNER: {ex.InnerException}");
            }
        }
    }
}
