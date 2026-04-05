using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Models;

public sealed class SearchResultItem : ObservableObject
{
    private static readonly SolidColorBrush DefaultBackgroundBrush = new(ColorHelper.FromArgb(255, 250, 250, 252));

    private static readonly SolidColorBrush HoverBackgroundBrush = new(ColorHelper.FromArgb(255, 243, 244, 246));

    private static readonly SolidColorBrush DefaultBorderBrush = new(ColorHelper.FromArgb(255, 216, 224, 236));
    private static readonly SolidColorBrush HoverBorderBrush = new(ColorHelper.FromArgb(255, 198, 210, 225));
    private static readonly SolidColorBrush DefaultChevronBrush = new(ColorHelper.FromArgb(255, 103, 115, 135));
    private static readonly SolidColorBrush SelectedBorderBrush = new(ColorHelper.FromArgb(255, 91, 141, 239));
    private static readonly SolidColorBrush SelectedChevronBrush = new(ColorHelper.FromArgb(255, 63, 111, 213));
    private bool _isSelected;
    private bool _isPointerOver;

    public SearchResultItem(Product product)
    {
        Product = product;
    }

    public Product Product { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CardBackgroundBrush));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
            OnPropertyChanged(nameof(ChevronBrush));
        }
    }

    public bool IsPointerOver
    {
        get => _isPointerOver;
        set
        {
            if (!SetProperty(ref _isPointerOver, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CardBackgroundBrush));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
            OnPropertyChanged(nameof(ChevronBrush));
        }
    }

    public string Name => Product.Name;

    public decimal Price => Product.Price;

    public Brush CardBackgroundBrush => IsPointerOver ? HoverBackgroundBrush : DefaultBackgroundBrush;

    public Brush CardBorderBrush => IsSelected
        ? SelectedBorderBrush
        : IsPointerOver
            ? HoverBorderBrush
            : DefaultBorderBrush;

    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : IsPointerOver ? new Thickness(1.5) : new Thickness(1);

    public Brush ChevronBrush => IsSelected ? SelectedChevronBrush : DefaultChevronBrush;
}


