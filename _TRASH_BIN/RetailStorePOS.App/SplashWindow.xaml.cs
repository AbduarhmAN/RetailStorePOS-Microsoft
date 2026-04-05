using System.Windows;

namespace RetailStorePOS.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string title, string detail)
    {
        StatusTitleText.Text = title;
        StatusDetailText.Text = detail;
    }
}
