using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class ReportsReceiptsPage : Page
{
    public ReportsViewModel ViewModel { get; } = new();

    public ReportsReceiptsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        ViewModel.XReportGenerated += ViewModel_XReportGenerated;
        ViewModel.Loc.PropertyChanged += (s, e) => Bindings.Update();
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

    private void ReprintButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RetailStorePOS.Data.Modules.Sales.Sale sale)
        {
            ViewModel.ReprintReceiptCommand.Execute(sale);
        }
    }
}
