namespace RetailStorePOS.Data.Models;

public sealed class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? PinHash { get; set; }
    public bool IsAdmin { get; set; }

    // Permissions
    public bool CanCheckout { get; set; } = true;
    public bool CanManageProducts { get; set; }
    public bool CanManageSettings { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanOverridePrice { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string RoleLabel => IsAdmin ? "Administrator" : "Cashier";
}
