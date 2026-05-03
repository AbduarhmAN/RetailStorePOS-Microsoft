namespace RetailStorePOS.Data.Modules.Sales;

/// <summary>
/// Read model representing the summary of an active register session for review before closing.
/// </summary>
public class RegisterCloseSummary
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public string OpenedAt { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public long SessionTotalCents { get; set; }
    public long OpeningAmountCents { get; set; }
    public long NetCashAdjustmentCents { get; set; }
    public long ExpectedCashCents { get; set; }
    public long ExpectedCardCents { get; set; }
    public List<RegisterCashAdjustmentSummary> CashAdjustments { get; set; } = [];

    // Transient fields for the UI
    public long CountedCashCents { get; set; }
    public long DifferenceCents => CountedCashCents - ExpectedCashCents;
    public string? ClosingNote { get; set; }
}
