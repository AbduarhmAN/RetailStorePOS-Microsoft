using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class StoreManagementPage : Page
{
    public SettingsViewModel ViewModel { get; private set; } = null!;

    public StoreManagementPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }

    private async void SaveStoreSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null || !ViewModel.CanManageSettings)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Save store changes",
            Content = "Save the current store management changes? If you cancel, the page will restore the last saved values.",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            if (ViewModel.SaveStoreSettingsCommand.CanExecute(null))
            {
                ViewModel.SaveStoreSettingsCommand.Execute(null);
            }

            return;
        }

        ViewModel.RestoreStoreManagementDraft();
        Bindings.Update();
    }

    private async void ResetAppDataButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.CanManageSettings || XamlRoot is null)
            {
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Reset app data",
                Content = "This will delete the local catalog, users, receipts, preferences, secure credentials, and telemetry cache. The app will reopen like a fresh install.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ViewModel.StatusMessage = "Resetting app data...";
            await LoginRuntime.ResetFreshStartAsync();

            if (!TryRelaunchFreshInstance())
            {
                LoginRuntime.CancelFreshStartReset();
                ViewModel.StatusMessage = "App data was cleared. Please restart the app manually.";
                return;
            }

            MainWindow.Current?.Close();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.StoreManagementPage.ResetAppDataButton_Click");
            ViewModel.StatusMessage = "Unable to reset app data right now.";
        }
    }

    private static bool TryRelaunchFreshInstance()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.StoreManagementPage.TryRelaunchFreshInstance");
            return false;
        }
    }
}
