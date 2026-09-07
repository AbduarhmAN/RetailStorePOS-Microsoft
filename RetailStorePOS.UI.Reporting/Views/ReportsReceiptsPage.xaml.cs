using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.Data.Modules.Sales;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Services.Printing;
using RetailStorePOS.UI.Reporting.Models;
using RetailStorePOS.UI.Reporting.ViewModels;

namespace RetailStorePOS.UI.Reporting.Views;

public sealed partial class ReportsReceiptsPage : Page
{
    private enum ReportHistoryRangePreset
    {
        Today,
        Last3Days,
        Last7Days,
        WeekToDate,
        Last30Days,
        MonthToDate
    }

    private static readonly Regex ExpectedCashCentsRegex = new(
        @"(?<prefix>\bExpected cash\s+)(?<cents>-?\d+)\s+cents\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly SolidColorBrush SelectedHistoryRangeBrush = new(Colors.LightGray);
    private static readonly SolidColorBrush UnselectedHistoryRangeBrush = new(Colors.Transparent);

    public ReportsViewModel ViewModel { get; } = new();
    private ReportHistoryRangePreset _selectedReportHistoryPreset = ReportHistoryRangePreset.Last3Days;

    public ReportsReceiptsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Sales))
            {
                SalesListView.Items.Clear();
                foreach (var sale in ViewModel.Sales)
                {
                    SalesListView.Items.Add(sale);
                }
            }
        };
        ViewModel.XReportGenerated += ViewModel_XReportGenerated;
        if (LoginRuntime.FeatureAccess is not null)
        {
            LoginRuntime.FeatureAccess.FeatureAccessChanged += (_, _) =>
                DispatcherQueue.TryEnqueue(() =>
                {
                    EnsureAllowedReportHistoryPreset();
                    UpdateReportHistoryUi();
                });
        }

        EnsureAllowedReportHistoryPreset();
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
        EnsureAllowedReportHistoryPreset();
        UpdateReportHistoryUi();
        DispatcherQueue.TryEnqueue(() => ViewModel.RefreshCommand.Execute(null));
    }

    public static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy");
    public static string FormatTime(DateTime date) => date.ToString("hh:mm tt");
    public static string FormatCurrency(decimal amount) => CurrencyDisplayHelper.FormatConfiguredAmount(amount);

    private string? _latestXReportPdfPath;
    private IReadOnlyList<Sale> _latestXReportSales = Array.Empty<Sale>();
    private DateTime _latestXReportFromDate = DateTime.Today;
    private DateTime _latestXReportToDate = DateTime.Today;
    private string _latestXReportPeriodLabel = "Today";

    private async void ViewModel_XReportGenerated(object? sender, string pdfPath)
    {
        try
        {
            _latestXReportPdfPath = pdfPath;
            XReportStatusText.Text = string.Format(LocalizationHelper.GetString("ReportsReceipts_XReport_StatusFormat"), pdfPath);

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
            var sales = _latestXReportSales.Count > 0
                ? _latestXReportSales
                : await Task.Run(() => LoginRuntime.Sales.GetSalesByDateRange(_latestXReportFromDate, _latestXReportToDate).ToList());
            var storeName = LoginRuntime.Settings.GetStoreName() ?? LocalizationHelper.GetString("ReportsReceipts_StoreFallback");
            var preferredPrinterName = LoginRuntime.Settings.GetPreferredPrinterName();

            using var printHelper = new XReportPrintHelper();
            printHelper.PrintXReport(
                sales,
                _latestXReportFromDate,
                _latestXReportToDate,
                _latestXReportPeriodLabel,
                storeName,
                preferredPrinterName);
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
        _latestXReportSales = Array.Empty<Sale>();
    }

    private async void GenerateXReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (start, end) = GetSelectedReportHistoryWindow();
            var presetCode = GetSelectedReportHistoryPresetCode();
            var run = await Task.Run(() => LoginRuntime.XReports.CreateRun(
                start,
                end,
                presetCode,
                LoginRuntime.Auth.CurrentUser?.Id));
            await ShowXReportRunAsync(run, openImmediately: false);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.GenerateXReport");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_XReport_Dialog.Title", "X Report Ready"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_Status_XReportError", "Unable to generate X report. {0}"),
                    ex.Message));
        }
    }

    private async void XReportHistoryButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var (start, end) = GetSelectedReportHistoryWindow();
            var reports = await Task.Run(() => LoginRuntime.XReports.GetRuns(start, end));
            var rows = reports.Select(BuildXReportHistoryRow).ToList();
            if (rows.Count == 0)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                    string.Format(
                        LocalizationHelper.GetString("ReportsReceipts_XReportHistory_FilteredEmptyFormat", "No saved X reports were found for {0}."),
                        GetSelectedReportHistoryRangeLabel()));
                return;
            }

            UpdateHistoryDialogDescriptions();
            XReportHistoryDialog.Title = LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List");
            XReportHistoryDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            XReportHistoryEmptyText.Visibility = Visibility.Collapsed;
            XReportHistoryList.Items.Clear();
            foreach (var item in rows)
            {
                XReportHistoryList.Items.Add(item);
            }
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
            var rows = FilterRegisterSessionHistory(history).Select(BuildRegisterSessionHistoryRow).ToList();
            if (rows.Count == 0)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryDialog_Title", "Register Sessions"),
                    string.Format(
                        LocalizationHelper.GetString("ReportsReceipts_RegisterReport_FilteredEmptyFormat", "No register sessions were found for {0}."),
                        GetSelectedReportHistoryRangeLabel()));
                return;
            }

            UpdateHistoryDialogDescriptions();
            RegisterSessionHistoryDialog.Title = LocalizationHelper.GetString("ReportsReceipts_RegisterReport_HistoryDialog_Title", "Register Sessions");
            RegisterSessionHistoryDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            RegisterSessionHistoryEmptyText.Visibility = Visibility.Collapsed;
            RegisterSessionHistoryList.Items.Clear();
            foreach (var item in rows)
            {
                RegisterSessionHistoryList.Items.Add(item);
            }
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

    private async void RegisterSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (start, end) = GetSelectedReportHistoryWindow();
            var report = await Task.Run(() => LoginRuntime.RegisterSessions.GetPeriodReport(start, end));
            PopulateRegisterPeriodReport(report);
            RegisterPeriodReportDialog.Title = LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_Dialog_Title", "Register Summary");
            RegisterPeriodReportDialog.CloseButtonText = LocalizationHelper.GetString("Generic_Close", "Close");
            RegisterPeriodReportDialog.XamlRoot = XamlRoot;
            await RegisterPeriodReportDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.ShowRegisterSummary");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_Dialog_Title", "Register Summary"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_RegisterReport_ErrorFormat", "Unable to load the register report. {0}"),
                    ex.Message));
        }
    }

    private async void XReportHistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not XReportHistoryRow row)
            {
                return;
            }

            XReportHistoryDialog.Hide();

            var run = await Task.Run(() => LoginRuntime.XReports.GetRun(row.RunId));
            if (run is null)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_OpenFailed", "Unable to open the selected X report PDF."));
                return;
            }

            await ShowXReportRunAsync(run, openImmediately: true);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.OpenXReportRun");
            await ShowMessageAsync(
                LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_ErrorFormat", "Unable to load the X report list. {0}"),
                    ex.Message));
        }
    }

    private async void RegisterSessionHistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not RegisterSessionHistoryRow row)
            {
                return;
            }

            RegisterSessionHistoryDialog.Hide();

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
        RegisterReportCashierSummaryList.Items.Clear();
        foreach (var item in cashierSummaries)
        {
            RegisterReportCashierSummaryList.Items.Add(item);
        }
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
        RegisterReportCashierIntervalList.Items.Clear();
        foreach (var item in cashierIntervals)
        {
            RegisterReportCashierIntervalList.Items.Add(item);
        }
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
        RegisterReportAdjustmentsList.Items.Clear();
        foreach (var item in adjustments)
        {
            RegisterReportAdjustmentsList.Items.Add(item);
        }
        RegisterReportAdjustmentsEmptyText.Visibility = adjustments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PopulateRegisterPeriodReport(RegisterPeriodReport report)
    {
        RegisterPeriodHeadlineText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_HeadlineFormat", "Register Summary • {0}"),
            GetSelectedReportHistoryRangeLabel());
        RegisterPeriodMetaText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_PeriodFormat", "{0} - {1}"),
            report.PeriodStartLocal.ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
            report.PeriodEndLocal.ToString("MMM d, yyyy", CultureInfo.CurrentCulture));

        RegisterPeriodSalesValueText.Text = FormatCurrency(report.GrossSalesCents);
        RegisterPeriodSalesCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_SalesCaptionFormat", "{0} sale(s) • Tax {1}"),
            CurrencyDisplayHelper.FormatInt(report.SaleCount),
            FormatCurrency(report.TaxCents));

        RegisterPeriodSessionsValueText.Text = CurrencyDisplayHelper.FormatInt(report.SessionCount);
        RegisterPeriodSessionsCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_SessionsCaptionFormat", "{0} closed • {1} open"),
            CurrencyDisplayHelper.FormatInt(report.ClosedSessionCount),
            CurrencyDisplayHelper.FormatInt(report.OpenSessionCount));

        RegisterPeriodCashValueText.Text = FormatCurrency(report.CashSalesCents);
        RegisterPeriodCashCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterReport_CashCaptionFormat", "Adjustments {0}"),
            FormatSignedCurrency(report.NetCashAdjustmentCents));

        RegisterPeriodCardValueText.Text = FormatCurrency(report.CardSalesCents);
        RegisterPeriodCardCaptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_CardCaption",
            "Non-cash sales captured in this session");

        RegisterPeriodExpectedCashValueText.Text = FormatCurrency(report.ExpectedCashCents);
        RegisterPeriodExpectedCashCaptionText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterSummary_ExpectedCashCaption",
            "Opening floats + cash sales + net adjustments");

        RegisterPeriodCountedCashValueText.Text = FormatCurrency(report.CountedCashCents);
        RegisterPeriodCountedCashCaptionText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_CountedCashCaptionFormat", "Variance {0}"),
            FormatSignedCurrency(report.CashVarianceCents));

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

        RegisterPeriodCashierSummaryList.Items.Clear();
        foreach (var item in cashierSummaries)
        {
            RegisterPeriodCashierSummaryList.Items.Add(item);
        }
        RegisterPeriodCashierSummaryEmptyText.Visibility = cashierSummaries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLocalizedStaticText()
    {
        ReportHistoryTitleText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_Title",
            "Report History");
        ReportHistorySubtitleText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_Subtitle",
            "Receipts stay unrestricted. These filters apply only to X report history and register sessions.");
        ReportHistoryQuickRangeLabelText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_QuickRangeLabel",
            "History Range");
        ReportHistoryTodayButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_Range_Today", "Today");
        ReportHistoryLast3DaysButtonText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_Range_Last3Days",
            "Last 3 Days");
        ReportHistoryLast7DaysMenuItem.Text = LocalizationHelper.GetString("ReportsReceipts_Range_Last7Days", "Last 7 Days");
        ReportHistoryWeekToDateMenuItem.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_Range_WeekToDate",
            "Week to Date");
        ReportHistoryLast30DaysMenuItem.Text = LocalizationHelper.GetString("ReportsReceipts_Range_Last30Days", "Last 30 Days");
        ReportHistoryMonthToDateMenuItem.Text = LocalizationHelper.GetString(
            "ReportsReceipts_ReportHistory_Range_MonthToDate",
            "Month to Date");
        XReportHistoryButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Button", "X Report List");
        RegisterSummaryButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_RegisterSummary_Button", "Register Summary");
        RegisterReportButtonText.Text = LocalizationHelper.GetString("ReportsReceipts_RegisterSessions_Button", "Register Sessions");

        XReportHistoryDescriptionText.Text = BuildHistoryDialogDescription(
            "ReportsReceipts_XReportHistory_Description",
            "Browse X reports saved in the database. Select any report to export or open it.");
        XReportHistoryEmptyText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_XReportHistory_Empty",
            "No saved X reports were found.");
        RegisterSessionHistoryDescriptionText.Text = BuildHistoryDialogDescription(
            "ReportsReceipts_RegisterReport_HistoryDialog_Description",
            "Browse the register sessions already saved in the database. Select any session to open its report details.");
        RegisterSessionHistoryEmptyText.Text = LocalizationHelper.GetString(
            "ReportsReceipts_RegisterReport_NoSessions",
            "No register sessions were found.");
        UpdateReportHistoryUi();
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

    private bool CanUseExtendedReportHistory =>
        LoginRuntime.FeatureAccess?.CanUse(FeatureAccessService.Features.AdvancedReports) ?? false;

    private void ReportHistoryTodayButton_Click(object sender, RoutedEventArgs e)
    {
        SetReportHistoryPreset(ReportHistoryRangePreset.Today);
    }

    private void ReportHistoryLast3DaysButton_Click(object sender, RoutedEventArgs e)
    {
        SetReportHistoryPreset(ReportHistoryRangePreset.Last3Days);
    }

    private async void ReportHistoryMoreRangesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!CanUseExtendedReportHistory)
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_ReportHistory_LockedTitle", "Extended report history"),
                    LocalizationHelper.GetString(
                        "ReportsReceipts_ReportHistory_LockedMessage",
                        "Free tier can browse report history for Today and Last 3 Days. Upgrade to unlock Last 7 Days, Week to Date, Last 30 Days, and Month to Date."));
                return;
            }

            if (ReportHistoryMoreRangesButton.ContextFlyout is MenuFlyout flyout)
            {
                flyout.ShowAt(ReportHistoryMoreRangesButton);
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "ReportsReceiptsPage.ReportHistoryMoreRangesButton_Click");
        }
    }

    private void ReportHistoryPresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag })
        {
            return;
        }

        var preset = tag switch
        {
            "Last7Days" => ReportHistoryRangePreset.Last7Days,
            "WeekToDate" => ReportHistoryRangePreset.WeekToDate,
            "Last30Days" => ReportHistoryRangePreset.Last30Days,
            "MonthToDate" => ReportHistoryRangePreset.MonthToDate,
            _ => ReportHistoryRangePreset.Last3Days
        };

        SetReportHistoryPreset(preset);
    }

    private void SetReportHistoryPreset(ReportHistoryRangePreset preset)
    {
        _selectedReportHistoryPreset = preset;
        EnsureAllowedReportHistoryPreset();
        UpdateReportHistoryUi();
    }

    private void EnsureAllowedReportHistoryPreset()
    {
        if (!CanUseExtendedReportHistory && IsPremiumReportHistoryPreset(_selectedReportHistoryPreset))
        {
            _selectedReportHistoryPreset = ReportHistoryRangePreset.Last3Days;
        }
    }

    private void UpdateReportHistoryUi()
    {
        var rangeLabel = GetSelectedReportHistoryRangeLabel();
        ReportHistoryRangeText.Text = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_ReportHistory_RangeStatusFormat", "History range: {0}"),
            rangeLabel);
        ReportHistoryAccessText.Text = CanUseExtendedReportHistory
            ? LocalizationHelper.GetString("ReportsReceipts_ReportHistory_AccessPaid", "Paid history unlocked")
            : LocalizationHelper.GetString("ReportsReceipts_ReportHistory_AccessFree", "Free tier: Today and Last 3 Days only");
        ReportHistoryMoreRangesButtonText.Text = IsPremiumReportHistoryPreset(_selectedReportHistoryPreset)
            ? rangeLabel
            : LocalizationHelper.GetString("ReportsReceipts_ReportHistory_MoreRanges", "More Ranges");

        SetHistoryRangeButtonState(ReportHistoryTodayButton, _selectedReportHistoryPreset == ReportHistoryRangePreset.Today);
        SetHistoryRangeButtonState(ReportHistoryLast3DaysButton, _selectedReportHistoryPreset == ReportHistoryRangePreset.Last3Days);
        SetHistoryRangeButtonState(ReportHistoryMoreRangesButton, IsPremiumReportHistoryPreset(_selectedReportHistoryPreset));
        UpdateHistoryDialogDescriptions();
    }

    private static void SetHistoryRangeButtonState(Button button, bool isSelected)
    {
        button.Background = isSelected ? SelectedHistoryRangeBrush : UnselectedHistoryRangeBrush;
    }

    private string BuildHistoryDialogDescription(string resourceKey, string fallback)
    {
        var baseText = LocalizationHelper.GetString(resourceKey, fallback);
        var rangeText = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_ReportHistory_CurrentWindowFormat", "Showing {0}."),
            GetSelectedReportHistoryRangeLabel());
        return string.Join(Environment.NewLine, baseText, rangeText);
    }

    private void UpdateHistoryDialogDescriptions()
    {
        XReportHistoryDescriptionText.Text = BuildHistoryDialogDescription(
            "ReportsReceipts_XReportHistory_Description",
            "Browse X reports saved in the database. Select any report to export or open it.");
        RegisterSessionHistoryDescriptionText.Text = BuildHistoryDialogDescription(
            "ReportsReceipts_RegisterReport_HistoryDialog_Description",
            "Browse the register sessions already saved in the database. Select any session to open its report details.");
    }

    private async Task ShowXReportRunAsync(XReportRun run, bool openImmediately)
    {
        var sales = await Task.Run(() => LoadSalesForXReportRun(run));
        var periodLabel = BuildXReportPeriodLabel(run);
        var storeName = LoginRuntime.Settings.GetStoreName() ?? LocalizationHelper.GetString("ReportsReceipts_StoreFallback");
        var pdfPath = await Task.Run(() => XReportHelper.GenerateXReport(
            sales,
            run.PeriodStartLocal,
            run.PeriodEndLocal,
            periodLabel,
            storeName));

        _latestXReportPdfPath = pdfPath;
        _latestXReportSales = sales;
        _latestXReportFromDate = run.PeriodStartLocal;
        _latestXReportToDate = run.PeriodEndLocal;
        _latestXReportPeriodLabel = periodLabel;

        if (openImmediately)
        {
            if (!XReportHelper.TryOpenReportPdf(pdfPath))
            {
                await ShowMessageAsync(
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_Dialog_Title", "X Report List"),
                    LocalizationHelper.GetString("ReportsReceipts_XReportHistory_OpenFailed", "Unable to open the selected X report PDF."));
            }

            return;
        }

        XReportStatusText.Text = string.Format(LocalizationHelper.GetString("ReportsReceipts_XReport_StatusFormat"), pdfPath);
        XReportDialog.XamlRoot = XamlRoot;
        await XReportDialog.ShowAsync();
    }

    private IReadOnlyList<Sale> LoadSalesForXReportRun(XReportRun run)
    {
        if (run.SaleIds.Count == 0)
        {
            return Array.Empty<Sale>();
        }

        return LoginRuntime.Sales
            .GetSalesByIds(run.SaleIds)
            .OrderBy(sale => sale.CreatedAt)
            .ToList();
    }

    private IReadOnlyList<RegisterSessionHistoryEntry> FilterRegisterSessionHistory(IReadOnlyList<RegisterSessionHistoryEntry> sessions)
    {
        var (start, end) = GetSelectedReportHistoryWindow();
        return sessions
            .Where(session =>
            {
                var anchorDate = ResolveSessionHistoryDate(session);
                return anchorDate.HasValue && anchorDate.Value >= start && anchorDate.Value <= end;
            })
            .ToList();
    }

    private static DateTime? ResolveSessionHistoryDate(RegisterSessionHistoryEntry session)
    {
        return ParseStoredUtc(session.ClosedAt)?.Date
            ?? ParseStoredUtc(session.OpenedAt)?.Date;
    }

    private (DateTime Start, DateTime End) GetSelectedReportHistoryWindow()
    {
        var today = DateTime.Today;
        return _selectedReportHistoryPreset switch
        {
            ReportHistoryRangePreset.Today => (today, today),
            ReportHistoryRangePreset.Last3Days => (today.AddDays(-2), today),
            ReportHistoryRangePreset.Last7Days => (today.AddDays(-6), today),
            ReportHistoryRangePreset.WeekToDate => (GetStartOfWeek(today), today),
            ReportHistoryRangePreset.Last30Days => (today.AddDays(-29), today),
            ReportHistoryRangePreset.MonthToDate => (new DateTime(today.Year, today.Month, 1), today),
            _ => (today.AddDays(-2), today)
        };
    }

    private string GetSelectedReportHistoryRangeLabel()
    {
        return _selectedReportHistoryPreset switch
        {
            ReportHistoryRangePreset.Today => LocalizationHelper.GetString("ReportsReceipts_Range_Today", "Today"),
            ReportHistoryRangePreset.Last3Days => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_Last3Days", "Last 3 Days"),
            ReportHistoryRangePreset.Last7Days => LocalizationHelper.GetString("ReportsReceipts_Range_Last7Days", "Last 7 Days"),
            ReportHistoryRangePreset.WeekToDate => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_WeekToDate", "Week to Date"),
            ReportHistoryRangePreset.Last30Days => LocalizationHelper.GetString("ReportsReceipts_Range_Last30Days", "Last 30 Days"),
            ReportHistoryRangePreset.MonthToDate => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_MonthToDate", "Month to Date"),
            _ => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_Last3Days", "Last 3 Days")
        };
    }

    private string GetSelectedReportHistoryPresetCode()
    {
        return _selectedReportHistoryPreset switch
        {
            ReportHistoryRangePreset.Today => "Today",
            ReportHistoryRangePreset.Last3Days => "Last3Days",
            ReportHistoryRangePreset.Last7Days => "Last7Days",
            ReportHistoryRangePreset.WeekToDate => "WeekToDate",
            ReportHistoryRangePreset.Last30Days => "Last30Days",
            ReportHistoryRangePreset.MonthToDate => "MonthToDate",
            _ => "Custom"
        };
    }

    private static string BuildXReportPeriodLabel(XReportRun run)
    {
        return run.PresetCode switch
        {
            "Today" => LocalizationHelper.GetString("ReportsReceipts_Range_Today", "Today"),
            "Last3Days" => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_Last3Days", "Last 3 Days"),
            "Last7Days" => LocalizationHelper.GetString("ReportsReceipts_Range_Last7Days", "Last 7 Days"),
            "WeekToDate" => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_WeekToDate", "Week to Date"),
            "Last30Days" => LocalizationHelper.GetString("ReportsReceipts_Range_Last30Days", "Last 30 Days"),
            "MonthToDate" => LocalizationHelper.GetString("ReportsReceipts_ReportHistory_Range_MonthToDate", "Month to Date"),
            _ => run.PeriodStartLocal == run.PeriodEndLocal
                ? run.PeriodStartLocal.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
                : string.Format(
                    LocalizationHelper.GetString("ReportsReceipts_Date_RangeFormat", "{0} - {1}"),
                    run.PeriodStartLocal.ToString("MMM d", CultureInfo.CurrentCulture),
                    run.PeriodEndLocal.ToString("MMM d, yyyy", CultureInfo.CurrentCulture))
        };
    }

    private static bool IsPremiumReportHistoryPreset(ReportHistoryRangePreset preset)
    {
        return preset is ReportHistoryRangePreset.Last7Days
            or ReportHistoryRangePreset.WeekToDate
            or ReportHistoryRangePreset.Last30Days
            or ReportHistoryRangePreset.MonthToDate;
    }

    private static DateTime GetStartOfWeek(DateTime value)
    {
        var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var offset = (7 + (value.DayOfWeek - firstDay)) % 7;
        return value.Date.AddDays(-offset);
    }

    private static XReportHistoryRow BuildXReportHistoryRow(XReportRun report)
    {
        var title = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_XReportHistory_TitleDateFormat", "X Report • {0}"),
            BuildXReportPeriodLabel(report));

        var generatedAt = ParseStoredUtc(report.GeneratedAtUtc);

        var subtitle = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_XReportHistory_SubtitleFormat", "Generated {0}"),
            FormatLocalDateTime(generatedAt));

        var meta = string.Format(
            LocalizationHelper.GetString("ReportsReceipts_XReportHistory_MetaFormat", "{0} transaction(s) • Gross {1}"),
            CurrencyDisplayHelper.FormatInt(report.TransactionCount),
            FormatCurrency(report.GrossSalesCents));

        return new XReportHistoryRow
        {
            RunId = report.Id,
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
                closedAt?.ToString("hh:mm tt", CultureInfo.CurrentCulture) ?? "--")
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

}
