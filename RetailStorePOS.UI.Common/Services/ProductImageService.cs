using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using LocalAppDataPaths = RetailStorePOS.Data.AppDataPaths;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Handles product image processing, thumbnail generation, and app-owned media storage.
/// </summary>
public class ProductImageService
{
    private const string MediaFolderName = "ProductMedia";
    private const string ThumbnailsFolderName = "Thumbnails";
    private const double GridThumbnailQualityScale = 1.25;
    private static readonly int[] GridThumbnailSizes = [256, 384, 512, 768];

    /// <summary>
    /// Gets the absolute path to the app-owned product media folder.
    /// </summary>
    public static string GetMediaRoot() => LocalAppDataPaths.Combine(MediaFolderName);

    /// <summary>
    /// Gets the absolute path to the app-owned thumbnails folder.
    /// </summary>
    public static string GetThumbnailsRoot() => LocalAppDataPaths.Combine(MediaFolderName, ThumbnailsFolderName);

    /// <summary>
    /// Generates a thumbnail from a source image path and saves it to the app-owned thumbnails folder.
    /// </summary>
    /// <param name="sourcePath">Absolute path to the source image.</param>
    /// <returns>The relative path to the generated 256px thumbnail for SQL storage, or null if generation failed.</returns>
    public async Task<string?> GenerateThumbnailAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return null;

        try
        {
            var thumbnailsRoot = GetThumbnailsRoot();
            if (!Directory.Exists(thumbnailsRoot))
            {
                Directory.CreateDirectory(thumbnailsRoot);
            }

            var mediaId = Guid.NewGuid().ToString("N");
            var storedRelativePath = BuildVariantRelativePath(mediaId, GridThumbnailSizes[0]);

            using var win32Stream = File.OpenRead(sourcePath);
            using var sourceStream = win32Stream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(sourceStream);
            var originalWidth = decoder.PixelWidth;
            var originalHeight = decoder.PixelHeight;

            var storageFolder = await StorageFolder.GetFolderFromPathAsync(thumbnailsRoot);

            foreach (var size in GridThumbnailSizes)
            {
                // Fit logic: Calculate scale to fit within 'size' while preserving aspect ratio
                double scale = Math.Min((double)size / originalWidth, (double)size / originalHeight);
                uint scaledWidth = (uint)Math.Max(1, (uint)(originalWidth * scale));
                uint scaledHeight = (uint)Math.Max(1, (uint)(originalHeight * scale));

                var fileName = BuildVariantFileName(mediaId, size);
                var destFile = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var destStream = await destFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var propertySet = new BitmapPropertySet();
                    var qualityValue = new BitmapTypedValue(0.85, Windows.Foundation.PropertyType.Single);
                    propertySet.Add("ImageQuality", qualityValue);

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destStream, propertySet);

                    using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        new BitmapTransform
                        {
                            ScaledWidth = scaledWidth,
                            ScaledHeight = scaledHeight,
                            InterpolationMode = BitmapInterpolationMode.Fant
                        },
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.ColorManageToSRgb))
                    {
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        await encoder.FlushAsync();
                    }
                }
            }

            return storedRelativePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thumbnail generation failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the smallest generated thumbnail variant that can cover the displayed image size sharply.
    /// </summary>
    public ProductImageVariant? ResolveThumbnailVariantForDisplay(
        string? storedThumbnailPath,
        double imageBoxWidthDip,
        double rasterizationScale)
    {
        if (string.IsNullOrWhiteSpace(storedThumbnailPath))
        {
            return null;
        }

        var displayPixels = Math.Max(1, imageBoxWidthDip * Math.Max(1, rasterizationScale));
        var requiredPixels = (int)Math.Ceiling(displayPixels * GridThumbnailQualityScale);
        var mediaId = TryGetMediaId(storedThumbnailPath);

        if (!string.IsNullOrWhiteSpace(mediaId))
        {
            foreach (var size in GridThumbnailSizes)
            {
                if (size < requiredPixels)
                {
                    continue;
                }

                var relativePath = BuildVariantRelativePath(mediaId, size);
                var absolutePath = ResolveThumbnailPath(relativePath);
                if (!string.IsNullOrWhiteSpace(absolutePath))
                {
                    return new ProductImageVariant(absolutePath, size);
                }
            }

            for (var index = GridThumbnailSizes.Length - 1; index >= 0; index--)
            {
                var size = GridThumbnailSizes[index];
                var relativePath = BuildVariantRelativePath(mediaId, size);
                var absolutePath = ResolveThumbnailPath(relativePath);
                if (!string.IsNullOrWhiteSpace(absolutePath))
                {
                    return new ProductImageVariant(absolutePath, size);
                }
            }
        }

        var fallbackPath = ResolveThumbnailPath(storedThumbnailPath);
        return string.IsNullOrWhiteSpace(fallbackPath)
            ? null
            : new ProductImageVariant(fallbackPath, TryGetVariantSize(storedThumbnailPath) ?? GridThumbnailSizes[0]);
    }

    /// <summary>
    /// Resolves a stored relative thumbnail path to an absolute path for UI binding.
    /// </summary>
    /// <param name="relativeThumbnailPath">The relative path stored in the database.</param>
    /// <returns>An absolute path, or null if the path is invalid or outside the app media root.</returns>
    public string? ResolveThumbnailPath(string? relativeThumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(relativeThumbnailPath)) return null;

        try
        {
            var absolutePath = Path.GetFullPath(LocalAppDataPaths.Combine(relativeThumbnailPath.Split('/', '\\')));
            var mediaRoot = Path.GetFullPath(GetMediaRoot())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!absolutePath.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(absolutePath) ? absolutePath : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes an app-owned thumbnail file if it exists.
    /// </summary>
    /// <param name="relativeThumbnailPath">The relative path stored in the database.</param>
    /// <returns>True if deletion succeeded or file did not exist; false if deletion failed.</returns>
    public bool TryCleanupThumbnail(string? relativeThumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(relativeThumbnailPath)) return true;

        try
        {
            var mediaId = TryGetMediaId(relativeThumbnailPath);
            if (!string.IsNullOrWhiteSpace(mediaId))
            {
                foreach (var size in GridThumbnailSizes)
                {
                    var variantPath = ResolveThumbnailPath(BuildVariantRelativePath(mediaId, size));
                    if (variantPath != null && File.Exists(variantPath))
                    {
                        File.Delete(variantPath);
                    }
                }
            }
            else
            {
                var absolutePath = ResolveThumbnailPath(relativeThumbnailPath);
                if (absolutePath != null && File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildVariantRelativePath(string mediaId, int size)
    {
        return Path.Combine(MediaFolderName, ThumbnailsFolderName, BuildVariantFileName(mediaId, size))
            .Replace('\\', '/');
    }

    private static string BuildVariantFileName(string mediaId, int size) => $"{mediaId}_{size}.jpg";

    private static string? TryGetMediaId(string relativeThumbnailPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativeThumbnailPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        foreach (var size in GridThumbnailSizes)
        {
            var suffix = $"_{size}";
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^suffix.Length];
            }
        }

        return null;
    }

    private static int? TryGetVariantSize(string relativeThumbnailPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativeThumbnailPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        foreach (var size in GridThumbnailSizes)
        {
            if (fileName.EndsWith($"_{size}", StringComparison.OrdinalIgnoreCase))
            {
                return size;
            }
        }

        return null;
    }
}

public sealed record ProductImageVariant(string Path, int PixelWidth);
