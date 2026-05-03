namespace RetailStorePOS.Data.Modules.Reporting;

using System.Collections.Generic;
using System.Linq;

public static class ImportExportSampleCatalog
{
    private static readonly List<ImportExportSample> _samples = new()
    {
        new ImportExportSample
        {
            SampleKey = "valid-standard",
            Name = "Valid Standard Catalog",
            IsMalformed = false,
            Content = "Barcode,Name,Description,Price,Cost,StockQuantity,Category,TaxCategory,ThumbnailPath\n10001,Test Product 1,Test Description 1,19.99,10.00,100,Test Category,Standard,thumb1.jpg\n10002,Test Product 2,Test Description 2,29.99,15.00,50,Test Category,Standard,thumb2.jpg",
            SampleType = SampleType.ValidRoundTrip,
            ExpectedOutcome = ExpectedSampleOutcome.ImportSucceeds
        },
        new ImportExportSample
        {
            SampleKey = "malformed-header",
            Name = "Malformed Header",
            IsMalformed = true,
            Content = "BadHeader1,BadHeader2,BadHeader3\n1,2,3",
            SampleType = SampleType.InvalidInput,
            ExpectedOutcome = ExpectedSampleOutcome.ImportRejected
        }
    };

    public static ImportExportSample? GetSample(string key)
    {
        return _samples.FirstOrDefault(s => s.SampleKey == key);
    }
}
