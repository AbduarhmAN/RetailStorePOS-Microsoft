using System;
using Microsoft.UI.Xaml;

namespace TestRunner.Sales.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            StartupTrace.Write("MainWindow.ctor: InitializeComponent start");
            this.InitializeComponent();
            StartupTrace.Write("MainWindow.ctor: InitializeComponent end");
            this.ExtendsContentIntoTitleBar = true;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"MainWindow.ctor FATAL EXCEPTION: {ex}");
            System.Diagnostics.Debug.WriteLine($"MainWindow.ctor FATAL EXCEPTION: {ex}");
            if (ex.InnerException != null)
            {
                StartupTrace.Write($"MainWindow.ctor INNER EXCEPTION: {ex.InnerException}");
                System.Diagnostics.Debug.WriteLine($"MainWindow.ctor INNER EXCEPTION: {ex.InnerException}");
            }
            throw;
        }
    }
}
