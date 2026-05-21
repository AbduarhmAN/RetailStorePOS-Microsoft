namespace RetailStorePOS.Data.Models;

public sealed class DailySummary
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalTax { get; set; }
    public int TransactionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
