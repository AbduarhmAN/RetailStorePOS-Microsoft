using System.Diagnostics;
using System.Globalization;
using System.Text;
using RetailStorePOS.Data;
using RetailStorePOS.WinUiLogin.Models;

namespace RetailStorePOS.WinUiLogin.Common;

public static class ReceiptHelper
{
    public static string GetReceiptPdfPath(long receiptNumber)
    {
        var folder = AppDataPaths.Combine("Receipts");
        return Path.Combine(folder, $"receipt_{receiptNumber:D6}.pdf");
    }

    public static void OpenReceiptPdf(long receiptNumber)
    {
        TryOpenReceiptPdf(GetReceiptPdfPath(receiptNumber));
    }

    public static void OpenReceiptsFolder()
    {
        // MSIX redirects Environment.GetFolderPath(LocalApplicationData) to:
        //   ...\Packages\{FamilyName}\LocalCache\Local
        // explorer.exe runs outside the sandbox, so we need the real physical path.
        string folder;
        try
        {
            var localCacheFolder = Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path;
            folder = Path.Combine(localCacheFolder, "Local", "RetailStorePOS", "Receipts");
        }
        catch
        {
            // Fallback for unpackaged builds
            folder = AppDataPaths.Combine("Receipts");
        }

        Directory.CreateDirectory(folder);
        Process.Start("explorer.exe", $"\"{folder}\"");
    }

    public static string ArchiveReceipt(ReceiptSummary receipt, string? storeName = null, string? storeAddress = null, string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        // Pull store info from settings if not provided
        storeName ??= LoginRuntime.Settings.GetStoreName() ?? "Store";
        storeAddress ??= LoginRuntime.Settings.GetStoreAddress();
        currencyCode ??= LoginRuntime.Settings.GetCurrencyCode() ?? "USD";

        var pdfPath = GetReceiptPdfPath(receipt.ReceiptNumber);
        var folder = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(pdfPath, BuildReceiptPdf(receipt, storeName, storeAddress, currencyCode));
        return pdfPath;
    }

    public static bool TryOpenReceiptPdf(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            return false;
        }

        try
        {
            // Use Windows.System.Launcher for MSIX packaged app compatibility
            var file = Windows.Storage.StorageFile.GetFileFromPathAsync(pdfPath).AsTask().GetAwaiter().GetResult();
            _ = Windows.System.Launcher.LaunchFileAsync(file);
            return true;
        }
        catch
        {
            // Fallback for unpackaged development builds
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private const int ReceiptWidth = 48; // chars per line — Courier 7pt on 80mm (227pt) page
    private const string Separator = "------------------------------------------------";
    private const string DottedSep = "................................................";

    private static byte[] BuildReceiptPdf(ReceiptSummary receipt, string storeName, string? storeAddress, string currencyCode)
    {
        var content = BuildReceiptContent(receipt, storeName, storeAddress, currencyCode);
        var contentBytes = Encoding.ASCII.GetBytes(content);

        // Use a narrow page width (226pt ~ 80mm) to match thermal receipt paper
        var pageHeight = 842;
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 227 {pageHeight}] /Resources << /Font << /F1 4 0 R /F2 6 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n",
            $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream\nendobj\n",
            "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>\nendobj\n"
        };

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        pdf.Append("%Receipt\n");

        var offsets = new List<int>(objects.Length);
        foreach (var obj in objects)
        {
            offsets.Add(pdf.Length);
            pdf.Append(obj);
        }

