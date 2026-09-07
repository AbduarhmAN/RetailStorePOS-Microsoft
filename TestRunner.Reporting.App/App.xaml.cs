using Microsoft.UI.Xaml;
using RetailStorePOS.UI.Common;
using System;

namespace TestRunner.Reporting.App;

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
        System.Diagnostics.Debug.WriteLine($"App UNHANDLED EXCEPTION: {ex}");
        if (ex?.InnerException != null)
        {
            System.Diagnostics.Debug.WriteLine($"App UNHANDLED EXCEPTION INNER: {ex.InnerException}");
        }
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            // Use a sandbox database for the Test Runner so we always get a fresh Start
            LoginRuntime.Initialize();

            m_window = new MainWindow();
            m_window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App.OnLaunched FATAL EXCEPTION: {ex}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"App.OnLaunched FATAL EXCEPTION INNER: {ex.InnerException}");
            }
        }
    }
}
