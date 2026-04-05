namespace RetailStorePOS.App;

public sealed class ReceiptSummary
{
    public long SaleId { get; set; }
    public long ReceiptNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Tendered { get; set; }
    public decimal Change { get; set; }
    public List<CartItem> Items { get; set; } = new();

    /// <summary>Full path to the saved PDF receipt, or null if generation failed.</summary>
    public string? PdfPath { get; set; }
}
