using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RetailStorePOS.UI.Sales.ViewModels;

namespace RetailStorePOS.UI.Sales.Views.Components
{
    public sealed partial class CheckoutProductsControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(CheckoutViewModel), typeof(CheckoutProductsControl), new PropertyMetadata(null));

        public CheckoutViewModel ViewModel
        {
            get => (CheckoutViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public CheckoutProductsControl()
        {
            this.InitializeComponent();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.Parent is FrameworkElement parent && parent.DataContext is CheckoutPage page)
            {
                // Proxy to original handler if needed, or handle directly via ViewModel
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
        }

        private void SearchResultsListView_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void SearchResultsListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void SearchResultsListView_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
        }

        private void SearchResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RetailStorePOS.UI.Sales.Models.SearchResultItem item)
            {
                ViewModel?.HandleSearchResultClick(item);
            }
        }

        private void SearchResultCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RetailStorePOS.UI.Sales.Models.SearchResultItem item)
            {
                item.IsPointerOver = true;
            }
        }

        private void SearchResultCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RetailStorePOS.UI.Sales.Models.SearchResultItem item)
            {
                item.IsPointerOver = false;
            }
        }
    }
}
