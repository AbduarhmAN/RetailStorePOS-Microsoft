namespace RetailStorePOS.Data.Modules.Sales;

public class Sale
{
    public long Id { get; set; }
    public long ReceiptNumber { get; set; }
    public long? RegisterSessionId { get; set; }
    public long? CashierUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CashierName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Tendered { get; set; }
    public decimal Change { get; set; }
    public string PaymentType { get; set; } = "cash";
    public List<SaleItem> Items { get; } = new();
}
