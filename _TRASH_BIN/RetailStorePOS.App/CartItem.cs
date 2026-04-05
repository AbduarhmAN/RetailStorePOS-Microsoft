namespace RetailStorePOS.App;

public sealed class CartItem : ViewModelBase
{
    private decimal _quantity = 1m;
    private decimal _price;
    private bool _isPriceOverridden;

    public long ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal TaxRatePercent { get; set; }

    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(TaxAmount));
            }
        }
    }

    public bool IsPriceOverridden
    {
        get => _isPriceOverridden;
        set => SetProperty(ref _isPriceOverridden, value);
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(TaxAmount));
            }
        }
    }

    public decimal LineTotal => Math.Round(Price * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal TaxAmount => Math.Round(LineTotal * TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
}
