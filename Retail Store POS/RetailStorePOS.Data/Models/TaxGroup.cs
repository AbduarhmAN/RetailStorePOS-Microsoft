namespace RetailStorePOS.Data.Models;

public class TaxGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = string.Empty;

    // Optional collection to hold mapped rules in memory
    public List<TaxRule> Rules { get; set; } = new();

    public string DisplaySummary => Rules.Count == 0 ? "Exempt (0 rules)" : $"{Rules.Count} rule(s) applied";

    public bool IsAutoManaged { get; set; } = false;

    public string RulesSummary => Rules.Count switch {
        0 => "No tax — item is exempt",
        1 => $"{Rules[0].Name} — {Rules[0].DisplaySummary} ({Rules[0].InclusiveLabel}), applied per {Rules[0].ScopeLabel}",
        _ => string.Join(" + ", System.Linq.Enumerable.Select(Rules, r => $"{r.Name} {r.DisplaySummary} ({r.InclusiveLabel})"))
    };
}
