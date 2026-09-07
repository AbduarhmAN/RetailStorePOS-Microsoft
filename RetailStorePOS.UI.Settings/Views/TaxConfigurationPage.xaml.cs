using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RetailStorePOS.Data.Modules.Tax;
using RetailStorePOS.UI.Settings.ViewModels;
using RetailStorePOS.UI.Common;
using System.Collections.Specialized;
using System.Collections;
using Microsoft.UI.Dispatching;

namespace RetailStorePOS.UI.Settings.Views;

public sealed partial class TaxConfigurationPage : Page
{
    private static TaxAuthorityRepository TaxAuthoritiesModule => LoginRuntime.TaxAuthorities;
    private static TaxRuleRepository TaxRulesModule => LoginRuntime.TaxRules;
    private static TaxGroupRepository TaxGroupsModule => LoginRuntime.TaxGroups;

    public static TaxConfigurationPage Current { get; private set; } = null!;
    public SettingsViewModel ViewModel { get; private set; } = null!;
    private readonly DispatcherQueue _uiDispatcher;
    private bool _isSyncingOptionCombos;

    public TaxConfigurationPage()
    {
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Current = this;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            if (ViewModel != null)
            {
                DetachPageSubscriptions();
            }
            ViewModel = vm;
            AttachPageSubscriptions();
            Bindings.Update();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        DetachPageSubscriptions();
    }

    private void AttachPageSubscriptions()
    {
        if (ViewModel == null) return;

        ViewModel.TaxAuthorities.CollectionChanged += TaxAuthorities_CollectionChanged;
        ViewModel.TaxRules.CollectionChanged += TaxRules_CollectionChanged;
        ViewModel.TaxGroups.CollectionChanged += TaxGroups_CollectionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        SyncCollectionToItemsControl(ViewModel.RoundingStrategies, RoundingStrategiesComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.CashRoundingUnits, CashRoundingUnitsComboBox.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncTaxOptionSelections();
        SyncCollectionToItemsControl(ViewModel.TaxAuthorities, TaxAuthoritiesListView.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.TaxRules, TaxRulesListView.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        SyncCollectionToItemsControl(ViewModel.TaxGroups, TaxGroupsListView.Items, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void DetachPageSubscriptions()
    {
        if (ViewModel == null) return;

        ViewModel.TaxAuthorities.CollectionChanged -= TaxAuthorities_CollectionChanged;
        ViewModel.TaxRules.CollectionChanged -= TaxRules_CollectionChanged;
        ViewModel.TaxGroups.CollectionChanged -= TaxGroups_CollectionChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.SelectedRoundingStrategy) or nameof(SettingsViewModel.SelectedCashRoundingUnit))
        {
            _uiDispatcher.TryEnqueue(SyncTaxOptionSelections);
        }
    }

    private void TaxAuthorities_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.TaxAuthorities, TaxAuthoritiesListView.Items, e));

