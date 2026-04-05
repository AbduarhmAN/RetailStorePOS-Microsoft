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
        var folder = AppDataPaths.Combine("Receipts");

        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    public static string ArchiveReceipt(ReceiptSummary receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var pdfPath = GetReceiptPdfPath(receipt.ReceiptNumber);
        var folder = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(pdfPath, BuildReceiptPdf(receipt));
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

    private static byte[] BuildReceiptPdf(ReceiptSummary receipt)
    {
        var content = BuildReceiptContent(receipt);
        var contentBytes = Encoding.ASCII.GetBytes(content);

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream\nendobj\n"
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
        pdf.Append("0 6\n");
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.AppendFormat(CultureInfo.InvariantCulture, "{0:0000000000} 00000 n \n", offset);
        }

        pdf.Append("trailer\n");
        pdf.Append("<< /Size 6 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string BuildReceiptContent(ReceiptSummary receipt)
    {
        var lines = BuildReceiptLines(receipt).ToList();
        var content = new StringBuilder();

        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("12 TL");
        content.AppendLine("36 800 Td");

        foreach (var line in lines)
        {
            content.Append('(');
            content.Append(EscapePdfText(line));
            content.AppendLine(") Tj");
            content.AppendLine("T*");
        }

        content.AppendLine("ET");
        return content.ToString();
    }

    private static IEnumerable<string> BuildReceiptLines(ReceiptSummary receipt)
    {
        yield return "RETAIL STORE RECEIPT";
        yield return $"Receipt #{receipt.ReceiptNumber:D6}";
        yield return $"Date: {receipt.CreatedAt.ToLocalTime():MMM d, yyyy hh:mm tt}";

        if (!string.IsNullOrWhiteSpace(receipt.CashierName))
        {
            yield return $"Cashier: {receipt.CashierName}";
        }

        yield return string.Empty;
        yield return "ITEMS";
        yield return "Item                           Qty   Price      Total";

        foreach (var item in receipt.Items)
        {
            var name = Truncate(item.Name, 31);
            var quantity = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
            yield return string.Format(
                CultureInfo.InvariantCulture,
                "{0,-31} {1,5} {2,9} {3,9}",
                name,
                quantity,
                FormatMoney(item.Price),
                FormatMoney(item.LineTotal));
        }

        yield return string.Empty;
        yield return string.Format(CultureInfo.InvariantCulture, "{0,-14} {1,9}", "Subtotal:", FormatMoney(receipt.Subtotal));
        yield return string.Format(CultureInfo.InvariantCulture, "{0,-14} {1,9}", "Tax:", FormatMoney(receipt.Tax));
        yield return string.Format(CultureInfo.InvariantCulture, "{0,-14} {1,9}", "Total:", FormatMoney(receipt.Total));
        yield return string.Format(CultureInfo.InvariantCulture, "{0,-14} {1,9}", "Tendered:", FormatMoney(receipt.Tendered));
        yield return string.Format(CultureInfo.InvariantCulture, "{0,-14} {1,9}", "Change:", FormatMoney(receipt.Change));
        yield return string.Empty;
        yield return "Thank you for your business.";
    }

    private static string FormatMoney(decimal amount) => $"${amount:0.00}";

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


