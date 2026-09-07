using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.UI.Settings.ViewModels;
using RetailStorePOS.UI.Common;
using System.Collections.Specialized;
using System.Collections;
using Microsoft.UI.Dispatching;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class StoreManagementPage : Page
{
    public SettingsViewModel ViewModel { get; private set; } = null!;

    private readonly DispatcherQueue _uiDispatcher;

    public StoreManagementPage()
    {
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            if (ViewModel != null)
            {
                DetachPageSubscriptions();
            }
            ViewModel = vm;
            AttachPageSubscriptions();
            Bindings.Update();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        DetachPageSubscriptions();
    }

    private void AttachPageSubscriptions()
    {
        if (ViewModel == null) return;

        ViewModel.RegionOptions.CollectionChanged += RegionOptions_CollectionChanged;
        ViewModel.CurrencyOptions.CollectionChanged += CurrencyOptions_CollectionChanged;
        ViewModel.PrinterOptions.CollectionChanged += PrinterOptions_CollectionChanged;
        ViewModel.LanguageOptions.CollectionChanged += LanguageOptions_CollectionChanged;

        SyncCollectionToItemsControl(ViewModel.RegionOptions, RegionOptionsComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.CurrencyOptions, CurrencyOptionsComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.PrinterOptions, PrinterOptionsComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.LanguageOptions, LanguageOptionsComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void DetachPageSubscriptions()
    {
        if (ViewModel == null) return;

        ViewModel.RegionOptions.CollectionChanged -= RegionOptions_CollectionChanged;
        ViewModel.CurrencyOptions.CollectionChanged -= CurrencyOptions_CollectionChanged;
        ViewModel.PrinterOptions.CollectionChanged -= PrinterOptions_CollectionChanged;
        ViewModel.LanguageOptions.CollectionChanged -= LanguageOptions_CollectionChanged;
    }

    private void RegionOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.RegionOptions, RegionOptionsComboBox.Items, e));

    private void CurrencyOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.CurrencyOptions, CurrencyOptionsComboBox.Items, e));

    private void PrinterOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.PrinterOptions, PrinterOptionsComboBox.Items, e));

    private void LanguageOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.LanguageOptions, LanguageOptionsComboBox.Items, e));

    private void SyncCollectionToItemsControl(IList source, ItemCollection target, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    target.Remove(item);
                }
            }
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (e.NewStartingIndex >= 0 && e.NewStartingIndex < target.Count)
                    {
                        target.Insert(e.NewStartingIndex, item);
                    }
                    else
                    {
                        target.Add(item);
                    }
                }
            }
        }
    }

    private async void SaveStoreSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
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

                    Microsoft.UI.Xaml.Application.Current.Exit();
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.StoreManagementPage.SaveStoreSettingsButton_Click");
        }
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

            Microsoft.UI.Xaml.Application.Current.Exit();
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
