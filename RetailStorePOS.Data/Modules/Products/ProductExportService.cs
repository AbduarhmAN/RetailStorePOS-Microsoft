using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace RetailStorePOS.Data.Modules.Products;

public sealed class ProductExportService
{
    public void ExportToCsv(string filePath, IEnumerable<Product> products)
    {
        ExportToCsv(filePath, products, resolveImageSourcePath: null);
    }

    public void ExportToCsv(
        string filePath,
        IEnumerable<Product> products,
        Func<Product, string?>? resolveImageSourcePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Export file path cannot be empty.", nameof(filePath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = args => true, // quote all just to be safe with names
        };

        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, config);

        var exportDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? Directory.GetCurrentDirectory();
        var imageFolderName = $"{Path.GetFileNameWithoutExtension(filePath)}_images";
        var imageExportDirectory = Path.Combine(exportDirectory, imageFolderName);
        var imageIndex = 0;
        var rows = new List<ProductCsvRow>();

        foreach (var product in products)
        {
            var imagePath = TryExportImage(
                product,
                resolveImageSourcePath,
                imageExportDirectory,
                imageFolderName,
                ref imageIndex);

            rows.Add(new ProductCsvRow
            {
                Name = product.Name,
                Price = product.Price.ToString("F2", CultureInfo.InvariantCulture),
                CostPrice = product.CostPrice.ToString("F2", CultureInfo.InvariantCulture),
                Barcode = product.Barcode,
                Unit = product.Unit,
                Sku = product.Sku,
                QuantityStore = product.QuantityStore.ToString("F2", CultureInfo.InvariantCulture),
                QuantityWarehouse = product.QuantityWarehouse.ToString("F2", CultureInfo.InvariantCulture),
                TaxGroupId = product.TaxGroupId?.ToString(),
                ImagePath = imagePath,
                ProductDna = product.ProductDna
            });
        }

        csv.WriteRecords(rows);
    }

    private static string? TryExportImage(
        Product product,
        Func<Product, string?>? resolveImageSourcePath,
        string imageExportDirectory,
        string imageFolderName,
        ref int imageIndex)
    {
        if (resolveImageSourcePath is null)
        {
            return null;
        }

        var sourcePath = resolveImageSourcePath(product);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(imageExportDirectory);

            imageIndex++;
            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var fileName = $"{imageIndex:000}-{GetSafeFileName(product.ProductDna ?? product.Sku ?? product.Barcode ?? product.Name)}{extension.ToLowerInvariant()}";
            var destinationPath = Path.Combine(imageExportDirectory, fileName);
            File.Copy(sourcePath, destinationPath, overwrite: true);

            return Path.Combine(imageFolderName, fileName).Replace('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    private static string GetSafeFileName(string value)
    {
        var safe = string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        safe = new string(safe.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "product" : safe.ToLowerInvariant();
    }
}
