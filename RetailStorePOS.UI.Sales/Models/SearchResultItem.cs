using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Sales.Models;

public sealed partial class SearchResultItem : ObservableObject
{
    private static readonly ProductImageService ImageService = new();
    private static readonly SolidColorBrush DefaultBackgroundBrush = new(ColorHelper.FromArgb(255, 250, 250, 252));

    private static readonly SolidColorBrush HoverBackgroundBrush = new(ColorHelper.FromArgb(255, 243, 244, 246));

    private static readonly SolidColorBrush DefaultBorderBrush = new(ColorHelper.FromArgb(255, 216, 224, 236));
    private static readonly SolidColorBrush HoverBorderBrush = new(ColorHelper.FromArgb(255, 198, 210, 225));
    private static readonly SolidColorBrush DefaultChevronBrush = new(ColorHelper.FromArgb(255, 103, 115, 135));
    private static readonly SolidColorBrush SelectedBorderBrush = new(ColorHelper.FromArgb(255, 91, 141, 239));
    private static readonly SolidColorBrush SelectedChevronBrush = new(ColorHelper.FromArgb(255, 63, 111, 213));
    private bool _isSelected;
    private bool _isPointerOver;
    private string? _resolvedThumbnailPath;
    private ImageSource? _thumbnailImageSource;
    private int _thumbnailPixelWidth;
    private int _decodePixelWidth;

    public SearchResultItem(Product product)
    {
        Product = product;
        UpdateThumbnailForDisplay(170, 1);
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

    public string Name => Product.Name ?? string.Empty;

    public decimal Price => Product.Price;
    public string FormattedPrice => CurrencyDisplayHelper.FormatConfiguredAmount(Price);

    public string? ThumbnailPath => Product.ThumbnailPath;

    public string? ResolvedThumbnailPath => _resolvedThumbnailPath;

    public ImageSource? ThumbnailImageSource => _thumbnailImageSource;

    public bool HasThumbnail => _thumbnailImageSource != null;

    public bool HasNoThumbnail => !HasThumbnail;

    public Visibility ThumbnailVisibility => HasThumbnail ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoThumbnailVisibility => HasNoThumbnail ? Visibility.Visible : Visibility.Collapsed;

    public Brush CardBackgroundBrush => IsPointerOver ? HoverBackgroundBrush : DefaultBackgroundBrush;

    public Brush CardBorderBrush => IsSelected
        ? SelectedBorderBrush
        : IsPointerOver
            ? HoverBorderBrush
            : DefaultBorderBrush;

    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : IsPointerOver ? new Thickness(1.5) : new Thickness(1);

    public Brush ChevronBrush => IsSelected ? SelectedChevronBrush : DefaultChevronBrush;

    public void UpdateThumbnailForDisplay(double imageBoxWidthDip, double rasterizationScale)
    {
        var variant = ImageService.ResolveThumbnailVariantForDisplay(Product.ThumbnailPath, imageBoxWidthDip, rasterizationScale);
        if (variant is null)
        {
            SetThumbnail(null, null, 0, 0);
            return;
        }

        var nextDecodePixelWidth = Math.Max(1, (int)Math.Ceiling(imageBoxWidthDip * Math.Max(1, rasterizationScale)));
        if (_thumbnailPixelWidth > 0
            && variant.PixelWidth > 0
            && variant.PixelWidth < _thumbnailPixelWidth)
        {
            return;
        }

        if (string.Equals(_resolvedThumbnailPath, variant.Path, StringComparison.OrdinalIgnoreCase)
            && nextDecodePixelWidth <= _decodePixelWidth)
        {
            return;
        }

        SetThumbnail(
            variant.Path,
            CreateThumbnailImageSource(variant.Path, nextDecodePixelWidth),
            variant.PixelWidth,
            nextDecodePixelWidth);
    }

    private void SetThumbnail(string? path, ImageSource? imageSource, int pixelWidth, int decodePixelWidth)
    {
        var hadThumbnail = HasThumbnail;
        _resolvedThumbnailPath = path;
        _thumbnailImageSource = imageSource;
        _thumbnailPixelWidth = imageSource is null ? 0 : pixelWidth;
        _decodePixelWidth = imageSource is null ? 0 : decodePixelWidth;

        OnPropertyChanged(nameof(ResolvedThumbnailPath));
        OnPropertyChanged(nameof(ThumbnailImageSource));

        if (hadThumbnail != HasThumbnail)
        {
            OnPropertyChanged(nameof(HasThumbnail));
            OnPropertyChanged(nameof(HasNoThumbnail));
            OnPropertyChanged(nameof(ThumbnailVisibility));
            OnPropertyChanged(nameof(NoThumbnailVisibility));
        }
    }

    private static ImageSource? CreateThumbnailImageSource(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = Math.Max(1, decodePixelWidth)
            };
            image.UriSource = new Uri(path, UriKind.Absolute);
            return image;
        }
        catch
        {
            return null;
        }
    }
}
