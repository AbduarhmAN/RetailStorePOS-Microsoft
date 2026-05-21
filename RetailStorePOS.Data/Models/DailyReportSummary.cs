namespace RetailStorePOS.Data.Models;

public sealed class DailyReportSummary
{
    public DateTime ReportDate { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalTax { get; set; }
    public int TotalTransactions { get; set; }
}
