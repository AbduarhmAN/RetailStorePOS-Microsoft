using System.Collections.Generic;

namespace RetailStorePOS.Data.Modules.Tax;

public class TaxGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public bool IsAutoManaged { get; set; }
    public List<TaxRule> Rules { get; set; } = new List<TaxRule>();

    public string DisplaySummary => IsActive ? Name : $"{Name} (Inactive)";
    public string RulesSummary => Rules != null && Rules.Count > 0 ? string.Join(", ", System.Linq.Enumerable.Select(Rules, r => r.DisplaySummary)) : "No rules";
}
