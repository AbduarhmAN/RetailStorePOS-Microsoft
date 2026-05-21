using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

/// <summary>
/// Column-mapping dialog (flipped layout):
///
///   ┌─ PRODUCT FIELD ───────────┬───┬─ CSV COLUMN ─────────────┬───┐
///   │ Name *           REQUIRED │ ← │ [ Title             ▼ ] │ ✓ │
///   │                           │   │ Sample: "Apple Juice"   │   │
///   ├───────────────────────────┼───┼─────────────────────────┼───┤
///   │ Cost price       OPTIONAL │ ← │ [ Cost              ▼ ] │ ✓ │
///   │                           │   │ Sample: "8.00"          │   │
///   └───────────────────────────────────────────────────────────────┘
///
/// The product-field list on the left is fixed (Name, Price, Barcode, …).
/// On the right is a dropdown of the user's CSV column titles plus a
/// "(skip)" sentinel meaning "leave this field blank".
/// </summary>
public sealed partial class ImportColumnMappingDialog : ContentDialog, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService Loc => LocalizationService.Instance;

    public ObservableCollection<ColumnMappingRow> Rows { get; } = new();

    private readonly CsvPreview _preview;
    private readonly List<CsvColumnOption> _csvOptions;

    public ImportColumnMappingDialog(CsvPreview preview, ProductImportMapping? suggestion)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        InitializeComponent();

        _csvOptions = BuildCsvColumnOptions(preview);

        FoundColumnsText.Text = LocalizationHelper.Format(
            "ImportMapping_FoundColumnsFormat",
            preview.Headers.Count);

        BuildRows(suggestion);

        // Keep the required-name banner in sync with the live mapping state.
        foreach (var row in Rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
        }
        UpdateNameStatusBar();
    }

    /// <summary>
    /// Populated after the user clicks Import (and validation passes).
    /// </summary>
    public ProductImportMapping? ConfirmedMapping { get; private set; }

    /// <summary>
    /// Builds the dropdown items the user picks from for every product-field
    /// row: index 0 is the "(skip)" sentinel (CsvHeader = null), the rest are
    /// the CSV columns from the user's file in their original order, each
    /// carrying its first non-empty sample value.
    /// </summary>
    private static List<CsvColumnOption> BuildCsvColumnOptions(CsvPreview preview)
    {
        var list = new List<CsvColumnOption>(preview.Headers.Count + 1)
        {
            new()
            {
                CsvHeader   = null,
                DisplayName = LocalizationHelper.GetString("ImportMapping_None"),
                SampleValue = LocalizationHelper.GetString("ImportMapping_Sample_Skipped"),
            }
        };

        foreach (var header in preview.Headers)
        {
            var sample = FirstSampleFor(preview, header);
            list.Add(new CsvColumnOption
            {
                CsvHeader   = header,
                DisplayName = header,
                SampleValue = BuildSampleText(sample),
            });
        }

        return list;
    }

    /// <summary>
    /// Canonical product-field list shown in the dialog. The order here is
    /// the order users see top-to-bottom. Importance drives the badge color
    /// and validation rules.
    /// </summary>
    private static IReadOnlyList<(string Key, string LabelResKey, ProductFieldImportance Importance)> ProductFields { get; } =
        new[]
        {
            (nameof(ProductImportMapping.Name),              "ImportMapping_Field_Name",              ProductFieldImportance.Required),
            (nameof(ProductImportMapping.Price),             "ImportMapping_Field_Price",             ProductFieldImportance.Recommended),
            (nameof(ProductImportMapping.Barcode),           "ImportMapping_Field_Barcode",           ProductFieldImportance.Recommended),
            (nameof(ProductImportMapping.Sku),               "ImportMapping_Field_Sku",               ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.Unit),              "ImportMapping_Field_Unit",              ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.CostPrice),         "ImportMapping_Field_CostPrice",         ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.QuantityStore),     "ImportMapping_Field_QuantityStore",     ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.QuantityWarehouse), "ImportMapping_Field_QuantityWarehouse", ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.TaxGroupId),        "ImportMapping_Field_TaxGroupId",        ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.ImagePath),         "ImportMapping_Field_ImagePath",         ProductFieldImportance.Optional),
            (nameof(ProductImportMapping.ProductDna),        "ImportMapping_Field_ProductDna",        ProductFieldImportance.Optional),
        };

    private void BuildRows(ProductImportMapping? suggestion)
    {
        // Quick lookup for the auto-suggested CSV header per product field.
        string? Suggested(string fieldKey) => fieldKey switch
        {
            nameof(ProductImportMapping.Name)              => suggestion?.Name,
            nameof(ProductImportMapping.Price)             => suggestion?.Price,
            nameof(ProductImportMapping.Barcode)           => suggestion?.Barcode,
            nameof(ProductImportMapping.Sku)               => suggestion?.Sku,
            nameof(ProductImportMapping.Unit)              => suggestion?.Unit,
            nameof(ProductImportMapping.CostPrice)         => suggestion?.CostPrice,
            nameof(ProductImportMapping.QuantityStore)     => suggestion?.QuantityStore,
            nameof(ProductImportMapping.QuantityWarehouse) => suggestion?.QuantityWarehouse,
            nameof(ProductImportMapping.TaxGroupId)        => suggestion?.TaxGroupId,
            nameof(ProductImportMapping.ImagePath)         => suggestion?.ImagePath,
            nameof(ProductImportMapping.ProductDna)        => suggestion?.ProductDna,
            _                                              => null,
        };

        Rows.Clear();
        foreach (var (fieldKey, labelKey, importance) in ProductFields)
        {
            // Each row gets its own copy of the CSV-options list so binding
            // works correctly per row. The default selection is either the
            // auto-suggested CSV header for this product field, or the
            // "(skip)" sentinel.
            var optionsForRow = new List<CsvColumnOption>(_csvOptions);
            CsvColumnOption defaultOption = optionsForRow[0]; // "(skip)"
            var suggestedHeader = Suggested(fieldKey);
            if (!string.IsNullOrWhiteSpace(suggestedHeader))
            {
                var match = optionsForRow.FirstOrDefault(o =>
                    o.CsvHeader is not null &&
                    string.Equals(o.CsvHeader, suggestedHeader, StringComparison.OrdinalIgnoreCase));
                if (match is not null) defaultOption = match;
            }

            var row = new ColumnMappingRow
            {
                FieldKey         = fieldKey,
                FieldDisplayName = LocalizationHelper.GetString(labelKey),
                Importance       = importance,
                ImportanceLabel  = LocalizationHelper.GetString(importance switch
                {
                    ProductFieldImportance.Required    => "ImportMapping_Section_Required",
                    ProductFieldImportance.Recommended => "ImportMapping_Section_Recommended",
                    _                                   => "ImportMapping_Section_Optional",
                }),
                CsvOptions       = optionsForRow,
            };
            row.SelectedCsvOption = defaultOption;
            Rows.Add(row);
        }
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnMappingRow.SelectedCsvOption))
        {
            UpdateNameStatusBar();
        }
    }

    private void UpdateNameStatusBar()
    {
        var nameRow = Rows.FirstOrDefault(r =>
            string.Equals(r.FieldKey, nameof(ProductImportMapping.Name), StringComparison.Ordinal));

        if (nameRow is null || !nameRow.IsMapped)
        {
            NameStatusBar.Severity = InfoBarSeverity.Warning;
            NameStatusBar.Message  = LocalizationHelper.GetString("ImportMapping_Required_NameMissing");
            IsPrimaryButtonEnabled = false;
            return;
        }

        NameStatusBar.Severity = InfoBarSeverity.Success;
        NameStatusBar.Message  = LocalizationHelper.GetString("ImportMapping_Required_NameMapped");
        IsPrimaryButtonEnabled = true;
    }

    private static string? FirstSampleFor(CsvPreview preview, string header)
    {
        foreach (var row in preview.Rows)
        {
            if (row.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    private static string BuildSampleText(string? sampleValue)
    {
        if (sampleValue is null)
        {
            return string.Format(
                "{0} {1}",
                LocalizationHelper.GetString("ImportMapping_Sample_Prefix"),
                LocalizationHelper.GetString("ImportMapping_Sample_Empty"));
        }
        return string.Format(
            "{0} \u201C{1}\u201D",
            LocalizationHelper.GetString("ImportMapping_Sample_Prefix"),
            Truncate(sampleValue, 80));
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max) return value;
        return value.Substring(0, max - 1) + "\u2026";
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Build the result (product-field -> csv-header). Multiple product
        // fields can be mapped to the same CSV column; that's not a hard
        // error but we surface it as an informational warning.
        var mapping = new ProductImportMapping();
        var seenCsvHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = false;

        foreach (var row in Rows)
        {
            var csvHeader = row.SelectedCsvOption.CsvHeader;
            if (string.IsNullOrEmpty(csvHeader)) continue;
            if (!seenCsvHeaders.Add(csvHeader)) duplicates = true;
            AssignField(mapping, row.FieldKey, csvHeader);
        }

        if (string.IsNullOrEmpty(mapping.Name))
        {
            ValidationBar.Severity = InfoBarSeverity.Error;
            ValidationBar.Message  = LocalizationHelper.GetString("ImportMapping_Validation_NameRequired");
            ValidationBar.IsOpen   = true;
            args.Cancel = true;
            return;
        }

        if (duplicates)
        {
            // Don't block — just inform.
            ValidationBar.Severity = InfoBarSeverity.Informational;
            ValidationBar.Message  = LocalizationHelper.GetString("ImportMapping_Validation_Duplicates");
            ValidationBar.IsOpen   = true;
        }

        ConfirmedMapping = mapping;
    }

    private static void AssignField(ProductImportMapping mapping, string fieldKey, string csvHeader)
    {
        switch (fieldKey)
        {
            case nameof(ProductImportMapping.Name):              mapping.Name = csvHeader; break;
            case nameof(ProductImportMapping.Price):             mapping.Price = csvHeader; break;
            case nameof(ProductImportMapping.Barcode):           mapping.Barcode = csvHeader; break;
            case nameof(ProductImportMapping.Sku):               mapping.Sku = csvHeader; break;
            case nameof(ProductImportMapping.Unit):              mapping.Unit = csvHeader; break;
            case nameof(ProductImportMapping.CostPrice):         mapping.CostPrice = csvHeader; break;
            case nameof(ProductImportMapping.QuantityStore):     mapping.QuantityStore = csvHeader; break;
            case nameof(ProductImportMapping.QuantityWarehouse): mapping.QuantityWarehouse = csvHeader; break;
            case nameof(ProductImportMapping.TaxGroupId):        mapping.TaxGroupId = csvHeader; break;
            case nameof(ProductImportMapping.ImagePath):         mapping.ImagePath = csvHeader; break;
            case nameof(ProductImportMapping.ProductDna):        mapping.ProductDna = csvHeader; break;
        }
    }
}
