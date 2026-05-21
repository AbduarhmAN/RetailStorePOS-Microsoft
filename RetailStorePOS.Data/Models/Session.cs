namespace RetailStorePOS.Data.Models;

public sealed class Session
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property (populated by repository)
    public User? User { get; set; }
}
