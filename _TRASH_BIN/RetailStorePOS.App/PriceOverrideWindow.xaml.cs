using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RetailStorePOS.App;

public partial class PriceOverrideWindow : Window
{
    private readonly decimal _currentPrice;
    private readonly string _decimalSeparator;
    private string _inputBuffer = string.Empty;

    public decimal? NewPrice { get; private set; }

    public PriceOverrideWindow(decimal currentPrice)
    {
        InitializeComponent();

        _currentPrice = currentPrice;
        _decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        CurrentPriceText.Text = currentPrice.ToString("C2", CultureInfo.CurrentCulture);

        UpdatePreview();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Activate();
            Focus();
            Keyboard.Focus(DialogRoot);
        }), DispatcherPriority.Input);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            if (ConfirmButton.IsEnabled)
            {
                Confirm_Click(sender, new RoutedEventArgs());
            }
            else
            {
                ShowValidation("Please enter a valid non-negative price.");
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Cancel_Click(sender, new RoutedEventArgs());
                return;

            case Key.Back:
                e.Handled = true;
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer = _inputBuffer[..^1];
                    ClearValidation();
                    UpdatePreview();
                }
                return;

            case Key.Delete:
            case Key.Clear:
                e.Handled = true;
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer = string.Empty;
                    ClearValidation();
                    UpdatePreview();
                }
                return;
        }

        var character = GetInputCharacter(e.Key);
        if (character == null)
        {
            return;
        }

        e.Handled = true;
        AppendCharacter(character);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (TryParsePrice(_inputBuffer, out var price))
        {
            NewPrice = price;
            DialogResult = true;
            Close();
            return;
        }

        ShowValidation("Please enter a valid non-negative price.");
        Keyboard.Focus(DialogRoot);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(_inputBuffer))
        {
            PreviewPlaceholderText.Visibility = Visibility.Visible;
            PreviewPriceText.Visibility = Visibility.Collapsed;
            DeltaText.Text = "Start typing a new price. Numeric keypad input is active immediately.";
            DeltaText.Foreground = (Brush)FindResource("PriceDialogMutedBrush");
            ConfirmButton.IsEnabled = false;
            return;
        }

        PreviewPlaceholderText.Visibility = Visibility.Collapsed;
        PreviewPriceText.Visibility = Visibility.Visible;
        PreviewPriceText.Foreground = (Brush)FindResource("TextPrimaryBrush");

        if (TryParsePrice(_inputBuffer, out var price))
        {
            PreviewPriceText.Text = price.ToString("C2", CultureInfo.CurrentCulture);

            var delta = price - _currentPrice;
            if (delta == 0)
            {
                DeltaText.Text = "No change from the current selling price.";
                DeltaText.Foreground = (Brush)FindResource("PriceDialogMutedBrush");
            }
            else if (delta > 0)
            {
                DeltaText.Text = $"Increase of {delta.ToString("C2", CultureInfo.CurrentCulture)} from the current price.";
                DeltaText.Foreground = (Brush)FindResource("PriceDialogAccentBrush");
            }
            else
            {
                DeltaText.Text = $"Discount of {Math.Abs(delta).ToString("C2", CultureInfo.CurrentCulture)} from the current price.";
                DeltaText.Foreground = (Brush)FindResource("PriceDialogSuccessBrush");
            }

            ConfirmButton.IsEnabled = true;
            return;
        }

        PreviewPriceText.Text = _inputBuffer;
        PreviewPriceText.Foreground = (Brush)FindResource("PriceDialogMutedBrush");
        DeltaText.Text = "Enter a valid amount to preview the override.";
        DeltaText.Foreground = (Brush)FindResource("PriceDialogMutedBrush");
        ConfirmButton.IsEnabled = false;
    }

    private void AppendCharacter(string character)
    {
        if (character == _decimalSeparator)
        {
            if (_inputBuffer.Contains(_decimalSeparator, StringComparison.Ordinal))
            {
                return;
            }

            _inputBuffer = string.IsNullOrEmpty(_inputBuffer)
                ? $"0{_decimalSeparator}"
                : _inputBuffer + character;
        }
        else
        {
            _inputBuffer += character;
        }

        ClearValidation();
        UpdatePreview();
    }

    private string? GetInputCharacter(Key key)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return null;
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture);
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture);
        }

        if (key == Key.Decimal || key == Key.OemPeriod || key == Key.OemComma)
        {
            return _decimalSeparator;
        }

        return null;
    }

    private static bool TryParsePrice(string input, out decimal price)
    {
        return decimal.TryParse(
                   input,
                   NumberStyles.Number,
                   CultureInfo.CurrentCulture,
                   out price)
               && price >= 0;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.Visibility = Visibility.Visible;
    }

    private void ClearValidation()
    {
        ValidationText.Text = string.Empty;
        ValidationBorder.Visibility = Visibility.Collapsed;
    }
}
