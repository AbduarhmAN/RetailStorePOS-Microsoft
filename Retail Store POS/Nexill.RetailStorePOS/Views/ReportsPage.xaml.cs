using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class ReportsPage : Page
{
    public ReportsPage()
    {
        this.InitializeComponent();
        this.Loaded += (_, _) => NavigateTo("Dashboard");
    }

    private void DashboardTab_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => NavigateTo("Dashboard");

    private void ReceiptsTab_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => NavigateTo("Receipts");

    private void NavigateTo(string tab)
    {
        // ── Highlight the active tab ────────────────────────────
        bool isDash = tab == "Dashboard";

        // Dashboard button styling
        var dashIcon  = ((StackPanel)DashboardTabBtn.Content).Children[0] as SymbolIcon;
        var dashText  = ((StackPanel)DashboardTabBtn.Content).Children[1] as TextBlock;
        var recIcon   = ((StackPanel)ReceiptsTabBtn.Content).Children[0]  as SymbolIcon;
        var recText   = ((StackPanel)ReceiptsTabBtn.Content).Children[1]  as TextBlock;

        var activeColor  = new SolidColorBrush(Color.FromArgb(255, 25, 118, 210));   // #1976D2
        var inactiveColor = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163)); // #8F96A3

        if (dashIcon  != null) dashIcon.Foreground   = isDash ? activeColor : inactiveColor;
        if (dashText  != null) { dashText.Foreground = isDash ? activeColor : inactiveColor;
                                  dashText.FontWeight = isDash ? Microsoft.UI.Text.FontWeights.SemiBold
                                                                : Microsoft.UI.Text.FontWeights.Normal; }
        if (recIcon   != null) recIcon.Foreground    = isDash ? inactiveColor : activeColor;
        if (recText   != null) { recText.Foreground  = isDash ? inactiveColor : activeColor;
                                  recText.FontWeight  = isDash ? Microsoft.UI.Text.FontWeights.Normal
                                                                : Microsoft.UI.Text.FontWeights.SemiBold; }

        DashboardIndicator.Visibility = isDash
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

        // ── Navigate the content frame ─────────────────────────
        if (isDash)
            ReportsContentFrame.Navigate(typeof(ReportsDashboardPage));
        else
            ReportsContentFrame.Navigate(typeof(ReportsReceiptsPage));
    }
}
