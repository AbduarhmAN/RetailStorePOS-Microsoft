namespace RetailStorePOS.WinUiLogin.Models;

public sealed class ReceiptLineItem
{
    private string _currencyCode = "USD";

    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    public decimal Quantity { get; set; }

    public decimal LineTotal => Math.Round(Price * Quantity, 2, MidpointRounding.AwayFromZero);

    public string QuantityText => Quantity.ToString("0.##");

    public string PriceText => RetailStorePOS.WinUiLogin.Common.CurrencyDisplayHelper.FormatAmount(Price, CurrencyCode);

    public string LineTotalText => RetailStorePOS.WinUiLogin.Common.CurrencyDisplayHelper.FormatAmount(LineTotal, CurrencyCode);

    public string CurrencyCode
    {
        get => _currencyCode;
        set
        {
            _currencyCode = string.IsNullOrWhiteSpace(value)
                ? "USD"
                : value.Trim().ToUpperInvariant();
        }
    }
}


