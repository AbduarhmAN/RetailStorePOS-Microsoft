namespace RetailStorePOS.Data.Modules.Reporting;

public sealed class XReportRun
{
    public long Id { get; set; }
    public string PresetCode { get; set; } = string.Empty;
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public long? GeneratedByUserId { get; set; }
    public int TransactionCount { get; set; }
    public long GrossSalesCents { get; set; }
    public long NetSalesCents { get; set; }
    public long TaxCents { get; set; }
    public long CashSalesCents { get; set; }
    public long CardSalesCents { get; set; }
    public List<long> SaleIds { get; } = [];
}
