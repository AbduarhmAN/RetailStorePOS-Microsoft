using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.ViewModels;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class TaxConfigurationPage : Page
{
    public SettingsViewModel ViewModel { get; private set; } = null!;

    public TaxConfigurationPage()
    {
        InitializeComponent();
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
            LoginRuntime.TaxAuthorities.Create(result);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = "Tax Authority added.";
        }
    }

    private async void EditAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxAuthority existing)
        {
            var result = await ShowAuthorityEditorDialog(existing);
            if (result != null)
            {
                LoginRuntime.TaxAuthorities.Update(result);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = "Tax Authority updated.";
            }
        }
    }

    private async void DeleteAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxAuthority existing)
        {
            var confirm = new ContentDialog
            {
                Title = "Delete Tax Agency",
                Content = $"Are you sure you want to delete \"{existing.Name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                try {
                    LoginRuntime.TaxAuthorities.Delete(existing.Id);
                    ViewModel.LoadTaxData();
                    ViewModel.StatusMessage = "Tax Authority deleted.";
                } catch {
                    ViewModel.StatusMessage = "Cannot delete: currently in use by a rule.";
                }
            }
        }
    }

    private async Task<TaxAuthority?> ShowAuthorityEditorDialog(TaxAuthority? existing)
    {
        var nameBox = new TextBox { Header = "Authority Name", Text = existing?.Name ?? "", Margin = new Thickness(0,0,0,12) };
        var codeBox = new TextBox { Header = "Authority Code", Text = existing?.AuthorityCode ?? "", Margin = new Thickness(0,0,0,12) };
        var regBox = new TextBox { Header = "Registration # (Optional)", Text = existing?.RegistrationNumber ?? "" };

        var dialog = new ContentDialog
        {
            Title = existing == null ? "Add Tax Agency" : "Edit Tax Agency",
            Content = new StackPanel { Children = { nameBox, codeBox, regBox } },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(codeBox.Text))
            {
                ViewModel.StatusMessage = "Name and Code are required.";
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
            LoginRuntime.TaxRules.Create(result, LoginRuntime.Auth.CurrentUser?.Id);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = "Tax Rule added.";
        }
    }

    private async void EditTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxRule existing)
        {
            var result = await ShowTaxRuleEditorDialog(existing);
            if (result != null)
            {
                LoginRuntime.TaxRules.Update(result, LoginRuntime.Auth.CurrentUser?.Id);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = "Tax Rule updated.";
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
                try {
                    LoginRuntime.TaxRules.Delete(existing.Id);
                    ViewModel.LoadTaxData();
                    ViewModel.StatusMessage = "Tax Rule deleted.";
                } catch {
                    ViewModel.StatusMessage = "Cannot delete: currently in use by a group.";
                }
            }
        }
    }

    private async Task<TaxRule?> ShowTaxRuleEditorDialog(TaxRule? existing)
    {
        var nameBox = new TextBox { Header = "Tax Name", Text = existing?.Name ?? "", Margin = new Thickness(0,0,0,12) };
        
        var typeCombo = new ComboBox { Header = "Calculation Type", HorizontalAlignment=HorizontalAlignment.Stretch, Margin=new Thickness(0,0,0,12) };
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

        var rateBox = new TextBox { Header = "Rate Value", Text = existing?.RateValue.ToString() ?? "0", Margin = new Thickness(0,0,0,12) };
        
        var scopeCombo = new ComboBox { Header = "Scope", HorizontalAlignment=HorizontalAlignment.Stretch, Margin=new Thickness(0,0,0,12) };
        var scopes = new System.Collections.Generic.Dictionary<string, string> {
            { "PRODUCT", "Per Product Item" },
            { "ORDER", "Entire Order (Subtotal)" }
        };
        scopeCombo.ItemsSource = System.Linq.Enumerable.ToList(scopes);
        scopeCombo.DisplayMemberPath = "Value";
        scopeCombo.SelectedValuePath = "Key";
        scopeCombo.SelectedValue = existing?.Scope ?? "PRODUCT";

        var chkInclusive = new CheckBox { Content = "Inclusive (Buried in price)", IsChecked = existing?.IsInclusive ?? false };

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
            return new TaxRule { 
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
            LoginRuntime.TaxGroups.Create(result.Group, result.RuleIds);
            ViewModel.LoadTaxData();
            ViewModel.StatusMessage = "Tax Group added.";
        }
    }

    private async void EditTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxGroup existing)
        {
            var result = await ShowTaxGroupEditorDialog(existing);
            if (result.Group != null)
            {
                LoginRuntime.TaxGroups.Update(result.Group, result.RuleIds);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = "Tax Group updated.";
            }
        }
    }

    private async void DeleteTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaxGroup existing)
        {
            if (existing.IsDefault)
            {
                ViewModel.StatusMessage = "Cannot delete the default tax group.";
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
                LoginRuntime.TaxGroups.Delete(existing.Id);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = "Tax Group deleted.";
            }
        }
    }

    private async Task<(TaxGroup? Group, List<long> RuleIds)> ShowTaxGroupEditorDialog(TaxGroup? existing)
    {
        var assignedRuleIds = new List<long>();
        var nameBox = new TextBox { Header = "Profile Name", Text = existing?.Name ?? "", Margin = new Thickness(0,0,0,12) };
        var defChk = new CheckBox { Content = "Set as Default for new products", IsChecked = existing?.IsDefault ?? false, Margin = new Thickness(0,0,0,12) };
        
        var lb = new ListBox { SelectionMode = SelectionMode.Multiple };
        var allRules = LoginRuntime.TaxRules.GetAll();
        foreach(var rule in allRules)
        {
            var lbi = new ListBoxItem { Content = $"{rule.Name} ({rule.DisplaySummary})", Tag = rule.Id };
            lb.Items.Add(lbi);
            if (existing != null && existing.Rules.Any(r => r.Id == rule.Id))
            {
                lb.SelectedItems.Add(lbi);
            }
        }

        var panel = new StackPanel { Children = { nameBox, defChk, new TextBlock { Text = "Included Tax Rates (Multiple allowed)" }, lb } };

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
            
            foreach(ListBoxItem i in lb.SelectedItems)
            {
                assignedRuleIds.Add((long)i.Tag);
            }

            return (new TaxGroup { Id = existing?.Id ?? 0, Name = nameBox.Text, IsDefault = defChk.IsChecked ?? false, IsActive = existing?.IsActive ?? true }, assignedRuleIds);
        }
        return (null, assignedRuleIds);
    }
}
