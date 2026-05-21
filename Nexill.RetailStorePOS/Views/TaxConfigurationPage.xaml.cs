using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.Data.Modules.Tax;
using RetailStorePOS.WinUiLogin.ViewModels;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class TaxConfigurationPage : Page
{
    private static TaxAuthorityRepository TaxAuthoritiesModule => LoginRuntime.TaxAuthorities;
    private static TaxRuleRepository TaxRulesModule => LoginRuntime.TaxRules;
    private static TaxGroupRepository TaxGroupsModule => LoginRuntime.TaxGroups;

    public static TaxConfigurationPage Current { get; private set; } = null!;
    public SettingsViewModel ViewModel { get; private set; } = null!;

    public TaxConfigurationPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Current = this;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }

    public static Visibility BoolToVis(bool val) => val ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InverseBoolToVis(bool val) => val ? Visibility.Collapsed : Visibility.Visible;
    public static bool InverseBool(bool val) => !val;

    private static string NormalizeDigits(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            var d = char.GetNumericValue(c);
            if (d >= 0 && d <= 9)
            {
                sb.Append((char)('0' + (int)d));
            }
            else if (c == '.' || c == ',')
            {
                sb.Append(c);
            }
            else if (c == '\u066B')
            {
                sb.Append('.');
            }
        }
        return sb.ToString();
    }

    // ─── Tax Authorities ─────────────────────────────────────────────

    private async void AddAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowAuthorityEditorDialog(null);
        if (result != null)
        {
            TaxAuthoritiesModule.Create(result);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_AuthorityAdded");
        }
    }

    private async void EditAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxAuthority existing)
        {
            var result = await ShowAuthorityEditorDialog(existing);
            if (result != null)
            {
                TaxAuthoritiesModule.Update(result);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_AuthorityUpdated");
            }
        }
    }

    private async void DeleteAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxAuthority existing)
        {
            var confirm = new ContentDialog
            {
                Title = LocalizationHelper.GetString("Tax_Dialog_DeleteAuthority_Title"),
                Content = LocalizationHelper.Format("Tax_Dialog_DeleteAuthority_Content", existing.Name),
                PrimaryButtonText = LocalizationHelper.GetString("Generic_Delete"),
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    TaxAuthoritiesModule.Delete(existing.Id);
                    ViewModel.LoadTaxData();
                    ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_AuthorityDeleted");
                }
                catch
                {
                    ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_AuthorityInUse");
                }
            }
        }
    }

    private async Task<TaxAuthority?> ShowAuthorityEditorDialog(TaxAuthority? existing)
    {
        var nameBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_AuthorityName_Header"), Text = existing?.Name ?? "", Margin = new Thickness(0, 0, 0, 12) };
        var codeBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_AuthorityCode_Header"), Text = existing?.AuthorityCode ?? "", Margin = new Thickness(0, 0, 0, 12) };
        var regBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_Registration_Header"), Text = existing?.RegistrationNumber ?? "" };

        var dialog = new ContentDialog
        {
            Title = existing == null ? LocalizationHelper.GetString("Tax_Dialog_AddAuthority_Title") : LocalizationHelper.GetString("Tax_Dialog_EditAuthority_Title"),
            Content = new StackPanel { Children = { nameBox, codeBox, regBox } },
            PrimaryButtonText = LocalizationHelper.GetString("Generic_Save"),
            CloseButtonText = LocalizationHelper.GetString("Generic_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(codeBox.Text))
            {
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_NameCodeRequired");
                return null;
            }
            return new TaxAuthority { Id = existing?.Id ?? 0, Name = nameBox.Text, AuthorityCode = codeBox.Text, RegistrationNumber = regBox.Text };
        }
        return null;
    }

    // ─── Tax Rules ───────────────────────────────────────────────────

    private async void AddTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowTaxRuleEditorDialog(null);
        if (result != null)
        {
            TaxRulesModule.Create(result, LoginRuntime.Auth.CurrentUser?.Id);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_RuleAdded");
        }
    }

    private async void EditTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxRule existing)
        {
            var result = await ShowTaxRuleEditorDialog(existing);
            if (result != null)
            {
                TaxRulesModule.Update(result, LoginRuntime.Auth.CurrentUser?.Id);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_RuleUpdated");
            }
        }
    }

    private async void DeleteTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxRule existing)
        {
            var confirm = new ContentDialog
            {
                Title = "Delete Tax Rate",
                Content = $"Are you sure you want to delete \"{existing.Name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    TaxRulesModule.Delete(existing.Id);
                    ViewModel.LoadTaxData();
                    ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_RuleDeleted");
                }
                catch
                {
                    ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_RuleInUse");
                }
            }
        }
    }

    private async Task<TaxRule?> ShowTaxRuleEditorDialog(TaxRule? existing)
    {
        var nameBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_TaxName_Header"), Text = existing?.Name ?? "", Margin = new Thickness(0, 0, 0, 12) };

        var typeCombo = new ComboBox { Header = LocalizationHelper.GetString("Tax_Dialog_CalcType_Header"), HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12) };
        var calcTypes = new System.Collections.Generic.Dictionary<string, string> {
            { "PERCENTAGE", "Standard Percentage (%)" },
            { "FIXED_AMOUNT", "Fixed Amount ($)" },
            { "TIERED", "Tiered Rates" },
            { "PER_UNIT_MEASURE", "Per Unit / Weight" },
            { "PERCENTAGE_ON_MARGIN", "Percentage on Margin" },
            { "REVERSE_CHARGE", "Reverse Charge (B2B)" }
        };
        typeCombo.ItemsSource = System.Linq.Enumerable.ToList(calcTypes);
        typeCombo.DisplayMemberPath = "Value";
        typeCombo.SelectedValuePath = "Key";
        typeCombo.SelectedValue = existing?.CalcType ?? "PERCENTAGE";

        var rateBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_RateValue_Header"), Text = existing?.RateValue.ToString() ?? "0", Margin = new Thickness(0, 0, 0, 12) };

        var scopeCombo = new ComboBox { Header = LocalizationHelper.GetString("Tax_Dialog_Scope_Header"), HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12) };
        var scopes = new System.Collections.Generic.Dictionary<string, string> {
            { "PRODUCT", "Per Product Item" },
            { "ORDER", "Entire Order (Subtotal)" }
        };
        scopeCombo.ItemsSource = System.Linq.Enumerable.ToList(scopes);
        scopeCombo.DisplayMemberPath = "Value";
        scopeCombo.SelectedValuePath = "Key";
        scopeCombo.SelectedValue = existing?.Scope ?? "PRODUCT";

        var chkInclusive = new CheckBox { Content = LocalizationHelper.GetString("Tax_Dialog_Inclusive_Content"), IsChecked = existing?.IsInclusive ?? false };

        var panel = new StackPanel { Children = { nameBox, typeCombo, rateBox, scopeCombo, chkInclusive } };

        var dialog = new ContentDialog
        {
            Title = existing == null ? "Add Tax Rate" : "Edit Tax Rate",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return null;
            decimal.TryParse(rateBox.Text, out decimal r);
            return new TaxRule
            {
                Id = existing?.Id ?? 0,
                Name = nameBox.Text,
                CalcType = typeCombo.SelectedValue?.ToString() ?? "PERCENTAGE",
                RateValue = r,
                Scope = scopeCombo.SelectedValue?.ToString() ?? "PRODUCT",
                IsInclusive = chkInclusive.IsChecked ?? false,
                TaxAuthorityId = existing?.TaxAuthorityId,
                AppliesAfterDiscount = existing?.AppliesAfterDiscount ?? true,
                SequenceOrder = existing?.SequenceOrder ?? 1,
                MinTaxAmountCents = existing?.MinTaxAmountCents,
                MaxTaxAmountCents = existing?.MaxTaxAmountCents,
                ThresholdMinCents = existing?.ThresholdMinCents,
                ThresholdMaxCents = existing?.ThresholdMaxCents,
                ThresholdScope = existing?.ThresholdScope,
                EffectiveFrom = existing?.EffectiveFrom,
                EffectiveUntil = existing?.EffectiveUntil,
                IsActive = existing?.IsActive ?? true,
                IsDefault = existing?.IsDefault ?? false
            };
        }
        return null;
    }

    // ─── Tax Groups ──────────────────────────────────────────────────

    private async void AddTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowTaxGroupEditorDialog(null);
        if (result.Group != null)
        {
            TaxGroupsModule.Create(result.Group, result.RuleIds);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_GroupAdded");
        }
    }

    private async void EditTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxGroup existing)
        {
            var result = await ShowTaxGroupEditorDialog(existing);
            if (result.Group != null)
            {
                TaxGroupsModule.Update(result.Group, result.RuleIds);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_GroupUpdated");
            }
        }
    }

    private async void DeleteTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxGroup existing)
        {
            if (existing.IsDefault)
            {
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_CannotDeleteDefaultGroup");
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Delete Tax Profile",
                Content = $"Are you sure you want to delete \"{existing.Name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                TaxGroupsModule.Delete(existing.Id);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_GroupDeleted");
            }
        }
    }

    private async Task<(TaxGroup? Group, List<long> RuleIds)> ShowTaxGroupEditorDialog(TaxGroup? existing)
    {
        var assignedRuleIds = new List<long>();
        var nameBox = new TextBox { Header = "Profile Name", Text = existing?.Name ?? "", Margin = new Thickness(0, 0, 0, 12) };
        var defChk = new CheckBox { Content = "Set as Default for new products", IsChecked = existing?.IsDefault ?? false, Margin = new Thickness(0, 0, 0, 12) };

        var lb = new ListBox { SelectionMode = SelectionMode.Multiple };
        var allRules = TaxRulesModule.GetAll();
        foreach (var rule in allRules)
        {
            var lbi = new ListBoxItem { Content = $"{rule.Name} ({rule.DisplaySummary})", Tag = rule.Id };
            lb.Items.Add(lbi);
            if (existing != null && existing.Rules.Any(r => r.Id == rule.Id))
            {
                lb.SelectedItems.Add(lbi);
            }
        }

        var panel = new StackPanel { Children = { nameBox, defChk, new TextBlock { Text = LocalizationHelper.GetString("Tax_Dialog_IncludedRules_Header") }, lb } };

        var dialog = new ContentDialog
        {
            Title = existing == null ? "Add Tax Profile" : "Edit Tax Profile",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return (null, assignedRuleIds);

            foreach (ListBoxItem i in lb.SelectedItems)
            {
                assignedRuleIds.Add((long)i.Tag);
            }

            return (new TaxGroup { Id = existing?.Id ?? 0, Name = nameBox.Text, IsDefault = defChk.IsChecked ?? false, IsActive = existing?.IsActive ?? true }, assignedRuleIds);
        }
        return (null, assignedRuleIds);
    }
}
