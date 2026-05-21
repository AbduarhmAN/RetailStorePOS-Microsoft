namespace RetailStorePOS.Data.Modules.Sales;

public sealed class RegisterCashAdjustmentSummary
{
    public bool IsCashIn { get; set; }
    public long AmountCents { get; set; }
    public string Reason { get; set; } = string.Empty;
}
