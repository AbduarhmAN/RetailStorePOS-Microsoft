using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Models;

using RetailStorePOS.WinUiLogin.ViewModels;
using Windows.System;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class CheckoutPage : Page
{
    private static readonly TimeSpan TenderedDoubleEnterWindow = TimeSpan.FromMilliseconds(600);

    private readonly Flyout _priceOverrideFlyout = new();
    private readonly NumberBox _priceOverrideBox = new();
    private readonly TextBlock _priceOverrideCurrentText = new();
    private readonly TextBlock _priceOverrideOriginalText = new();
    private readonly TextBlock _priceOverrideDeltaText = new();
    private readonly Button _priceOverrideApplyButton = new();
    private CheckoutCartItem? _priceOverrideTargetItem;
    private bool _isShowingDiscountDialog;
    private bool _isShowingClearCartDialog;
    private DateTimeOffset _lastTenderedEnterAt = DateTimeOffset.MinValue;

    public CheckoutPage()
    {
        InitializeComponent();

        ViewModel = new CheckoutViewModel(
            LoginRuntime.Sales,
            LoginRuntime.Settings,
            LoginRuntime.ProductSearch,
            LoginRuntime.Products,
            LoginRuntime.Audit,
            LoginRuntime.LocalPreferences,
            LoginRuntime.Auth,
            DispatcherQueue
        );

        Unloaded += CheckoutPage_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.CartItems.CollectionChanged += CartItems_CollectionChanged;
        InitializePriceOverrideFlyout();
    }

    public CheckoutViewModel ViewModel { get; }

    private void FocusTenderedCashBox()
    {
        TenderedCashBox.Focus(FocusState.Programmatic);
        TenderedCashBox.SelectAll();
    }

    private void TenderedCashBox_CtrlD_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            FocusTenderedCashBox();
            args.Handled = true;
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.TenderedCashBox_CtrlD");
        }
    }

    private void TenderedCashBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            _lastTenderedEnterAt = DateTimeOffset.MinValue;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var isDoubleEnter = now - _lastTenderedEnterAt <= TenderedDoubleEnterWindow;
        _lastTenderedEnterAt = now;
        e.Handled = true;

        if (!isDoubleEnter)
        {
            return;
        }

        if (ViewModel.CompleteSaleCommand.CanExecute(null))
        {
            ViewModel.CompleteSaleCommand.Execute(null);
        }

        _lastTenderedEnterAt = DateTimeOffset.MinValue;
    }

    private void TenderedCashBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var currentText = TenderedCashBox.Text ?? string.Empty;
            if (ViewModel.TenderedInputText != currentText)
            {
                ViewModel.TenderedInputText = currentText;
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.TenderedCashBox_TextChanged");
        }
    }

    private void TenderedCashBox_GotFocus(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentText = TenderedCashBox.Text ?? string.Empty;
            if (!NumericInputNormalization.TryParseDecimal(currentText, out var currentValue) || currentValue != 0m)
            {
                return;
            }

            TenderedCashBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.TenderedCashBox_GotFocus");
        }
    }

    private void CheckoutPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.CartItems.CollectionChanged -= CartItems_CollectionChanged;
        ViewModel.Dispose();
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HandleSearchNavigationKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            CommitSearchSelection();
            e.Handled = true;
        }
    }

    private void SearchResultsListView_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HandleSearchNavigationKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        CommitSearchSelection();
        e.Handled = true;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var currentText = SearchTextBox.Text ?? string.Empty;
            if (ViewModel.SearchTerm != currentText)
            {
                ViewModel.SearchTerm = currentText;
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.SearchTextBox_TextChanged");
        }
    }

    private void CommitSearchSelection()
    {
        if (!ViewModel.TryAddSelectedProduct())
        {
            ViewModel.TrySubmitSearch();
        }
    }

    private void SearchResultsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not SearchResultItem resultItem)
            {
                return;
            }

            ViewModel.HandleSearchResultClick(resultItem);
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.SearchResultsListView_ItemClick");
        }
    }

    private void SearchResultCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SearchResultItem resultItem)
        {
            resultItem.IsPointerOver = true;
        }
    }

    private void SearchResultCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SearchResultItem resultItem)
        {
            resultItem.IsPointerOver = false;
        }
    }

    private bool HandleSearchNavigationKey(VirtualKey key)
    {
        if (key is not (VirtualKey.Down or VirtualKey.Up))
        {
            return false;
        }

        var direction = key == VirtualKey.Down ? 1 : -1;
        var nextResult = ViewModel.MoveSearchSelection(direction);
        if (nextResult is not null)
        {
            SearchResultsListView.ScrollIntoView(nextResult);
        }

        return true;
    }

    private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TryAddSelectedProduct();
    }

    private async void ClearCartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.CanClearCart || _isShowingClearCartDialog || XamlRoot is null)
            {
                return;
            }

            _isShowingClearCartDialog = true;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Clear current sale?",
                Content = "This will remove all items from the current sale.",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.ClearCartCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.ClearCartButton_Click");
        }
        finally
        {
            _isShowingClearCartDialog = false;
        }
    }

    private void PriceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement element || element.DataContext is not CheckoutCartItem item)
            {
                return;
            }

            if (!ViewModel.CanOverridePrice || item.ProductId <= 0)
            {
                return;
            }

            PreparePriceOverrideFlyout(item);
            _priceOverrideFlyout.ShowAt(element);
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.PriceButton_Click");
        }
    }

    private async void PrintReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var receipt = ViewModel.Receipt;
            if (receipt is null || receipt.Items.Count == 0)
            {
                return;
            }

            // Try native Windows print dialog first
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
                var rootGrid = (Grid)this.Content;
                var printContainer = new Canvas { Opacity = 0, IsHitTestVisible = false };
                rootGrid.Children.Add(printContainer);

                var printHelper = new ReceiptPrintHelper();
                await printHelper.PrintReceiptAsync(
                    hwnd,
                    printContainer,
                    receipt,
                    ViewModel.StoreName,
                    ViewModel.StoreAddress,
                    ViewModel.CurrencyCode);

                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Native print not available — fall through to PDF approach
            }

            // Fallback: open PDF with shell print verb
            var pdfPath = receipt.PdfPath;
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                ViewModel.OpenReceiptPdfCommand.Execute(null);
                pdfPath = receipt.PdfPath;
            }

            if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        Verb = "print",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.PrintReceiptButton_Click");
        }
    }

    private void CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (e.Action is not NotifyCollectionChangedAction.Add and not NotifyCollectionChangedAction.Move)
            {
                return;
            }

            // Keep the cart stable: do not auto-scroll on add or move.
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.CartItems_CollectionChanged");
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CheckoutViewModel.ReceiptVisibility)
            && ViewModel.ReceiptVisibility == Visibility.Collapsed)
        {
            DispatcherQueue.TryEnqueue(FocusTenderedCashBox);
        }

        if (e.PropertyName == nameof(CheckoutViewModel.IsDiscountDialogOpen)
            && ViewModel.IsDiscountDialogOpen)
        {
            DispatcherQueue.TryEnqueue(() => _ = ShowDiscountDialogAsync());
        }
    }

    private void InitializePriceOverrideFlyout()
    {
        _priceOverrideBox.Minimum = 0;
        _priceOverrideBox.SmallChange = 1;
        _priceOverrideBox.AcceptsExpression = false;
        _priceOverrideBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        _priceOverrideBox.MinWidth = 240;
        NumericInputNormalization.SetNormalizeOnLostFocus(_priceOverrideBox, true);
        _priceOverrideBox.ValueChanged += PriceOverrideBox_ValueChanged;
        _priceOverrideBox.KeyDown += PriceOverrideBox_KeyDown;

        var titleText = new TextBlock
        {
            Text = "Override price",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        };

        var helperText = new TextBlock
        {
            Text = "Set the new selling price for this cart line.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        _priceOverrideCurrentText.FontSize = 20;
        _priceOverrideCurrentText.FontWeight = FontWeights.SemiBold;

        _priceOverrideOriginalText.FontSize = 12;
        _priceOverrideOriginalText.Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

        _priceOverrideDeltaText.FontSize = 12;
        _priceOverrideDeltaText.TextWrapping = TextWrapping.Wrap;
        _priceOverrideDeltaText.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];

        _priceOverrideApplyButton.Content = "Apply";
        _priceOverrideApplyButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        _priceOverrideApplyButton.IsEnabled = false;
        _priceOverrideApplyButton.Click += PriceOverrideApplyButton_Click;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 6, 12, 6)
        };
        cancelButton.Click += (_, _) => _priceOverrideFlyout.Hide();

        var currentBlock = new StackPanel
        {
            Spacing = 4
        };
        currentBlock.Children.Add(new TextBlock
        {
            Text = "Current price",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        currentBlock.Children.Add(_priceOverrideCurrentText);
        currentBlock.Children.Add(_priceOverrideOriginalText);

        var boxBlock = new StackPanel
        {
            Spacing = 6
        };
        boxBlock.Children.Add(new TextBlock
        {
            Text = "New price",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        boxBlock.Children.Add(_priceOverrideBox);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(_priceOverrideApplyButton);

        var root = new StackPanel
        {
            Width = 320,
            Spacing = 12,
            Padding = new Thickness(6)
        };
        root.Children.Add(titleText);
        root.Children.Add(helperText);
        root.Children.Add(currentBlock);
        root.Children.Add(boxBlock);
        root.Children.Add(_priceOverrideDeltaText);
        root.Children.Add(buttonRow);

        _priceOverrideFlyout.Content = root;
        _priceOverrideFlyout.Opened += PriceOverrideFlyout_Opened;
        _priceOverrideFlyout.Closed += PriceOverrideFlyout_Closed;
    }

    private void PreparePriceOverrideFlyout(CheckoutCartItem item)
    {
        _priceOverrideTargetItem = item;
        _priceOverrideCurrentText.Text = item.PriceText;
        _priceOverrideOriginalText.Text = $"Original: {ViewModel.FormatMoneyText(item.OriginalPrice)}";
        _priceOverrideOriginalText.Visibility = item.IsPriceOverridden ? Visibility.Visible : Visibility.Collapsed;
        _priceOverrideBox.Value = (double)item.Price;
        _priceOverrideBox.Text = item.Price.ToString(CultureInfo.InvariantCulture);
        UpdatePriceOverrideFlyoutState();
    }

    private void PriceOverrideBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            UpdatePriceOverrideFlyoutState();
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.PriceOverrideBox_ValueChanged");
        }
    }

    private void PriceOverrideBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (_priceOverrideApplyButton.IsEnabled)
                {
                    PriceOverrideApplyButton_Click(sender, new RoutedEventArgs());
                }
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                _priceOverrideFlyout.Hide();
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.PriceOverrideBox_KeyDown");
        }
    }

    private void UpdatePriceOverrideFlyoutState()
    {
        try
        {
            if (_priceOverrideTargetItem is null)
            {
                _priceOverrideDeltaText.Text = string.Empty;
                _priceOverrideApplyButton.IsEnabled = false;
                return;
            }

            var currentPrice = _priceOverrideTargetItem.Price;
            var nextPrice = Math.Round((decimal)_priceOverrideBox.Value, 2, MidpointRounding.AwayFromZero);

            if (nextPrice == currentPrice)
            {
                _priceOverrideDeltaText.Text = "Enter a different amount to change the price.";
                _priceOverrideApplyButton.IsEnabled = false;
                return;
            }

            if (nextPrice > currentPrice)
            {
                _priceOverrideDeltaText.Text = $"Increase by {ViewModel.FormatMoneyText(nextPrice - currentPrice)}.";
            }
            else
            {
                _priceOverrideDeltaText.Text = $"Discount by {ViewModel.FormatMoneyText(currentPrice - nextPrice)}.";
            }

            _priceOverrideApplyButton.IsEnabled = nextPrice >= 0m;
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.UpdatePriceOverrideFlyoutState");
        }
    }

    private void PriceOverrideApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_priceOverrideTargetItem is null)
            {
                return;
            }

            var newPrice = Math.Round((decimal)_priceOverrideBox.Value, 2, MidpointRounding.AwayFromZero);
            if (ViewModel.TryOverridePrice(_priceOverrideTargetItem, newPrice))
            {
                _priceOverrideFlyout.Hide();
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.PriceOverrideApplyButton_Click");
        }
    }

    private void PriceOverrideFlyout_Opened(object? sender, object e)
    {
        _priceOverrideBox.Focus(FocusState.Programmatic);
    }

    private void PriceOverrideFlyout_Closed(object? sender, object e)
    {
        _priceOverrideTargetItem = null;
    }

    private async Task ShowDiscountDialogAsync()
    {
        if (_isShowingDiscountDialog || XamlRoot is null)
        {
            return;
        }

        _isShowingDiscountDialog = true;

        try
        {
            DiscountDialog.XamlRoot = XamlRoot;
            await DiscountDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.ShowDiscountDialogAsync");
        }
        finally
        {
            _isShowingDiscountDialog = false;
            ViewModel.IsDiscountDialogOpen = false;
        }
    }
    private void DiscountDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            ViewModel.ApplyDiscount(sender, args);
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.DiscountDialog_PrimaryButtonClick");
        }
    }

    private void DiscountPresetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string preset)
            {
                return;
            }

            switch (preset)
            {
                case "pct:5":
                    ViewModel.IsDiscountPercentage = true;
                    ViewModel.DiscountAmount = 5;
                    break;
                case "pct:10":
                    ViewModel.IsDiscountPercentage = true;
                    ViewModel.DiscountAmount = 10;
                    break;
                case "amt:1":
                    ViewModel.IsDiscountPercentage = false;
                    ViewModel.DiscountAmount = 1;
                    break;
                case "amt:5":
                    ViewModel.IsDiscountPercentage = false;
                    ViewModel.DiscountAmount = 5;
                    break;
            }
        }
        catch (Exception ex)
        {
            ReportCheckoutException(ex, "CheckoutPage.DiscountPresetButton_Click");
        }
    }

    private static void ReportCheckoutException(Exception ex, string operationName)
    {
        LoginRuntime.ReportException(ex, operationName);
    }

}


