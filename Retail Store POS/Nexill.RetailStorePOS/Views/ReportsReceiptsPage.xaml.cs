using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public class ProductVelocityItem
{
    public string ProductName { get; set; } = string.Empty;
    public string VelocityText { get; set; } = string.Empty;
}

public sealed partial class ReportsReceiptsPage : Page
{
    public ReportsViewModel ViewModel { get; } = new();

    public ReportsReceiptsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshCommand.Execute(null);
    }

    public static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy");
    public static string FormatTime(DateTime date) => date.ToString("hh:mm tt");
    public static string FormatCurrency(decimal amount) => amount.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);
}


