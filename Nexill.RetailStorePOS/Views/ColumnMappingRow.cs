using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace RetailStorePOS.WinUiLogin.Views;

/// <summary>
/// Importance hint shown next to a product field name in the mapping dialog.
/// </summary>
public enum ProductFieldImportance
{
    Optional,
    Recommended,
    Required,
}

/// <summary>
/// One choice in the CSV-column dropdown. <see cref="CsvHeader"/> is null for
/// the "(skip)" sentinel meaning "no CSV column maps to this product field".
/// </summary>
public sealed class CsvColumnOption
{
    public string DisplayName { get; set; } = string.Empty;
    public string? CsvHeader { get; set; }

    /// <summary>First non-empty sample value from this CSV column (preview-only).</summary>
    public string? SampleValue { get; set; }
}

/// <summary>
/// One row in the column-mapping dialog: a single product field on the left,
/// paired with the CSV column the user picks on the right.
/// </summary>
public sealed class ColumnMappingRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly SolidColorBrush MappedBrush =
        new(ColorHelper.FromArgb(0xFF, 0x22, 0xC5, 0x5E));   // green
    private static readonly SolidColorBrush SkippedBrush =
        new(ColorHelper.FromArgb(0xFF, 0x8F, 0x96, 0xA3));   // muted gray
    private static readonly SolidColorBrush MissingRequiredBrush =
        new(ColorHelper.FromArgb(0xFF, 0xDC, 0x26, 0x26));   // red

    private static readonly SolidColorBrush RequiredBadgeBrush =
        new(ColorHelper.FromArgb(0xFF, 0xDC, 0x26, 0x26));   // red
    private static readonly SolidColorBrush RecommendedBadgeBrush =
        new(ColorHelper.FromArgb(0xFF, 0x1D, 0x4E, 0xD8));   // blue
    private static readonly SolidColorBrush OptionalBadgeBrush =
        new(ColorHelper.FromArgb(0xFF, 0x64, 0x74, 0x8B));   // muted

    /// <summary>The product-field key (e.g. "Name", "Price"). Never null.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>Localized name of the product field shown on the left.</summary>
    public string FieldDisplayName { get; set; } = string.Empty;

    /// <summary>Required / Recommended / Optional importance.</summary>
    public ProductFieldImportance Importance { get; set; }

    /// <summary>Localized "Required"/"Recommended"/"Optional" badge text.</summary>
    public string ImportanceLabel { get; set; } = string.Empty;

    public bool IsRequired => Importance == ProductFieldImportance.Required;

    /// <summary>Brush used to color the importance badge.</summary>
    public Brush ImportanceBrush => Importance switch
    {
        ProductFieldImportance.Required    => RequiredBadgeBrush,
        ProductFieldImportance.Recommended => RecommendedBadgeBrush,
        _                                   => OptionalBadgeBrush,
    };

    /// <summary>Items shown in the row's CSV-column dropdown, including a "(skip)" sentinel.</summary>
    public List<CsvColumnOption> CsvOptions { get; set; } = new();

    private CsvColumnOption _selectedCsvOption = new();
    public CsvColumnOption SelectedCsvOption
    {
        get => _selectedCsvOption;
        set
        {
            if (ReferenceEquals(_selectedCsvOption, value)) return;
            _selectedCsvOption = value;
            Notify(nameof(SelectedCsvOption));
            Notify(nameof(SampleText));
            Notify(nameof(IsMapped));
            Notify(nameof(StatusGlyph));
            Notify(nameof(StatusBrush));
        }
    }

    public bool IsMapped => !string.IsNullOrEmpty(_selectedCsvOption.CsvHeader);

    /// <summary>Sample value from the currently selected CSV column.</summary>
    public string SampleText => _selectedCsvOption.SampleValue ?? string.Empty;

    public string StatusGlyph
    {
        get
        {
            if (IsMapped) return "\uE73E";                 // checkmark
            return IsRequired ? "\uE783" : "\uE10A";       // warning vs disallowed
        }
    }

    public Brush StatusBrush
    {
        get
        {
            if (IsMapped) return MappedBrush;
            return IsRequired ? MissingRequiredBrush : SkippedBrush;
        }
    }

    private void Notify(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
