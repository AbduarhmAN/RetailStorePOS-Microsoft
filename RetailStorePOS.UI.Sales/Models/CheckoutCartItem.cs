using System.Text.Json;
using Microsoft.UI.Xaml;
using RetailStorePOS.Data.Modules.Tax;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Sales.Models;

public sealed class CheckoutCartItem : ObservableObject
{
    private decimal _quantity = 1m;
    private decimal _price;
    private string _currencyCode = "USD";
    private bool _canOverridePrice;
    private string? _unit;

    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string? Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                OnPropertyChanged(nameof(DisplayUnitText));
                OnPropertyChanged(nameof(QuantityPriceUnitText));
            }
        }
    }

    public decimal OriginalPrice { get; set; }

    public decimal CostPrice { get; set; }

    /// <summary>
    /// The full list of tax rules applied to this cart item from the product's Tax Group.
    /// When populated, TaxAmount is computed by iterating these rules.
    /// When empty, falls back to the legacy TaxRatePercent flat calculation.
    /// </summary>
    public List<TaxRule> AppliedTaxRules { get; set; } = new();

    /// <summary>
    /// Legacy/display-only combined percentage rate.
    /// Used for backward compatibility with the sale_items.tax_rate_percent column.
    /// </summary>
    public decimal TaxRatePercent { get; set; }

    public bool CanOverridePrice
    {
        get => _canOverridePrice;
        set
        {
            if (SetProperty(ref _canOverridePrice, value))
            {
                OnPropertyChanged(nameof(PriceOverrideVisibility));
                OnPropertyChanged(nameof(PriceReadOnlyVisibility));
            }
        }
    }

    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                RaiseMoneyPropertiesChanged();
            }
        }
    }

    public double PriceDouble
    {
        get => (double)Price;
        set => Price = Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                RaiseMoneyPropertiesChanged();
                OnPropertyChanged(nameof(QuantityText));
            }
        }
    }

    public decimal GrossLineTotal => Math.Round(Price * Quantity, 2, MidpointRounding.AwayFromZero);

    public decimal LineTotal => GrossLineTotal;

    public decimal NetLineTotal => CalculateTaxBreakdown().NetLineTotal;

    public decimal TaxAmount => CalculateTaxBreakdown().TaxAmount;

    public string QuantityText => Quantity.ToString("0.##");

    public string PriceText => CurrencyDisplayHelper.FormatAmount(Price, _currencyCode);

    public string PriceAmountText => CurrencyDisplayHelper.FormatNumber(Price);

    public string OriginalPriceText => CurrencyDisplayHelper.FormatAmount(OriginalPrice, _currencyCode);

    public string LineTotalText => CurrencyDisplayHelper.FormatAmount(LineTotal, _currencyCode);

    public string LineTotalAmountText => CurrencyDisplayHelper.FormatNumber(LineTotal);

    public string DisplayUnitText => string.IsNullOrWhiteSpace(Unit) ? "Unit" : Unit.Trim();

    public string QuantityPriceUnitText => $"x {PriceAmountText} / {DisplayUnitText}";

    public string TaxSnapshotJson => BuildTaxSnapshotJson();

    public bool IsPriceOverridden => Math.Round(Price, 2, MidpointRounding.AwayFromZero) != Math.Round(OriginalPrice, 2, MidpointRounding.AwayFromZero);

    public Visibility PriceOverrideVisibility => CanOverridePrice && ProductId > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PriceReadOnlyVisibility => PriceOverrideVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public Visibility OriginalPriceVisibility => IsPriceOverridden ? Visibility.Visible : Visibility.Collapsed;

    public void UpdateCurrencyCode(string currencyCode)
    {
        var normalizedCurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
            ? "USD"
            : currencyCode.Trim().ToUpperInvariant();

        if (_currencyCode == normalizedCurrencyCode)
        {
            return;
        }

        _currencyCode = normalizedCurrencyCode;
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(OriginalPriceText));
        OnPropertyChanged(nameof(LineTotalText));
    }

    private void RaiseMoneyPropertiesChanged()
    {
        OnPropertyChanged(nameof(PriceDouble));
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(GrossLineTotal));
        OnPropertyChanged(nameof(NetLineTotal));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(PriceAmountText));
        OnPropertyChanged(nameof(OriginalPriceText));
        OnPropertyChanged(nameof(LineTotalText));
        OnPropertyChanged(nameof(LineTotalAmountText));
        OnPropertyChanged(nameof(QuantityPriceUnitText));
        OnPropertyChanged(nameof(IsPriceOverridden));
        OnPropertyChanged(nameof(PriceOverrideVisibility));
        OnPropertyChanged(nameof(PriceReadOnlyVisibility));
        OnPropertyChanged(nameof(OriginalPriceVisibility));
    }

    public string BuildTaxSnapshotJson()
    {
        var breakdown = CalculateTaxBreakdown();
        var payload = new
        {
            grossLineTotal = breakdown.GrossLineTotal,
            netLineTotal = breakdown.NetLineTotal,
            taxAmount = breakdown.TaxAmount,
            inclusiveTaxAmount = breakdown.InclusiveTaxAmount,
            exclusiveTaxAmount = breakdown.ExclusiveTaxAmount,
            price = Price,
            originalPrice = OriginalPrice,
            quantity = Quantity,
            costPrice = CostPrice,
            rules = breakdown.RuleBreakdowns.Select(rule => new
            {
                ruleId = rule.RuleId,
                name = rule.Name,
                calcType = rule.CalcType,
                scope = rule.Scope,
                rateValue = rule.RateValue,
                isInclusive = rule.IsInclusive,
                taxAmount = rule.TaxAmount
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    private TaxBreakdown CalculateTaxBreakdown()
    {
        var grossLineTotal = GrossLineTotal;

        if (AppliedTaxRules.Count == 0)
        {
            var legacyTax = Math.Round(grossLineTotal * TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
            return new TaxBreakdown(
                grossLineTotal,
                grossLineTotal,
                legacyTax,
                0m,
                legacyTax,
                Array.Empty<TaxRuleBreakdown>());
        }

        var activeRules = AppliedTaxRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.SequenceOrder)
            .ToList();

        if (activeRules.Count == 0)
        {
            return new TaxBreakdown(
                grossLineTotal,
                grossLineTotal,
                0m,
                0m,
                0m,
                Array.Empty<TaxRuleBreakdown>());
        }

        decimal inclusiveBase = grossLineTotal;
        decimal inclusiveTax = 0m;
        decimal exclusiveTax = 0m;
        var ruleBreakdowns = new List<TaxRuleBreakdown>(activeRules.Count);

        foreach (var rule in activeRules.Where(r => r.IsInclusive))
        {
            var ruleTax = CalculateRuleTax(rule, inclusiveBase);
            inclusiveTax += ruleTax;
            inclusiveBase = Math.Round(Math.Max(0m, inclusiveBase - ruleTax), 2, MidpointRounding.AwayFromZero);
            ruleBreakdowns.Add(new TaxRuleBreakdown(
                rule.Id,
                rule.Name,
                rule.CalcType,
                rule.Scope,
                rule.RateValue,
                true,
                ruleTax));
        }

        var exclusiveBase = inclusiveBase;
        foreach (var rule in activeRules.Where(r => !r.IsInclusive))
        {
            var ruleTax = CalculateRuleTax(rule, exclusiveBase);
            exclusiveTax += ruleTax;
            ruleBreakdowns.Add(new TaxRuleBreakdown(
                rule.Id,
                rule.Name,
                rule.CalcType,
                rule.Scope,
                rule.RateValue,
                false,
                ruleTax));
        }

        var taxAmount = Math.Round(inclusiveTax + exclusiveTax, 2, MidpointRounding.AwayFromZero);
        var netLineTotal = Math.Round(Math.Max(0m, grossLineTotal - inclusiveTax), 2, MidpointRounding.AwayFromZero);

        return new TaxBreakdown(
            grossLineTotal,
            netLineTotal,
            taxAmount,
            Math.Round(inclusiveTax, 2, MidpointRounding.AwayFromZero),
            Math.Round(exclusiveTax, 2, MidpointRounding.AwayFromZero),
            ruleBreakdowns);
    }

    private decimal CalculateRuleTax(TaxRule rule, decimal baseAmount)
    {
        var rate = rule.RateValue;
        if (rate <= 0m)
        {
            return 0m;
        }

        return rule.CalcType switch
        {
            "PERCENTAGE" => rule.IsInclusive
                ? Math.Round(baseAmount * rate / (100m + rate), 2, MidpointRounding.AwayFromZero)
                : Math.Round(baseAmount * rate / 100m, 2, MidpointRounding.AwayFromZero),

            "FIXED_AMOUNT" => Math.Round(rate * Quantity, 2, MidpointRounding.AwayFromZero),

            "PERCENTAGE_ON_MARGIN" => rule.IsInclusive
                ? Math.Round(
                    Math.Max(0m, baseAmount - (CostPrice * Quantity)) * rate / (100m + rate),
                    2, MidpointRounding.AwayFromZero)
                : Math.Round(
                    Math.Max(0m, Price - CostPrice) * Quantity * rate / 100m,
                    2, MidpointRounding.AwayFromZero),

            "PER_UNIT_MEASURE" => Math.Round(rate * Quantity, 2, MidpointRounding.AwayFromZero),

            "REVERSE_CHARGE" => 0m,

            _ => 0m
        };
    }

    private sealed record TaxBreakdown(
        decimal GrossLineTotal,
        decimal NetLineTotal,
        decimal TaxAmount,
        decimal InclusiveTaxAmount,
        decimal ExclusiveTaxAmount,
        IReadOnlyList<TaxRuleBreakdown> RuleBreakdowns);

    private sealed record TaxRuleBreakdown(
        long RuleId,
        string Name,
        string CalcType,
        string Scope,
        decimal RateValue,
        bool IsInclusive,
        decimal TaxAmount);
}
