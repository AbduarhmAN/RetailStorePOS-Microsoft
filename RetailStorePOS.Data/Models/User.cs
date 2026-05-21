using System.Text.Json.Serialization;

namespace RetailStorePOS.Data.Models;

public sealed class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    [JsonIgnore]
    public string? PasswordHash { get; set; }

    [JsonIgnore]
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
    public int FailedLoginCount { get; set; }
    public DateTime? LastFailedLoginAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string RoleLabel => IsAdmin ? "Administrator" : "Cashier";
}
