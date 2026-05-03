using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;
using RetailStorePOS.WinUiLogin.Common;

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

        if (ViewModel.HasPendingAppLanguageRestart)
        {
            var restartDecision = new ContentDialog
            {
                Title = LocalizationHelper.GetString("Settings_Dialog_RestartRequired_Title"),
                Content = LocalizationHelper.GetString("Settings_Dialog_RestartRequired_Content"),
                PrimaryButtonText = LocalizationHelper.GetString("Settings_Action_RestartNow"),
                SecondaryButtonText = LocalizationHelper.GetString("Settings_Action_SaveRestartLater"),
                CloseButtonText = LocalizationHelper.GetString("Settings_Action_Discard"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var restartDecisionResult = await restartDecision.ShowAsync();
            if (restartDecisionResult == ContentDialogResult.Primary)
            {
                if (!ViewModel.TrySaveStoreSettings())
                {
                    return;
                }

                if (!TryRelaunchFreshInstance())
                {
                    ViewModel.StatusMessage = LocalizationHelper.GetString("Settings_Status_Reset_ManualRestart");
                    return;
                }

                MainWindow.Current?.Close();
                return;
            }

            if (restartDecisionResult == ContentDialogResult.Secondary)
            {
                ViewModel.TrySaveStoreSettings();
                return;
            }

            ViewModel.RestoreStoreManagementDraft();
            Bindings.Update();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = LocalizationHelper.GetString("Settings_Dialog_SaveStore_Title"),
            Content = LocalizationHelper.GetString("Settings_Dialog_SaveStore_Content"),
            PrimaryButtonText = LocalizationHelper.GetString("Settings_Action_Save"),
            CloseButtonText = LocalizationHelper.GetString("Settings_Action_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.TrySaveStoreSettings();
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
                Title = LocalizationHelper.GetString("Settings_Dialog_Reset_Title"),
                Content = LocalizationHelper.GetString("Settings_Dialog_Reset_Content"),
                PrimaryButtonText = LocalizationHelper.GetString("Settings_Action_Reset"),
                CloseButtonText = LocalizationHelper.GetString("Settings_Action_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ViewModel.StatusMessage = LocalizationHelper.GetString("Settings_Status_Resetting");
            await LoginRuntime.ResetFreshStartAsync();

            if (!TryRelaunchFreshInstance())
            {
                LoginRuntime.CancelFreshStartReset();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Settings_Status_Reset_ManualRestart");
                return;
            }

            MainWindow.Current?.Close();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.StoreManagementPage.ResetAppDataButton_Click");
            ViewModel.StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Reset");
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
