namespace RetailStorePOS.Data.Modules.Sales;

public sealed class RegisterSessionHistoryEntry
{
    public long SessionId { get; set; }
    public long OpenedByUserId { get; set; }
    public long? ClosedByUserId { get; set; }
    public string OpenedAt { get; set; } = string.Empty;
    public string? ClosedAt { get; set; }
    public long OpeningAmountCents { get; set; }
    public long? ClosingAmountCents { get; set; }
    public int SaleCount { get; set; }
    public long GrossSalesCents { get; set; }
}

public sealed class RegisterSessionReport
{
    public long SessionId { get; set; }
    public long OpenedByUserId { get; set; }
    public long? ClosedByUserId { get; set; }
    public string OpenedAt { get; set; } = string.Empty;
    public string? ClosedAt { get; set; }
    public long OpeningAmountCents { get; set; }
    public long? ClosingAmountCents { get; set; }
    public string? OpeningNote { get; set; }
    public string? ClosingNote { get; set; }
    public int SaleCount { get; set; }
    public long GrossSalesCents { get; set; }
    public long TaxCents { get; set; }
    public long CashSalesCents { get; set; }
    public long CardSalesCents { get; set; }
    public long NetCashAdjustmentCents { get; set; }
    public long ExpectedCashCents { get; set; }
    public List<RegisterSessionCashierInterval> CashierIntervals { get; set; } = [];
    public List<RegisterSessionCashierSummary> CashierSummaries { get; set; } = [];
    public List<RegisterSessionAdjustmentEntry> CashAdjustments { get; set; } = [];
}

public sealed class RegisterSessionCashierInterval
{
    public long UserId { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string? EndedAt { get; set; }
    public string? Note { get; set; }
}

public sealed class RegisterSessionCashierSummary
{
    public long? CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public int SaleCount { get; set; }
    public long GrossSalesCents { get; set; }
    public long CashSalesCents { get; set; }
    public long CardSalesCents { get; set; }
}

public sealed class RegisterSessionAdjustmentEntry
{
    public long? UserId { get; set; }
    public bool IsCashIn { get; set; }
    public long AmountCents { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
