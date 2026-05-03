using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class CashInOutDialog : ContentDialog
{
    private bool _isCashIn = true;

    public CashInOutDialog()
    {
        InitializeComponent();
        Opened += CashInOutDialog_Opened;
        UpdateModeButtons();
        UpdatePrimaryState();
    }

    public bool IsCashIn => _isCashIn;

    public long AmountCents => (long)Math.Round(ParseAmount() * 100m);

    public string Reason => ReasonBox.Text?.Trim() ?? string.Empty;

    public void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void CashInOutDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        AmountBox.Focus(FocusState.Programmatic);
    }

    private void CashInButton_Click(object sender, RoutedEventArgs e)
    {
        _isCashIn = true;
        UpdateModeButtons();
    }

    private void CashOutButton_Click(object sender, RoutedEventArgs e)
    {
        _isCashIn = false;
        UpdateModeButtons();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        UpdatePrimaryState();
    }

    private void CashInOutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (AmountCents <= 0)
        {
            ShowError(LocalizationHelper.GetString("CashInOutDialog_Error_Amount"));
            args.Cancel = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ShowError(LocalizationHelper.GetString("CashInOutDialog_Error_Reason"));
            args.Cancel = true;
        }
    }

    private void UpdatePrimaryState()
    {
        IsPrimaryButtonEnabled = AmountCents > 0 && !string.IsNullOrWhiteSpace(Reason);
    }

    private void UpdateModeButtons()
    {
        CashInButton.Style = (Style)Resources[_isCashIn ? "CashInSelectedButtonStyle" : "CashModeButtonStyle"];
        CashOutButton.Style = (Style)Resources[_isCashIn ? "CashModeButtonStyle" : "CashOutSelectedButtonStyle"];
    }

    private decimal ParseAmount()
    {
        var input = AmountBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0m;
        }

        if (decimal.TryParse(
                input,
                NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture,
                out var value))
        {
            return value;
        }

        var normalized = input
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        return decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.CurrentCulture,
            out value)
            ? value
            : 0m;
    }
}
