using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using RetailStorePOS.Data.Modules.Sales;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class ReportsReceiptsPage : Page
{
    private static readonly Regex ExpectedCashCentsRegex = new(
        @"(?<prefix>\bExpected cash\s+)(?<cents>-?\d+)\s+cents\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ReportsViewModel ViewModel { get; } = new();

    public ReportsReceiptsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        ViewModel.XReportGenerated += ViewModel_XReportGenerated;
        ApplyLocalizedStaticText();
        ViewModel.Loc.PropertyChanged += (s, e) =>
        {
            Bindings.Update();
            ApplyLocalizedStaticText();
        };
    }

    public static string GetLoc(string key) => LocalizationService.Instance[key];



    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DispatcherQueue.TryEnqueue(() => ViewModel.RefreshCommand.Execute(null));
    }

    public static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy");
    public static string FormatTime(DateTime date) => date.ToString("hh:mm tt");
    public static string FormatCurrency(decimal amount) => CurrencyDisplayHelper.FormatConfiguredAmount(amount);

    private string? _latestXReportPdfPath;

    private async void ViewModel_XReportGenerated(object? sender, string pdfPath)
    {
        _latestXReportPdfPath = pdfPath;
        XReportStatusText.Text = string.Format(LocalizationHelper.GetString("ReportsReceipts_XReport_StatusFormat"), pdfPath);

        try
        {
            XReportDialog.XamlRoot = this.XamlRoot;
            await XReportDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.ShowXReportDialog");
        }
    }

    private async void XReportDialog_PrintClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            var sales = await Task.Run(() => LoginRuntime.Sales.GetSalesByDate(DateTime.Today));
            var storeName = LoginRuntime.Settings.GetStoreName() ?? LocalizationHelper.GetString("ReportsReceipts_StoreFallback");
            var preferredPrinterName = LoginRuntime.Settings.GetPreferredPrinterName();

            using var printHelper = new XReportPrintHelper();
            printHelper.PrintXReport(sales, DateTime.Today, storeName, preferredPrinterName);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.PrintXReport");
        }
    }

    private void XReportDialog_OpenPdfClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(_latestXReportPdfPath))
        {
            XReportHelper.TryOpenReportPdf(_latestXReportPdfPath);
        }
    }

    private void XReportDialog_CloseClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _latestXReportPdfPath = null;
    }

    private async void XReportHistoryButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var reports = await Task.Run(() => XReportHelper.GetAvailableReports());
            var rows = reports.Select(BuildXReportHistoryRow).ToList();
            if (rows.Count == 0)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Empty", "No saved X reports were found."));
                return;
            }

            XReportHistoryDialog.Title = LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List");
            XReportHistoryDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            XReportHistoryEmptyText.Visibility = Visibility.Collapsed;
            XReportHistoryList.ItemsSource = rows;
            XReportHistoryDialog.XamlRoot = XamlRoot;
            await XReportHistoryDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.ShowXReportHistory");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_ErrorFormat", "Unable to load the X report list. {0}"),
                    ex.Message));
        }
    }

    private async void RegisterReportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var history = await Task.Run(() => LoginRuntime.RegisterSessions.GetSessionHistory());
            var rows = history.Select(BuildRegisterSessionHistoryRow).ToList();
            if (rows.Count == 0)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryDialog_Title", "Register Sessions"),
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_NoSessions", "No register sessions were found."));
                return;
            }

            RegisterSessionHistoryDialog.Title = LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryDialog_Title", "Register Sessions");
            RegisterSessionHistoryDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            RegisterSessionHistoryEmptyText.Visibility = Visibility.Collapsed;
            RegisterSessionHistoryList.ItemsSource = rows;
            RegisterSessionHistoryDialog.XamlRoot = XamlRoot;
            await RegisterSessionHistoryDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.ShowRegisterReport");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryDialog_Title", "Register Sessions"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ErrorFormat", "Unable to load the register report. {0}"),
                    ex.Message));
        }
    }

    private async void XReportHistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not XReportHistoryRow row)
        {
            return;
        }

        XReportHistoryDialog.Hide();

        if (!XReportHelper.TryOpenReportPdf(row.PdfPath))
        {
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_OpenFailed", "Unable to open the selected X report PDF."));
        }
    }

    private async void RegisterSessionHistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not RegisterSessionHistoryRow row)
        {
            return;
        }

        RegisterSessionHistoryDialog.Hide();

        try
        {
            var report = await Task.Run(() => LoginRuntime.RegisterSessions.GetSessionReport(row.SessionId));
            if (report is null)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_Dialog_Title", "Register Report"),
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_SessionMissing", "The selected register session could not be loaded."));
                return;
            }

            PopulateRegisterReport(report);
            RegisterReportDialog.Title = string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_DetailTitleFormat", "Register Session #{0}"),
                report.SessionId);
            RegisterReportDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            RegisterReportDialog.XamlRoot = XamlRoot;
            await RegisterReportDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.OpenRegisterSessionReport");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_Dialog_Title", "Register Report"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ErrorFormat", "Unable to load the register report. {0}"),
                    ex.Message));
        }
    }

    private void ReprintButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RetailStorePOS.Data.Modules.Sales.Sale sale)
        {
            ViewModel.ReprintReceiptCommand.Execute(sale);
        }
    }

    private void PopulateRegisterReport(RegisterSessionReport report)
    {
        var openedAt = ParseStoredUtc(report.OpenedAt);
        var closedAt = ParseStoredUtc(report.ClosedAt);
        var isClosed = closedAt.HasValue;
        var openedBy = ResolveUserName(report.OpenedByUserId);
        var currentCashier = report.CashierIntervals
            .LastOrDefault(interval => string.IsNullOrWhiteSpace(interval.EndedAt)) is { } activeInterval
            ? ResolveUserName(activeInterval.UserId)
            : openedBy;
        var closedBy = report.ClosedByUserId.HasValue && report.ClosedByUserId.Value > 0
            ? ResolveUserName(report.ClosedByUserId.Value)
            : null;

        RegisterReportHeadlineText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HeadlineFormat", "Session #{0}"),
            report.SessionId);
        RegisterReportSessionMetaText.Text = string.Join(
            Environment.NewLine,
            string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_OpenedAtFormat", "Opened: {0}"),
                FormatLocalDateTime(openedAt)),
            string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosedAtFormat", "Closed: {0}"),
                isClosed
                    ? FormatLocalDateTime(closedAt)
                    : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosedAtPending", "Not yet")));
        RegisterReportOwnershipText.Text = !isClosed
            ? string.Join(
                Environment.NewLine,
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_OpenedByFormat", "Opened by: {0}"),
                    openedBy),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_CurrentCashierFormat", "Current cashier: {0}"),
                    currentCashier))
            : string.Join(
                Environment.NewLine,
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_OpenedByFormat", "Opened by: {0}"),
                    openedBy),
                closedBy is not null
                    ? string.Format(
                        LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosedByFormat", "Closed by: {0}"),
                        closedBy)
                    : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosedByUnknown", "Closed by: not recorded"));

        var noteParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(report.OpeningNote))
        {
            noteParts.Add(string.Join(
                Environment.NewLine,
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_OpeningNoteLabel", "Opening note"),
                NormalizeRegisterNote(report.OpeningNote)));
        }

        if (!string.IsNullOrWhiteSpace(report.ClosingNote))
        {
            noteParts.Add(string.Join(
                Environment.NewLine,
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosingNoteLabel", "Closing note"),
                NormalizeRegisterNote(report.ClosingNote)));
        }

        RegisterReportNotesText.Text = string.Join(Environment.NewLine + Environment.NewLine, noteParts);
        RegisterReportNotesText.Visibility = noteParts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        RegisterReportSalesValueText.Text = FormatCurrency(report.GrossSalesCents);
        RegisterReportSalesCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_SalesCaptionFormat", "{0} sale(s) • Tax {1}"),
            CurrencyDisplayHelper.FormatInt(report.SaleCount),
            FormatCurrency(report.TaxCents));

        RegisterReportCashValueText.Text = FormatCurrency(report.CashSalesCents);
        RegisterReportCashCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_CashCaptionFormat", "Adjustments {0}"),
            FormatSignedCurrency(report.NetCashAdjustmentCents));

        RegisterReportCardValueText.Text = FormatCurrency(report.CardSalesCents);
        RegisterReportCardCaptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_CardCaption",
            "Non-cash sales captured in this session");

        RegisterReportExpectedCashValueText.Text = FormatCurrency(report.ExpectedCashCents);
        RegisterReportExpectedCashCaptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_ExpectedCashCaption",
            "Opening float + cash sales + net adjustments");

        RegisterReportOpeningValueText.Text = FormatCurrency(report.OpeningAmountCents);
        RegisterReportOpeningCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_OpeningCaptionFormat", "Started by {0}"),
            openedBy);

        RegisterReportClosingValueText.Text = isClosed && report.ClosingAmountCents.HasValue
            ? FormatCurrency(report.ClosingAmountCents.Value)
            : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosingPending", "Pending");
        RegisterReportClosingCaptionText.Text = !isClosed
            ? LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosingCaptionOpen", "Recorded when the session is closed")
            : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ClosingCaptionClosed", "Actual cash counted in the drawer at close");

        var cashierSummaries = report.CashierSummaries
            .Select(summary => new RegisterReportCashierSummaryRow
            {
                CashierName = ResolveCashierSummaryName(summary),
                SaleCountText = string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_CashierSalesCountFormat", "{0} sale(s)"),
                    CurrencyDisplayHelper.FormatInt(summary.SaleCount)),
                GrossSalesText = FormatCurrency(summary.GrossSalesCents),
                BreakdownText = string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_CashierBreakdownFormat", "Cash {0} • Card {1}"),
                    FormatCurrency(summary.CashSalesCents),
                    FormatCurrency(summary.CardSalesCents))
            })
            .ToList();
        RegisterReportCashierSummaryList.ItemsSource = cashierSummaries;
        RegisterReportCashierSummaryEmptyText.Visibility = cashierSummaries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var cashierIntervals = report.CashierIntervals
            .Select(interval => new RegisterReportCashierIntervalRow
            {
                CashierName = ResolveUserName(interval.UserId),
                TimeRangeText = BuildIntervalTimeRange(interval.StartedAt, interval.EndedAt),
                NoteText = string.IsNullOrWhiteSpace(interval.Note) ? string.Empty : interval.Note.Trim(),
                NoteVisibility = string.IsNullOrWhiteSpace(interval.Note) ? Visibility.Collapsed : Visibility.Visible
            })
            .ToList();
        RegisterReportCashierIntervalList.ItemsSource = cashierIntervals;
        RegisterReportIntervalsEmptyText.Visibility = cashierIntervals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var adjustments = report.CashAdjustments
            .Select(adjustment => new RegisterReportAdjustmentRow
            {
                Reason = string.IsNullOrWhiteSpace(adjustment.Reason)
                    ? LocalizationHelper.GetString("ReportsReceipts_RegisterReport_AdjustmentReasonFallback", "Cash adjustment")
                    : adjustment.Reason.Trim(),
                DetailText = string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_AdjustmentDetailFormat", "{0} • {1}"),
                    ResolveUserName(adjustment.UserId),
                    FormatLocalDateTime(ParseStoredUtc(adjustment.CreatedAt))),
                AmountText = FormatSignedCurrency(adjustment.IsCashIn ? adjustment.AmountCents : -adjustment.AmountCents)
            })
            .ToList();
        RegisterReportAdjustmentsList.ItemsSource = adjustments;
        RegisterReportAdjustmentsEmptyText.Visibility = adjustments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLocalizedStaticText()
    {
        XReportHistoryButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Button", "X Report List");
        RegisterReportButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_RegisterReport_Button", "Register Report");

        XReportHistoryDescriptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_XReportHistory_Description",
            "Browse the X report PDFs already saved on this device. Select any report to open it.");
        XReportHistoryEmptyText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_XReportHistory_Empty",
            "No saved X reports were found.");
        RegisterSessionHistoryDescriptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_HistoryDialog_Description",
            "Browse the register sessions already saved in the database. Select any session to open its report details.");
        RegisterSessionHistoryEmptyText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_NoSessions",
            "No register sessions were found.");
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            },
            CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close"),
            CloseButtonStyle = (Style)Application.Current.Resources["FlatCloseButtonStyle"]
        };

        await dialog.ShowAsync();
    }

    private static XReportHistoryRow BuildXReportHistoryRow(XReportHelper.XReportFileEntry report)
    {
        var title = report.BusinessDate.HasValue
            ? string.Format(
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_TitleDateFormat", "X Report • {0}"),
                report.BusinessDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture))
            : string.Format(
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_TitleFallbackFormat", "X Report • {0}"),
                Path.GetFileNameWithoutExtension(report.PdfPath));

        var subtitle = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_XReportHistory_SubtitleFormat", "Updated {0}"),
            report.LastModifiedLocal.ToString("MMM d, yyyy hh:mm tt", CultureInfo.CurrentCulture));

        var meta = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_XReportHistory_MetaFormat", "File {0}"),
            Path.GetFileName(report.PdfPath));

        return new XReportHistoryRow
        {
            PdfPath = report.PdfPath,
            TitleText = title,
            SubtitleText = subtitle,
            MetaText = meta
        };
    }

    private static RegisterSessionHistoryRow BuildRegisterSessionHistoryRow(RegisterSessionHistoryEntry session)
    {
        var openedAt = ParseStoredUtc(session.OpenedAt);
        var closedAt = ParseStoredUtc(session.ClosedAt);
        var isClosed = closedAt.HasValue;
        var openedBy = ResolveUserName(session.OpenedByUserId);
        var closedBy = session.ClosedByUserId.HasValue && session.ClosedByUserId.Value > 0
            ? ResolveUserName(session.ClosedByUserId.Value)
            : null;

        var title = isClosed
            ? string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryTitleClosedFormat", "{0} • {1} - {2}"),
                openedAt?.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) ?? LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UnknownTime", "Unknown time"),
                openedAt?.ToString("hh:mm tt", CultureInfo.CurrentCulture) ?? "--",
                closedAt.Value.ToString("hh:mm tt", CultureInfo.CurrentCulture))
            : string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryTitleOpenFormat", "{0} • Started {1}"),
                openedAt?.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) ?? LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UnknownTime", "Unknown time"),
                openedAt?.ToString("hh:mm tt", CultureInfo.CurrentCulture) ?? "--");

        var subtitle = !isClosed
            ? string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistorySubtitleOpenFormat", "Opened by {0}"),
                openedBy)
            : closedBy is not null
                ? string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistorySubtitleClosedFormat", "Opened by {0} • Closed by {1}"),
                    openedBy,
                    closedBy)
                : string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistorySubtitleClosedNoUserFormat", "Opened by {0} • Closed"),
                    openedBy);

        var meta = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryMetaFormat", "Session #{0} • {1} sale(s) • Opening {2}"),
            session.SessionId,
            CurrencyDisplayHelper.FormatInt(session.SaleCount),
            FormatCurrency(session.OpeningAmountCents));

        var stateText = isClosed
            ? session.ClosingAmountCents.HasValue
                ? string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryStateClosedFormat", "Closed • {0}"),
                    FormatCurrency(session.ClosingAmountCents.Value))
                : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryStateClosed", "Closed")
            : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryStateActive", "Active");

        return new RegisterSessionHistoryRow
        {
            SessionId = session.SessionId,
            TitleText = title,
            SubtitleText = subtitle,
            MetaText = meta,
            GrossSalesText = FormatCurrency(session.GrossSalesCents),
            StateText = stateText
        };
    }

    private static string ResolveCashierSummaryName(RegisterSessionCashierSummary summary)
    {
        if (summary.CashierUserId.HasValue && summary.CashierUserId.Value > 0)
        {
            return ResolveUserName(summary.CashierUserId.Value);
        }

        return string.IsNullOrWhiteSpace(summary.CashierName)
            ? LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UnassignedCashier", "Unassigned cashier")
            : summary.CashierName.Trim();
    }

    private static string ResolveUserName(long? userId)
    {
        if (!userId.HasValue || userId.Value <= 0)
        {
            return LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UnassignedCashier", "Unassigned cashier");
        }

        var user = LoginRuntime.Users.GetById(userId.Value);
        if (user is not null && !string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        return string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UserFallbackFormat", "User #{0}"),
            userId.Value);
    }

    private static string BuildIntervalTimeRange(string startedAt, string? endedAt)
    {
        var started = ParseStoredUtc(startedAt);
        var ended = ParseStoredUtc(endedAt);

        return ended.HasValue
            ? string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_TimeRangeClosedFormat", "{0} → {1}"),
                FormatLocalDateTime(started),
                FormatLocalDateTime(ended))
            : string.Format(
                LocalizationHelper.GetString("ReportsReceipts_RegisterReport_TimeRangeOpenFormat", "{0} → Active"),
                FormatLocalDateTime(started));
    }

    private static DateTime? ParseStoredUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc).ToLocalTime() : parsed.ToLocalTime();
        }

        return null;
    }

    private static string FormatLocalDateTime(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("MMM d, yyyy hh:mm tt", CultureInfo.CurrentCulture)
            : LocalizationHelper.GetString("ReportsReceipts_RegisterReport_UnknownTime", "Unknown time");
    }

    private static string FormatCurrency(long amountCents)
    {
        return CurrencyDisplayHelper.FormatConfiguredAmount(amountCents / 100m);
    }

    private static string NormalizeRegisterNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return string.Empty;
        }

        return ExpectedCashCentsRegex.Replace(
            note.Trim(),
            match => $"{match.Groups["prefix"].Value}{FormatCurrency(long.Parse(match.Groups["cents"].Value, CultureInfo.InvariantCulture))}");
    }

    private static string FormatSignedCurrency(long amountCents)
    {
        var absolute = FormatCurrency(Math.Abs(amountCents));
        if (amountCents > 0)
        {
            return $"+{absolute}";
        }

        if (amountCents < 0)
        {
            return $"-{absolute}";
        }

        return absolute;
    }

    private sealed class RegisterReportCashierSummaryRow
    {
        public string CashierName { get; init; } = string.Empty;
        public string SaleCountText { get; init; } = string.Empty;
        public string GrossSalesText { get; init; } = string.Empty;
        public string BreakdownText { get; init; } = string.Empty;
    }

    private sealed class RegisterSessionHistoryRow
    {
        public long SessionId { get; init; }
        public string TitleText { get; init; } = string.Empty;
        public string SubtitleText { get; init; } = string.Empty;
        public string MetaText { get; init; } = string.Empty;
        public string GrossSalesText { get; init; } = string.Empty;
        public string StateText { get; init; } = string.Empty;
    }

    private sealed class XReportHistoryRow
    {
        public string PdfPath { get; init; } = string.Empty;
        public string TitleText { get; init; } = string.Empty;
        public string SubtitleText { get; init; } = string.Empty;
        public string MetaText { get; init; } = string.Empty;
    }

    private sealed class RegisterReportCashierIntervalRow
    {
        public string CashierName { get; init; } = string.Empty;
        public string TimeRangeText { get; init; } = string.Empty;
        public string NoteText { get; init; } = string.Empty;
        public Visibility NoteVisibility { get; init; }
    }

    private sealed class RegisterReportAdjustmentRow
    {
        public string Reason { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string AmountText { get; init; } = string.Empty;
    }
}
