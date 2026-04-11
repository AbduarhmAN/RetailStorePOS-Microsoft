using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;
using System;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class ReportsReceiptsPage : Page
{
    public ReportsViewModel ViewModel { get; } = new();

    public ReportsReceiptsPage()
    {
        InitializeComponent();
        ViewModel.XReportGenerated += ViewModel_XReportGenerated;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshCommand.Execute(null);
    }

    public static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy");
    public static string FormatTime(DateTime date) => date.ToString("hh:mm tt");
    public static string FormatCurrency(decimal amount) => amount.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);

    private string? _latestXReportPdfPath;

    private async void ViewModel_XReportGenerated(object? sender, string pdfPath)
    {
        _latestXReportPdfPath = pdfPath;
        XReportStatusText.Text = $"PDF saved to: {pdfPath}";

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

    private void XReportDialog_PrintClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(_latestXReportPdfPath) && System.IO.File.Exists(_latestXReportPdfPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _latestXReportPdfPath,
                    UseShellExecute = true,
                    Verb = "print"
                });
            }
            catch (Exception ex)
            {
                LoginRuntime.ReportException(ex, "ReportsReceiptsPage.PrintXReport");
            }
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
}