        var xrefOffset = pdf.Length;
        pdf.Append("xref\n");
        pdf.AppendFormat(CultureInfo.InvariantCulture, "0 {0}\n", objects.Length + 1);
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.AppendFormat(CultureInfo.InvariantCulture, "{0:0000000000} 00000 n \n", offset);
        }

        pdf.Append("trailer\n");
        pdf.AppendFormat(CultureInfo.InvariantCulture, "<< /Size {0} /Root 1 0 R >>\n", objects.Length + 1);
        pdf.Append("startxref\n");
        pdf.Append(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string BuildReceiptContent(ReceiptSummary receipt, string storeName, string? storeAddress, string currencyCode)
    {
        var lines = BuildReceiptLines(receipt, storeName, storeAddress, currencyCode).ToList();
        var content = new StringBuilder();

        content.AppendLine("BT");
        content.AppendLine("/F1 7 Tf");
        content.AppendLine("11 TL");
        content.AppendLine("2 828 Td");

        foreach (var line in lines)
        {
            if (line.StartsWith("##BOLD##", StringComparison.Ordinal))
            {
                content.AppendLine("/F2 7 Tf");
                content.Append('(');
                content.Append(EscapePdfText(line[8..]));
                content.AppendLine(") Tj");
                content.AppendLine("T*");
                content.AppendLine("/F1 7 Tf");
            }
            else if (line.StartsWith("##BOLD10##", StringComparison.Ordinal))
            {
                // Same 7pt width as regular text so it doesn't overflow the page
                content.AppendLine("/F2 7 Tf");
                content.Append('(');
                content.Append(EscapePdfText(line[10..]));
                content.AppendLine(") Tj");
                content.AppendLine("T*");
                content.AppendLine("/F1 7 Tf");
            }
            else
            {
                content.Append('(');
                content.Append(EscapePdfText(line));
                content.AppendLine(") Tj");
                content.AppendLine("T*");
            }
        }

        content.AppendLine("ET");
        return content.ToString();
    }

    private static IEnumerable<string> BuildReceiptLines(ReceiptSummary receipt, string storeName, string? storeAddress, string currencyCode)
    {
        // ── Header ──
        yield return Separator;
        yield return "##BOLD10##" + CenterText(storeName);
        if (!string.IsNullOrWhiteSpace(storeAddress))
        {
            yield return CenterText(storeAddress);
        }
        yield return Separator;

        // ── Transaction info ──
        yield return FormatLine2Col($"Slip: #{receipt.ReceiptNumber:D6}", $"Sale: {receipt.SaleId}");
        yield return $"Staff: {receipt.CashierName}";
        yield return $"Date: {receipt.CreatedAt.ToLocalTime().ToString("MM/dd/yy  h:mm tt", CultureInfo.InvariantCulture)}";
        yield return Separator;
        yield return string.Empty;

        // ── Column header ──
        yield return "##BOLD##" + FormatLine3Col("Description", "Qty", "Amount");
        yield return DottedSep;

        // ── Items ──
        foreach (var item in receipt.Items)
        {
            var name = Truncate(item.Name, 24);
            var qty = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
            var amount = FormatCurrency(item.LineTotal, currencyCode);
            yield return FormatLine3Col(name, qty, amount);
        }

        // ── Totals ──
        yield return Separator;
        yield return FormatLine2Col("Subtotal", FormatCurrency(receipt.Subtotal, currencyCode));
        yield return FormatLine2Col("Tax", FormatCurrency(receipt.Tax, currencyCode));
        yield return string.Empty;
        yield return "##BOLD10##" + FormatLine2Col($"Total {currencyCode}", FormatCurrency(receipt.Total, currencyCode));
        yield return string.Empty;
        yield return Separator;
        yield return FormatLine2Col("Tendered", FormatCurrency(receipt.Tendered, currencyCode));
        yield return "##BOLD##" + FormatLine2Col("Change", FormatCurrency(receipt.Change, currencyCode));
        yield return Separator;

        // ── Footer ──
        yield return string.Empty;
        yield return CenterText("Welcome again");
    }

    private static string FormatCurrency(decimal amount, string currencyCode) =>
        $"{currencyCode} {amount:0.00}";

    private static string CenterText(string text)
    {
        if (text.Length >= ReceiptWidth) return text;
        var pad = (ReceiptWidth - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string FormatLine2Col(string left, string right)
    {
        var gap = ReceiptWidth - left.Length - right.Length;
        if (gap < 1) gap = 1;
        return left + new string(' ', gap) + right;
    }

    private static string FormatLine3Col(string col1, string col2, string col3)
    {
        // Description | Qty | Amount
        int c1W = 24;
        int c2W = 5;
        int c3W = ReceiptWidth - c1W - c2W;

        var part1 = col1.Length > c1W ? col1[..c1W] : col1.PadRight(c1W);
        var padLeft2 = (c2W - col2.Length) / 2;
        if (padLeft2 < 0) padLeft2 = 0;
        var part2 = (new string(' ', padLeft2) + col2).PadRight(c2W);
        var part3 = col3.PadLeft(c3W);
        return part1 + part2 + part3;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        if (maxLength <= 3)
        {
            return value[..maxLength];
        }

        return value[..(maxLength - 3)] + "...";
    }

    private static string EscapePdfText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKD);
        var output = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (ch is '(' or ')' or '\\')
            {
                output.Append('\\').Append(ch);
            }
            else if (ch >= 32 && ch <= 126)
            {
                output.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                output.Append(' ');
            }
            else
            {
                output.Append('?');
            }
        }

        return output.ToString();
    }
}


