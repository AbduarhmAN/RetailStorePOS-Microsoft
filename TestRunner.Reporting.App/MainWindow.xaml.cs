using System;
using Microsoft.UI.Xaml;

namespace TestRunner.Reporting.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow.ctor FATAL EXCEPTION: {ex}");
            throw;
        }
    }
}
