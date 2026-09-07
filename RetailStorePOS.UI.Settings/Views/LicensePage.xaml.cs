using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Services;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class LicensePage : Page
{
    private CancellationTokenSource? _operationCts;
    private string? _messageKey;

    public LicensePage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
    }

    private static bool CanManageLicense => LoginRuntime.Auth.IsLoggedIn
        && LoginRuntime.Auth.CurrentUser?.IsAdmin == true
        && LoginRuntime.Auth.CurrentUser?.MustChangePassword == false;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        LoginRuntime.Auth.LoginStateChanged += Auth_LoginStateChanged;
        LoginRuntime.License.SnapshotChanged += License_SnapshotChanged;
        LocalizationService.Instance.PropertyChanged += Localization_PropertyChanged;
        RefreshView();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        LoginRuntime.Auth.LoginStateChanged -= Auth_LoginStateChanged;
        LoginRuntime.License.SnapshotChanged -= License_SnapshotChanged;
        LocalizationService.Instance.PropertyChanged -= Localization_PropertyChanged;
        _operationCts?.Cancel();
        LicenseKeyBox.Password = string.Empty;
        _messageKey = null;
    }

    private void Auth_LoginStateChanged(object? sender, EventArgs e)
    {
        // A session change also invalidates a request started by the prior user.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded) return;
            _operationCts?.Cancel();
            LicenseKeyBox.Password = string.Empty;
            _messageKey = null;
            RefreshView();
        });
    }

    private void License_SnapshotChanged(object? sender, LicenseActivationSnapshot? snapshot) => QueueRefresh();

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e) => QueueRefresh();

    private void QueueRefresh() => DispatcherQueue.TryEnqueue(() =>
    {
        if (IsLoaded) RefreshView();
    });

    private static string Text(string key) => LocalizationHelper.GetString(key);

    private void RefreshView()
    {
        FlowDirection = LocalizationService.Instance.FlowDirection;
        TitleText.Text = Text("LicensePage_Title");
        DescriptionText.Text = Text("LicensePage_Description");
        LicenseKeyBox.Header = Text("LicensePage_KeyLabel");
        LicenseKeyBox.PlaceholderText = Text("LicensePage_KeyPlaceholder");
        ActivateButton.Content = Text("LicensePage_Activate");
        AccessText.Text = Text("LicensePage_AdminRequired");
        AccessText.Visibility = CanManageLicense ? Visibility.Collapsed : Visibility.Visible;
        var busy = _operationCts is not null;
        LicenseKeyBox.IsEnabled = CanManageLicense && !busy;
        ActivateButton.IsEnabled = CanManageLicense && !busy
            && !string.IsNullOrWhiteSpace(LicenseKeyBox.Password);
        ActivationProgress.IsActive = busy;
        ActivationProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        var snapshot = LoginRuntime.License.GetCurrentSnapshot();
        StatusText.Text = snapshot is not null && snapshot.IsCurrentlyValid(DateTimeOffset.UtcNow)
            ? string.Format(CultureInfo.CurrentCulture, Text("LicensePage_ActiveStatus"),
                snapshot.PermissionGroup, snapshot.ExpiresAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
            : Text("LicensePage_InactiveStatus");
        ResultInfo.IsOpen = _messageKey is not null;
        if (_messageKey is not null) ResultInfo.Message = Text(_messageKey);
    }

    private void LicenseKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ActivateButton is not null)
            ActivateButton.IsEnabled = CanManageLicense && _operationCts is null
                && !string.IsNullOrWhiteSpace(LicenseKeyBox.Password);
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCts is not null) return;
        try
        {
            LoginRuntime.Authorization.RequireAdmin();
            if (!CanManageLicense) throw new UnauthorizedAccessException();
        }
        catch (UnauthorizedAccessException)
        {
            ShowMessage("LicensePage_AdminRequired", InfoBarSeverity.Error);
            return;
        }

        var licenseKey = LicenseKeyBox.Password.Trim();
        LicenseKeyBox.Password = string.Empty;
        if (licenseKey.Length == 0)
        {
            ShowMessage("LicensePage_KeyRequired", InfoBarSeverity.Warning);
            return;
        }

        using var cts = new CancellationTokenSource();
        var initiatingUser = LoginRuntime.Auth.CurrentUser;
        _operationCts = cts;
        var token = cts.Token;
        _messageKey = null;
        RefreshView();
        try
        {
            // The verifier performs synchronous DPAPI, device identity and
            // SQLite work before HTTP awaits; keep that work off the dispatcher.
            var activation = await Task.Run(async () =>
            {
                if (!CanManageLicense || !ReferenceEquals(initiatingUser, LoginRuntime.Auth.CurrentUser))
                    cts.Cancel();
                token.ThrowIfCancellationRequested();
                var result = await LoginRuntime.License.ActivateAsync(licenseKey, token).ConfigureAwait(false);
                if (!CanManageLicense || !ReferenceEquals(initiatingUser, LoginRuntime.Auth.CurrentUser))
                    cts.Cancel();
                token.ThrowIfCancellationRequested();
                var keySaved = false;
                if (result.IsActivated)
                {
                    try
                    {
                        SecureStorageService.StoreSecret("LicenseKey", licenseKey);
                        keySaved = string.Equals(SecureStorageService.GetSecret("LicenseKey"), licenseKey,
                            StringComparison.Ordinal);
                    }
                    catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException
                        or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
                    {
                        // Preserve the verified session and report the local save failure.
                    }
                }
                return (Result: result, KeySaved: keySaved);
            }, token);

            if (!IsLoaded || token.IsCancellationRequested) return;
            if (activation.Result.IsActivated)
            {
                ShowMessage(activation.KeySaved ? "LicensePage_Success" : "LicensePage_SaveFailed",
                    activation.KeySaved ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
            else
            {
                var message = activation.Result.ErrorCode == "local_persist_failed"
                    ? "LicensePage_SaveFailed"
                    : activation.Result.Outcome switch
                    {
                        LicenseActivationOutcome.NetworkFailure => "LicensePage_NetworkFailed",
                        LicenseActivationOutcome.BackendNotConfigured => "LicensePage_NotConfigured",
                        LicenseActivationOutcome.InvalidLicenseKey => "LicensePage_KeyRequired",
                        LicenseActivationOutcome.BackendRejected => "LicensePage_Rejected",
                        _ => "LicensePage_VerificationFailed",
                    };
                ShowMessage(message, InfoBarSeverity.Error);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Leaving the page or changing the session cancels the local wait.
        }
        catch (Exception)
        {
            // Never surface exception text that may contain request or credential data.
            if (IsLoaded && !token.IsCancellationRequested)
                ShowMessage("LicensePage_UnexpectedFailure", InfoBarSeverity.Error);
        }
        finally
        {
            licenseKey = string.Empty;
            _operationCts = null;
            if (IsLoaded) RefreshView();
        }
    }

    private void ShowMessage(string key, InfoBarSeverity severity)
    {
        _messageKey = key;
        ResultInfo.Severity = severity;
        RefreshView();
    }
}
