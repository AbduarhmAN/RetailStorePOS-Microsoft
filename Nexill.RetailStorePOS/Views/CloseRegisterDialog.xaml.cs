using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.Data.Modules.Sales;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class CloseRegisterDialog : ContentDialog
{
    private bool _isCashAdjustmentExpanded;

    public RegisterCloseSummary? Summary { get; private set; }
    public ContentDialogResult ActionResult { get; private set; } = ContentDialogResult.None;

    public long CountedCashCents => (long)Math.Round(ParseCountedCash() * 100m);
    public string CountedCashText => CountedCashInputBox.Text ?? string.Empty;
    public string Note => ClosingNoteBox.Text;

    public CloseRegisterDialog(RegisterCloseSummary summary, string? countedCashText = null, string? note = null)
    {
        this.InitializeComponent();
        this.Summary = summary;
        this.Opened += CloseRegisterDialog_Opened;
        CountedCashInputBox.Text = countedCashText ?? string.Empty;
        ClosingNoteBox.Text = note ?? string.Empty;
        
        Loc.PropertyChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() => Bindings.Update());
        };

        UpdateUi();
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string CloseRegisterDialog_Title => Loc["CloseRegisterDialog_Title.Text"];
    public string CloseRegisterDialog_ReviewText => Loc["CloseRegisterDialog_ReviewText.Text"];
    public string CloseRegisterDialog_SessionMetaText => Loc["CloseRegisterDialog_SessionMetaText.Text"];
    public string CloseRegisterDialog_SessionTotalLabel => Loc["CloseRegisterDialog_SessionTotalLabel.Text"];
    public string CloseRegisterDialog_ErrorBar => Loc["CloseRegisterDialog_ErrorBar.Title"];
    public string CloseRegisterDialog_CashLabel => Loc["CloseRegisterDialog_CashLabel.Text"];
    public string CloseRegisterDialog_OpeningLabel => Loc["CloseRegisterDialog_OpeningLabel.Text"];
    public string CloseRegisterDialog_CashInOutLabel => Loc["CloseRegisterDialog_CashInOutLabel.Text"];
    public string CloseRegisterDialog_CountedLabel => Loc["CloseRegisterDialog_CountedLabel.Text"];
    public string CloseRegisterDialog_DifferenceLabel => Loc["CloseRegisterDialog_DifferenceLabel.Text"];
    public string CloseRegisterDialog_CountedCashLabel => Loc["CloseRegisterDialog_CountedCashLabel.Text"];
    public string CloseRegisterDialog_CountedCashInputBox => Loc["CloseRegisterDialog_CountedCashInputBox.PlaceholderText"];
    public string CloseRegisterDialog_ClosingNoteLabel => Loc["CloseRegisterDialog_ClosingNoteLabel.Text"];
    public string CloseRegisterDialog_ClosingNoteBox => Loc["CloseRegisterDialog_ClosingNoteBox.PlaceholderText"];
    public string CloseRegisterDialog_FooterHint => Loc["CloseRegisterDialog_FooterHint.Text"];
    public string CloseRegisterDialog_CloseRegisterButton => Loc["CloseRegisterDialog_CloseRegisterButton.Content"];
    public string CloseRegisterDialog_CashInOutButton => Loc["CloseRegisterDialog_CashInOutButton.Content"];
    public string CloseRegisterDialog_DiscardButton => Loc["CloseRegisterDialog_DiscardButton.Content"];

    public void RefreshSummary(RegisterCloseSummary summary)
    {
        Summary = summary;
        UpdateUi();
    }

    private void UpdateUi()
    {
        if (Summary == null) return;

        SessionTotalsText.Text = BuildSessionTotalsText(Summary.OrderCount, Summary.SessionTotalCents);
        OpeningAmountText.Text = FormatPlainAmount(Summary.OpeningAmountCents);
        CashInOutAmountText.Text = BuildSignedMoney(Summary.NetCashAdjustmentCents);
        ExpectedCashText.Text = FormatExpectedCash(Summary.ExpectedCashCents);
        // ExpectedCardText.Text = FormatPlainAmount(Summary.ExpectedCardCents);
        SessionMetaText.Text = BuildSessionMetaText(Summary.OpenedAt);
        UpdateCashAdjustmentEntries();
        UpdateDifference();
    }

    private void UpdateDifference()
    {
        if (Summary == null) return;

        decimal expected = Summary.ExpectedCashCents / 100m;
        decimal counted = ParseCountedCash();
        decimal diff = counted - expected;

        CountedAmountText.Text = string.IsNullOrWhiteSpace(CountedCashInputBox.Text)
            ? "0.00"
            : CountedCashInputBox.Text;
        DifferenceText.Text = diff > 0
            ? "+" + FormatPlainAmount((long)Math.Round(diff * 100m))
            : diff < 0
                ? "-" + FormatPlainAmount((long)Math.Round(Math.Abs(diff) * 100m))
                : "0.00";
        if (diff != 0)
        {
            DifferenceText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 153, 27, 27));
            return;
        }

        DifferenceText.Foreground = OpeningAmountText.Foreground;
    }

    private string FormatPlainAmount(long cents) => CurrencyDisplayHelper.FormatNumber(cents / 100m);

    private string FormatExpectedCash(long cents)
        => CurrencyDisplayHelper.FormatAmount(cents / 100m, CurrencyDisplayHelper.ResolveConfiguredCurrencyCode());

    private string BuildSignedMoney(long cents)
    {
        if (cents == 0)
        {
            return FormatPlainAmount(0);
        }

        var sign = cents > 0 ? "+" : "-";
        return $"{sign}{FormatPlainAmount(Math.Abs(cents))}";
    }

    private void UpdateCashAdjustmentEntries()
    {
        CashAdjustmentEntriesPanel.Children.Clear();

        if (Summary?.CashAdjustments is not { Count: > 0 } cashAdjustments)
        {
            CashAdjustmentEntriesPanel.Children.Add(new TextBlock
            {
                Text = LocalizationHelper.GetString("CloseRegisterDialog_NoAdjustments"),
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 71, 85, 105)),
                TextWrapping = TextWrapping.WrapWholeWords
            });

            UpdateCashAdjustmentExpansionState();
            return;
        }

        foreach (var adjustment in cashAdjustments)
        {
            CashAdjustmentEntriesPanel.Children.Add(CreateCashAdjustmentRow(adjustment));
        }

        UpdateCashAdjustmentExpansionState();
    }

    private Grid CreateCashAdjustmentRow(RegisterCashAdjustmentSummary adjustment)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = BuildCashAdjustmentEntryLabel(adjustment),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 71, 85, 105)),
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var amount = new TextBlock
        {
            Text = BuildSignedMoney(adjustment.IsCashIn ? adjustment.AmountCents : -adjustment.AmountCents),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 15, 23, 42)),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        Grid.SetColumn(amount, 1);
        grid.Children.Add(label);
        grid.Children.Add(amount);
        return grid;
    }

    private static string BuildCashAdjustmentEntryLabel(RegisterCashAdjustmentSummary adjustment)
    {
        var typeLabel = adjustment.IsCashIn 
            ? LocalizationHelper.GetString("CloseRegisterDialog_In") 
            : LocalizationHelper.GetString("CloseRegisterDialog_Out");
        var note = string.IsNullOrWhiteSpace(adjustment.Reason) ? string.Empty : $" - {adjustment.Reason}";
        return $"{typeLabel}{note}";
    }

    private void UpdateCashAdjustmentExpansionState()
    {
        CashAdjustmentChevronIcon.Glyph = _isCashAdjustmentExpanded ? "\uE70E" : "\uE70D";
        CashAdjustmentEntriesPanel.Visibility = _isCashAdjustmentExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private string BuildSessionTotalsText(int orderCount, long sessionTotalCents)
    {
        var orderLabelKey = orderCount == 1 ? "CloseRegisterDialog_Order" : "CloseRegisterDialog_Orders";
        var orderLabel = LocalizationHelper.GetString(orderLabelKey);
        return $"{orderCount} {orderLabel}: {FormatPlainAmount(sessionTotalCents)}";
    }

    private static string BuildSessionMetaText(string openedAt)
    {
        if (DateTimeOffset.TryParse(openedAt, out var parsed))
        {
            var dateStr = parsed.LocalDateTime.ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture);
            return LocalizationHelper.Format("CloseRegisterDialog_SessionOpenedAt", dateStr);
        }

        return LocalizationHelper.GetString("CloseRegisterDialog_SessionMetaText");
    }

    private void CloseRegisterDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        ActionResult = ContentDialogResult.None;
        CountedCashInputBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void CloseRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        ActionResult = ContentDialogResult.Primary;
        Hide();
    }

    private void CashInOutFooterButton_Click(object sender, RoutedEventArgs e)
    {
        ActionResult = ContentDialogResult.Secondary;
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ActionResult = ContentDialogResult.None;
        Hide();
    }

    private void CashAdjustmentExpanderButton_Click(object sender, RoutedEventArgs e)
    {
        _isCashAdjustmentExpanded = !_isCashAdjustmentExpanded;
        UpdateCashAdjustmentExpansionState();
    }

    private void CountedCashInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDifference();
    }

    private decimal ParseCountedCash()
    {
        var input = CountedCashInputBox.Text?.Trim();
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

    public void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }
}
