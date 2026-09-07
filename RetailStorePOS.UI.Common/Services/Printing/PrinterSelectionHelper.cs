using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;

namespace RetailStorePOS.UI.Common.Services.Printing;

public static class PrinterSelectionHelper
{
    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        try
        {
            return PrinterSettings.InstalledPrinters
                .Cast<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool IsInstalledPrinter(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        return GetInstalledPrinters().Any(name => string.Equals(name, printerName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
