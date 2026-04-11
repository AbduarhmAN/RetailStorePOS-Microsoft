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
            var file = Windows.Storage.StorageFile.GetFileFromPathAsync(pdfPath).AsTask().GetAwaiter().GetResult();
            _ = Windows.System.Launcher.LaunchFileAsync(file);
            return true;
        }
        catch
        {
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

    private static byte[] BuildReportPdf(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var content = BuildReportContent(sales, date, storeName);
        var contentBytes = Encoding.ASCII.GetBytes(content);

        // Calculate needed height based on number of lines
        var lineCount = content.Split('\n').Length;
        var pageHeight = Math.Max(842, lineCount * 12 + 50);

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
        pdf.Append("%Report\n");

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

    private static string BuildReportContent(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var lines = BuildReportLines(sales, date, storeName).ToList();
        var content = new StringBuilder();

        content.AppendLine("BT");
        content.AppendLine("/F1 7 Tf");
        content.AppendLine("11 TL");
        var startY = Math.Max(828, lines.Count * 12 + 20); // Adjust startY dynamically
        content.AppendLine($"2 {startY} Td");

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

        decimal totalGross = 0; // The prompt said: Total Gross Sales (Total before discounts). BUT we only have Subtotal, Tax, Total. We will assume Subtotal is Gross if we don't have discount data at the receipt summary level.
                                // Actually, let's use: Gross (Subtotal), Net (Subtotal), Total Cash (Cash payments Total).
                                // Prompt: Total Gross Sales (Total before discounts) Total Net Sales (Total after discounts, before tax)
                                // Let's compute Gross and Net properly if possible, else Subtotal for both.
                                // Actually, in Sale item: Price vs LineTotal.
        decimal totalNet = 0;
        decimal totalCash = 0;

        foreach (var sale in salesList)
        {
            yield return FormatLine2Col(sale.ReceiptNumber.ToString("D6"), sale.Total.ToString("C2", CultureInfo.CurrentCulture));

            // Reconstruct Gross
            decimal saleGross = 0;
            foreach (var item in sale.Items)
            {
                // In SaleItem, Price is per unit, LineTotal is the final cost
                saleGross += (item.Price * item.Quantity);
            }
            if (saleGross < sale.Subtotal) saleGross = sale.Subtotal; // fallback

            totalGross += saleGross;
            totalNet += sale.Subtotal;

            if (sale.PaymentType != null && sale.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                totalCash += sale.Total;
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
        yield return FormatLine2Col("Total Gross Sales", totalGross.ToString("C2", CultureInfo.CurrentCulture));
        yield return FormatLine2Col("Total Net Sales", totalNet.ToString("C2", CultureInfo.CurrentCulture));
        yield return FormatLine2Col("Total Cash in Drawer", totalCash.ToString("C2", CultureInfo.CurrentCulture));
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
