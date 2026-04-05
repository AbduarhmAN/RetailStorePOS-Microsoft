using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RetailStorePOS.App;

public partial class CheckoutView : UserControl
{
    public CheckoutView()
    {
        InitializeComponent();
        Unloaded += CheckoutView_Unloaded;
    }

    private void CheckoutView_Loaded(object sender, RoutedEventArgs e)
    {
        UnifiedInputBox.Focus();
        UnifiedInputBox.SelectAll();

        if (ViewModel != null)
        {
            ViewModel.PrintRequested -= ViewModel_PrintRequested;
            ViewModel.PrintRequested += ViewModel_PrintRequested;
        }
    }

    private void CheckoutView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PrintRequested -= ViewModel_PrintRequested;
        }
    }

    private void ViewModel_PrintRequested(object? sender, ReceiptSummary receipt)
    {
        // Invoke native print dialog centered on the main window
        // The service logic handles the dialog modality
        var window = Window.GetWindow(this);
        if (window != null)
        {
            AppServices.ReceiptPdf.PrintReceipt(receipt, window);
        }
    }

    private void CheckoutView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            TenderedCashInput.Focus();
            TenderedCashInput.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && !UnifiedInputBox.IsKeyboardFocusWithin)
        {
            UnifiedInputBox.Focus();
            UnifiedInputBox.SelectAll();
        }
    }

    private CheckoutViewModel? ViewModel => DataContext as CheckoutViewModel;

    // Kept for any remaining references
    private void ScanInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;
        if (ViewModel.HandleBarcodeKey(e.Key))
            e.Handled = true;
    }

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;
        if (ViewModel.HandleSearchKey(e.Key))
            e.Handled = true;
    }

    /// <summary>
    /// Unified handler for the combined Scan / Find Products input.
    ///
    /// On Enter  → mirrors the old barcode path (synchronous):
    ///             copies SearchTerm → BarcodeEntry, calls AddByBarcode,
    ///             then clears the field so the list resets.
    /// On ↑↓ / Escape → delegates to HandleSearchKey for list navigation.
    /// </summary>
    private void UnifiedInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Key == Key.Enter)
        {
            // Sync the visible search text into BarcodeEntry so the
            // synchronous AddByBarcode() lookup uses it.
            ViewModel.BarcodeEntry = ViewModel.SearchTerm;
            ViewModel.HandleBarcodeKey(Key.Enter);   // calls AddByBarcode()
            ViewModel.SearchTerm = string.Empty;     // clear unified field
            e.Handled = true;
            return;
        }

        // ↑ ↓ navigate the product list; Escape clears the field
        if (ViewModel.HandleSearchKey(e.Key))
            e.Handled = true;
    }

    /// <summary>
    /// Handles Enter on the product ListBox so the selected item is added
    /// even when focus has moved from the TextBox to the list.
    /// </summary>
    private void ProductList_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Key == Key.Enter)
        {
            // AddSelectedProduct() adds whatever SelectedProduct is set to.
            ViewModel.AddSelectedCommand.Execute(null);
            e.Handled = true;
        }
    }
    private Key? _lastTenderedKey;
    private System.DateTime? _lastTenderedTime;

    /// <summary>
    /// Enter on the Cash Tendered field fires Complete Sale — but ONLY when
    /// CanCompleteSale is true (cart non-empty, cash >= total) AND
    /// the user presses Enter twice consecutively (double-press within 500ms).
    /// The ViewModel's canExecute guard is the authoritative check.
    /// </summary>
    private void CashTendered_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Key == Key.Enter)
        {
            var now = System.DateTime.UtcNow;

            // Check if the previous key was also Enter, AND it was pressed less than 500ms ago
            if (_lastTenderedKey == Key.Enter && _lastTenderedTime.HasValue && (now - _lastTenderedTime.Value).TotalMilliseconds < 500)
            {
                ViewModel.CompleteSaleCommand.Execute(null);
                _lastTenderedKey = null;
                _lastTenderedTime = null;
            }
            else
            {
                _lastTenderedKey = Key.Enter;
                _lastTenderedTime = now;
            }
            e.Handled = true;
        }
        else
        {
            _lastTenderedKey = e.Key;
            _lastTenderedTime = null;
        }
    }
}
