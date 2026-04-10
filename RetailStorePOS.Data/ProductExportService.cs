using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Services;

public sealed class ProductExportService
{
    public void ExportToCsv(string filePath, IEnumerable<Product> products)
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

        var rows = products.Select(p => new ProductCsvRow
        {
            Name = p.Name,
            Price = p.Price.ToString("F2", CultureInfo.InvariantCulture),
            CostPrice = p.CostPrice.ToString("F2", CultureInfo.InvariantCulture),
            Barcode = p.Barcode,
            Unit = p.Unit,
            Sku = p.Sku,
            QuantityStore = p.QuantityStore.ToString("F2", CultureInfo.InvariantCulture),
            QuantityWarehouse = p.QuantityWarehouse.ToString("F2", CultureInfo.InvariantCulture),
            TaxGroupId = p.TaxGroupId?.ToString()
        });

        csv.WriteRecords(rows);
    }
}
