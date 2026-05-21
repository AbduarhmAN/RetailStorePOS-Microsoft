namespace RetailStorePOS.Data.Modules.Tax;

public class TaxAuthority
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AuthorityCode { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
