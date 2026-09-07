using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RetailStorePOS.UI.Sales.ViewModels;

namespace RetailStorePOS.UI.Sales.Views.Components
{
    public sealed partial class CheckoutPaymentControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(CheckoutViewModel), typeof(CheckoutPaymentControl), new PropertyMetadata(null));

        public CheckoutViewModel ViewModel
        {
            get => (CheckoutViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public CheckoutPaymentControl()
        {
            this.InitializeComponent();
        }

        private void TenderedCashBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                box.SelectAll();
            }
        }

        private void TenderedCashBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Handled via ViewModel bindings
        }

        private void TenderedCashBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel?.CompleteSaleCommand?.Execute(null);
            }
        }

        private void TenderedCashBox_CtrlD_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel?.TenderExactCommand?.Execute(null);
            args.Handled = true;
        }
    }
}
