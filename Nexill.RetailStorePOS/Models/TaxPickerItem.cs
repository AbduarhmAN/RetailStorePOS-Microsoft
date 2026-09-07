using Microsoft.UI.Xaml;
using RetailStorePOS.Data.Modules.Tax;

namespace RetailStorePOS.WinUiLogin.Models;

public enum TaxPickerKind { Header, Rule, Profile }

public partial class TaxPickerItem
{
    public TaxPickerKind Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string InfoChipText { get; set; } = string.Empty;

    // Exactly one of these will be set for non-headers:
    public TaxRule? Rule { get; set; }
    public TaxGroup? Profile { get; set; }

    public bool IsHeader => Kind == TaxPickerKind.Header;
    public bool IsSelectable => Kind != TaxPickerKind.Header;

    public Visibility HeaderVis => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectableVis => IsSelectable ? Visibility.Visible : Visibility.Collapsed;
}
