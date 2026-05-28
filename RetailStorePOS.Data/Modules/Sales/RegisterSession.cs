namespace RetailStorePOS.Data.Modules.Sales;

public class RegisterSession
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long? ClosedByUserId { get; set; }
    public long OpeningAmountCents { get; set; }
    public string? OpeningNote { get; set; }
    public string OpenedAt { get; set; } = string.Empty;
    public string? ClosedAt { get; set; }
    public long? ClosingAmountCents { get; set; }
    public string? ClosingNote { get; set; }
}
