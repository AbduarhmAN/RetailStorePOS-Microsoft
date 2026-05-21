using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views.Components
{
    public sealed partial class CheckoutCartControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(CheckoutViewModel), typeof(CheckoutCartControl), new PropertyMetadata(null));

        public CheckoutViewModel ViewModel
        {
            get => (CheckoutViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public CheckoutCartControl()
        {
            this.InitializeComponent();
        }
    }
}
