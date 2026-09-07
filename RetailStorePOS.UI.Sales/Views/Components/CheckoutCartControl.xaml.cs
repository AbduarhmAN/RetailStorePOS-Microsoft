using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common.Models;
using RetailStorePOS.UI.Sales.Models;
using RetailStorePOS.UI.Sales.ViewModels;

namespace RetailStorePOS.UI.Sales.Views.Components
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

        private void CartItemRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (sender is FrameworkElement { Tag: CheckoutCartItem item } &&
                viewModel is not null &&
                viewModel.RemoveItemCommand.CanExecute(item))
            {
                viewModel.RemoveItemCommand.Execute(item);
            }
        }
    }
}
