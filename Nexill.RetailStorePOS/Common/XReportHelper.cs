using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.WinUiLogin.Common;

public static class XReportHelper
{
    private const int ReceiptWidth = 48; // chars per line — Courier 7pt on 80mm (227pt) page
    private const string Separator = "------------------------------------------------";

    public static string GetReportPdfPath(DateTime date)
    {
        var folder = AppDataPaths.Combine("Reports");
        return Path.Combine(folder, $"XReport_{date:yyyyMMdd}.pdf");
    }

    public static string GenerateXReport(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var pdfPath = GetReportPdfPath(date);
        var folder = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(pdfPath, BuildReportPdf(sales, date, storeName));
        return pdfPath;
    }

    public static bool TryOpenReportPdf(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            return false;
        }

        try
        {
            // MSIX sandbox virtualizes AppData paths — copy the PDF to a real,
            // user-accessible folder so the Windows shell can actually open it.
            var exportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Nexill Reports");
            Directory.CreateDirectory(exportFolder);

            var exportPath = Path.Combine(exportFolder, Path.GetFileName(pdfPath));
            File.Copy(pdfPath, exportPath, overwrite: true);

            Process.Start(new ProcessStartInfo
            {
                FileName = exportPath,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] BuildReportPdf(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var content = BuildReportContent(sales, date, storeName, out var visibleLineCount);
        var streamBytes = Encoding.ASCII.GetBytes(content);

        // Calculate page height from actual visible text lines, not PDF command count
        var pageHeight = Math.Max(842, visibleLineCount * 12 + 50);

        // Write raw bytes to a MemoryStream so every offset is physically exact
        using var ms = new MemoryStream();

        void Write(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
        }

        // PDF Header
        Write("%PDF-1.4\n");
        Write("%\xe2\xe3\xcf\xd3\n");

        // Track byte offsets for xref table
        var offsets = new long[6];

        // Object 1: Catalog
        offsets[0] = ms.Position;
        Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages
        offsets[1] = ms.Position;
        Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Object 3: Page
        offsets[2] = ms.Position;
        Write($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 227 {pageHeight}] /Resources << /Font << /F1 4 0 R /F2 6 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        // Object 4: Font (Courier)
        offsets[3] = ms.Position;
        Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n");

        // Object 5: Content Stream (the actual report drawing commands)
        offsets[4] = ms.Position;
        Write($"5 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        ms.Write(streamBytes, 0, streamBytes.Length);
        Write("endstream\nendobj\n");

        // Object 6: Font (Courier-Bold)
        offsets[5] = ms.Position;
        Write("6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>\nendobj\n");

        // Cross-reference table
        var xrefOffset = ms.Position;
        Write("xref\n");
        Write("0 7\n");
        Write("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:0000000000} 00000 n \n");
        }

        // Trailer
        Write("trailer\n");
        Write("<< /Size 7 /Root 1 0 R >>\n");
        Write("startxref\n");
        Write($"{xrefOffset}\n");
        Write("%%EOF\n");

        return ms.ToArray();
    }

    private static string BuildReportContent(IEnumerable<Sale> sales, DateTime date, string storeName, out int visibleLineCount)
    {
        var lines = BuildReportLines(sales, date, storeName).ToList();
        visibleLineCount = lines.Count;
        var content = new StringBuilder();

        content.Append("BT\n");
        content.Append("/F1 7 Tf\n");
        content.Append("11 TL\n");
        var startY = Math.Max(828, lines.Count * 12 + 20);
        content.Append($"2 {startY} Td\n");

        foreach (var line in lines)
        {
            if (line.StartsWith("##BOLD##", StringComparison.Ordinal))
            {
                content.Append("/F2 7 Tf\n");
                content.Append('(');
                content.Append(EscapePdfText(line[8..]));
                content.Append(") Tj\n");
                content.Append("T*\n");
                content.Append("/F1 7 Tf\n");
            }
            else if (line.StartsWith("##BOLD10##", StringComparison.Ordinal))
            {
                content.Append("/F2 7 Tf\n");
                content.Append('(');
                content.Append(EscapePdfText(line[10..]));
                content.Append(") Tj\n");
                content.Append("T*\n");
                content.Append("/F1 7 Tf\n");
            }
            else
            {
                content.Append('(');
                content.Append(EscapePdfText(line));
                content.Append(") Tj\n");
                content.Append("T*\n");
            }
        }

        content.Append("ET\n");
        return content.ToString();
    }

    private static IEnumerable<string> BuildReportLines(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var salesList = sales.OrderBy(s => s.CreatedAt).ToList();

        yield return Separator;
        yield return "##BOLD10##" + CenterText(storeName);
        yield return CenterText("X REPORT (DAILY READ)");
        yield return CenterText($"Date: {date:MM/dd/yyyy}");
        yield return Separator;
        yield return string.Empty;

        yield return "##BOLD##" + FormatLine2Col("Receipt #", "Amount");
        yield return Separator;

        decimal totalGross = 0;
        decimal totalNet = 0;
        decimal totalTax = 0;
        decimal totalCash = 0;
        decimal totalCard = 0;
        int transactionCount = 0;

        foreach (var sale in salesList)
        {
            yield return FormatLine2Col(sale.ReceiptNumber.ToString("D6"), sale.Total.ToString("C2", CultureInfo.CurrentCulture));

            totalGross += sale.Total;
            totalNet += sale.Subtotal;
            totalTax += sale.Tax;
            transactionCount++;

            if (sale.PaymentType != null && sale.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                totalCash += sale.Total;
            }
            else
            {
                totalCard += sale.Total;
            }
        }

        if (!salesList.Any())
        {
            yield return CenterText("NO TRANSACTIONS");
        }

        yield return Separator;
        yield return string.Empty;
        yield return "##BOLD10##" + "SUMMARY";
        yield return Separator;
        yield return FormatLine2Col("Transactions", transactionCount.ToString());
        yield return FormatLine2Col("Total Gross Sales", totalGross.ToString("C2", CultureInfo.CurrentCulture));
        yield return FormatLine2Col("Total Net Sales", totalNet.ToString("C2", CultureInfo.CurrentCulture));
        yield return FormatLine2Col("Total Tax Collected", totalTax.ToString("C2", CultureInfo.CurrentCulture));
        yield return Separator;
        yield return FormatLine2Col("Cash in Drawer", totalCash.ToString("C2", CultureInfo.CurrentCulture));
        yield return FormatLine2Col("Card Payments", totalCard.ToString("C2", CultureInfo.CurrentCulture));
        yield return Separator;
        yield return string.Empty;
        yield return CenterText("End of Report");
    }

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
