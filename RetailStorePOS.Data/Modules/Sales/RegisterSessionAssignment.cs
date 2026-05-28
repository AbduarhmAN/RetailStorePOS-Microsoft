namespace RetailStorePOS.Data.Modules.Sales;

public class RegisterSessionAssignment
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string? EndedAt { get; set; }
    public string? Note { get; set; }
}