    private void TaxRules_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.TaxRules, TaxRulesListView.Items, e));

    private void TaxGroups_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _uiDispatcher.TryEnqueue(() => SyncCollectionToItemsControl(ViewModel.TaxGroups, TaxGroupsListView.Items, e));

    private void SyncCollectionToItemsControl(IEnumerable source, ItemCollection target, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    target.Remove(item);
                }
            }
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (e.NewStartingIndex >= 0 && e.NewStartingIndex < target.Count)
                    {
                        target.Insert(e.NewStartingIndex, item);
                    }
                    else
                    {
                        target.Add(item);
                    }
                }
            }
        }
    }

    private void RoundingStrategiesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingOptionCombos || ViewModel is null)
        {
            return;
        }

        if (RoundingStrategiesComboBox.SelectedItem is ComboBoxOption option)
        {
            ViewModel.SelectedRoundingStrategy = option.Key;
        }
    }

    private void CashRoundingUnitsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingOptionCombos || ViewModel is null)
        {
            return;
        }

        if (CashRoundingUnitsComboBox.SelectedItem is ComboBoxOption option)
        {
            ViewModel.SelectedCashRoundingUnit = option.Key;
        }
    }

    private void SyncTaxOptionSelections()
    {
        if (ViewModel is null)
        {
            return;
        }

        _isSyncingOptionCombos = true;
        try
        {
            RoundingStrategiesComboBox.SelectedItem = FindComboBoxOption(RoundingStrategiesComboBox.Items, ViewModel.SelectedRoundingStrategy);
            CashRoundingUnitsComboBox.SelectedItem = FindComboBoxOption(CashRoundingUnitsComboBox.Items, ViewModel.SelectedCashRoundingUnit);
        }
        finally
        {
            _isSyncingOptionCombos = false;
        }
    }

    private static ComboBoxOption? FindComboBoxOption(ItemCollection items, string key)
    {
        foreach (var item in items)
        {
            if (item is ComboBoxOption option && string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
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

    // â”€â”€â”€ Tax Authorities â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async void AddAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await ShowAuthorityEditorDialog(null);
            if (result != null)
            {
                TaxAuthoritiesModule.Create(result);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_AuthorityAdded");
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.AddAuthorityButton_Click");
        }
    }

    private async void EditAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.EditAuthorityButton_Click");
        }
    }

    private async void DeleteAuthorityButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.DeleteAuthorityButton_Click");
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

    // â”€â”€â”€ Tax Rules â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async void AddTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await ShowTaxRuleEditorDialog(null);
            if (result != null)
            {
                TaxRulesModule.Create(result, LoginRuntime.Auth.CurrentUser?.Id);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_RuleAdded");
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.AddTaxRuleButton_Click");
        }
    }

    private async void EditTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.EditTaxRuleButton_Click");
        }
    }

    private async void DeleteTaxRuleButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.DeleteTaxRuleButton_Click");
        }
    }

    private async Task<TaxRule?> ShowTaxRuleEditorDialog(TaxRule? existing)
    {
        var nameBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_TaxName_Header"), Text = existing?.Name ?? "", Margin = new Thickness(0, 0, 0, 12) };

        var typeCombo = new ComboBox { Header = LocalizationHelper.GetString("Tax_Dialog_CalcType_Header"), HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12) };
        var calcTypes = new System.Collections.Generic.List<ComboBoxOption> {
            new("PERCENTAGE", "Standard Percentage (%)"),
            new("FIXED_AMOUNT", "Fixed Amount ($)"),
            new("TIERED", "Tiered Rates"),
            new("PER_UNIT_MEASURE", "Per Unit / Weight"),
            new("PERCENTAGE_ON_MARGIN", "Percentage on Margin"),
            new("REVERSE_CHARGE", "Reverse Charge (B2B)")
        };
        typeCombo.Items.Clear();
        foreach (var item in calcTypes)
        {
            typeCombo.Items.Add(item);
        }
        typeCombo.SelectedItem = FindComboBoxOption(typeCombo.Items, existing?.CalcType ?? "PERCENTAGE");

        var rateBox = new TextBox { Header = LocalizationHelper.GetString("Tax_Dialog_RateValue_Header"), Text = existing?.RateValue.ToString() ?? "0", Margin = new Thickness(0, 0, 0, 12) };

        var scopeCombo = new ComboBox { Header = LocalizationHelper.GetString("Tax_Dialog_Scope_Header"), HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12) };
        var scopes = new System.Collections.Generic.List<ComboBoxOption> {
            new("PRODUCT", "Per Product Item"),
            new("ORDER", "Entire Order (Subtotal)")
        };
        scopeCombo.Items.Clear();
        foreach (var item in scopes)
        {
            scopeCombo.Items.Add(item);
        }
        scopeCombo.SelectedItem = FindComboBoxOption(scopeCombo.Items, existing?.Scope ?? "PRODUCT");

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
                CalcType = (typeCombo.SelectedItem as ComboBoxOption)?.Key ?? "PERCENTAGE",
                RateValue = r,
                Scope = (scopeCombo.SelectedItem as ComboBoxOption)?.Key ?? "PRODUCT",
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

    // â”€â”€â”€ Tax Groups â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async void AddTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await ShowTaxGroupEditorDialog(null);
            if (result.Group != null)
            {
                TaxGroupsModule.Create(result.Group, result.RuleIds);
                ViewModel.LoadTaxData();
                ViewModel.StatusMessage = LocalizationHelper.GetString("Tax_Status_GroupAdded");
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.AddTaxGroupButton_Click");
        }
    }

    private async void EditTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.EditTaxGroupButton_Click");
        }
    }

    private async void DeleteTaxGroupButton_Click(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "TaxConfigurationPage.DeleteTaxGroupButton_Click");
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
