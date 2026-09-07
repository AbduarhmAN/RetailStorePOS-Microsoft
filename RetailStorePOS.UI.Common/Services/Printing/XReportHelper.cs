using System.Diagnostics;
using System.Text;
using System.Globalization;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Sales;

namespace RetailStorePOS.UI.Common.Services.Printing;

public static class XReportHelper
{
    private const int ReceiptWidth = 48;
    private const int PdfPageWidthPoints = 227;
    private const int PdfTopMarginPoints = 18;
    private const int PdfBottomMarginPoints = 18;
    private const int PdfLineHeightPoints = 11;
    private const string Separator = "------------------------------------------------";

    public static string GetReportPdfPath(DateTime date)
    {
        var folder = AppDataPaths.Combine("Reports");
        return Path.Combine(folder, $"XReport_{date:yyyyMMdd}.pdf");
    }

    public static string GetReportPdfPath(DateTime fromDate, DateTime toDate, DateTime generatedAtLocal)
    {
        var folder = AppDataPaths.Combine("Reports");
        return Path.Combine(folder, $"XReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}_{generatedAtLocal:HHmmss}.pdf");
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

    public static string GenerateXReport(IEnumerable<Sale> sales, DateTime fromDate, DateTime toDate, string periodLabel, string storeName)
    {
        var pdfPath = GetReportPdfPath(fromDate.Date, toDate.Date, DateTime.Now);
        var folder = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(pdfPath, BuildReportPdf(sales, fromDate.Date, toDate.Date, periodLabel, storeName));
        return pdfPath;
    }

    public static IReadOnlyList<XReportFileEntry> GetAvailableReports()
    {
        var folder = AppDataPaths.Combine("Reports");
        if (!Directory.Exists(folder))
        {
            return Array.Empty<XReportFileEntry>();
        }

        return Directory.EnumerateFiles(folder, "XReport_*.pdf", SearchOption.TopDirectoryOnly)
            .Select(path => new XReportFileEntry(
                path,
                TryParseBusinessDateFromFileName(Path.GetFileNameWithoutExtension(path)),
                File.GetLastWriteTime(path)))
            .OrderByDescending(entry => entry.BusinessDate ?? entry.LastModifiedLocal.Date)
            .ThenByDescending(entry => entry.LastModifiedLocal)
            .ToList();
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

            try
            {
                var file = Windows.Storage.StorageFile.GetFileFromPathAsync(exportPath).AsTask().GetAwaiter().GetResult();
                if (Windows.System.Launcher.LaunchFileAsync(file).AsTask().GetAwaiter().GetResult())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"XReportHelper.TryOpenReportPdf.LauncherFailed:{ex.Message}");
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exportPath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                StartupTrace.Write($"XReportHelper.TryOpenReportPdf.ShellFailed:{ex.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"XReportHelper.TryOpenReportPdf.ExportFailed:{ex.Message}");
            return false;
        }
    }

    private static DateTime? TryParseBusinessDateFromFileName(string? fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension) ||
            !fileNameWithoutExtension.StartsWith("XReport_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var datePart = fileNameWithoutExtension["XReport_".Length..];
        return DateTime.TryParseExact(
            datePart,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static byte[] BuildReportPdf(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        return BuildReportPdf(sales, date, date, "Today", storeName);
    }

    private static byte[] BuildReportPdf(IEnumerable<Sale> sales, DateTime fromDate, DateTime toDate, string periodLabel, string storeName)
    {
        var lines = BuildReportLines(sales, fromDate, toDate, periodLabel, storeName).ToList();
        return BuildTextPdf(lines);
    }

    private static byte[] BuildTextPdf(IReadOnlyList<ReportLine> lines)
    {
        var textLines = BuildPdfTextLines(lines).ToList();
        var pageHeight = Math.Max(
            220,
            PdfTopMarginPoints + PdfBottomMarginPoints + (textLines.Count * PdfLineHeightPoints));
        var content = BuildPdfContent(textLines, pageHeight);
        var contentBytes = Encoding.ASCII.GetBytes(content);

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            string.Format(
                CultureInfo.InvariantCulture,
                "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /Resources << /Font << /F1 4 0 R /F2 6 0 R >> >> /Contents 5 0 R >>\nendobj\n",
                PdfPageWidthPoints,
                pageHeight),
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n",
            string.Format(
                CultureInfo.InvariantCulture,
                "5 0 obj\n<< /Length {0} >>\nstream\n{1}endstream\nendobj\n",
                contentBytes.Length,
                content),
            "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>\nendobj\n"
        };

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        pdf.Append("%XReport\n");

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

    private static string BuildPdfContent(IReadOnlyList<PdfTextLine> lines, int pageHeight)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 7 Tf");
        content.AppendLine("11 TL");
        content.AppendFormat(CultureInfo.InvariantCulture, "2 {0} Td\n", pageHeight - PdfTopMarginPoints);

        foreach (var line in lines)
        {
            if (line.IsBold)
            {
                content.AppendLine("/F2 7 Tf");
            }

            content.Append('(');
            content.Append(EscapePdfText(line.Text));
            content.AppendLine(") Tj");
            content.AppendLine("T*");

            if (line.IsBold)
            {
                content.AppendLine("/F1 7 Tf");
            }
        }

        content.AppendLine("ET");
        return content.ToString();
    }

    private static IEnumerable<PdfTextLine> BuildPdfTextLines(IReadOnlyList<ReportLine> lines)
    {
        foreach (var line in lines)
        {
            switch (line.Style)
            {
                case LineStyle.Separator:
                    yield return new PdfTextLine(Separator);
                    break;
                case LineStyle.Empty:
                    yield return new PdfTextLine(string.Empty);
                    break;
                case LineStyle.TitleBold:
                    yield return new PdfTextLine(CenterText(line.Text), IsBold: true);
                    break;
                case LineStyle.Bold:
                    yield return new PdfTextLine(Truncate(line.Text, ReceiptWidth), IsBold: true);
                    break;
                case LineStyle.Centered:
                    yield return new PdfTextLine(CenterText(line.Text));
                    break;
                case LineStyle.TwoColumn:
                    yield return new PdfTextLine(FormatLine2Col(line.Text, line.RightText ?? string.Empty));
                    break;
                case LineStyle.TwoColumnBold:
                    yield return new PdfTextLine(FormatLine2Col(line.Text, line.RightText ?? string.Empty), IsBold: true);
                    break;
                default:
                    yield return new PdfTextLine(Truncate(line.Text, ReceiptWidth));
                    break;
            }
        }
    }

    private static IEnumerable<ReportLine> BuildReportLines(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        return BuildReportLines(sales, date, date, "Today", storeName);
    }

    private static IEnumerable<ReportLine> BuildReportLines(IEnumerable<Sale> sales, DateTime fromDate, DateTime toDate, string periodLabel, string storeName)
    {
        var salesList = sales.OrderBy(s => s.CreatedAt).ToList();

        yield return new(LineStyle.Separator);
        yield return new(LineStyle.TitleBold, storeName);
        yield return new(LineStyle.Centered, "X REPORT");
        yield return new(LineStyle.Centered, string.IsNullOrWhiteSpace(periodLabel) ? "Period Report" : periodLabel.Trim());
        yield return new(LineStyle.Centered, fromDate.Date == toDate.Date
            ? $"Date: {fromDate:MM/dd/yyyy}"
            : $"Period: {fromDate:MM/dd/yyyy} - {toDate:MM/dd/yyyy}");
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.Empty);

        yield return new(LineStyle.TwoColumnBold, "Receipt #", "Amount");
        yield return new(LineStyle.Separator);

        decimal totalGross = 0;
        decimal totalNet = 0;
        decimal totalTax = 0;
        decimal totalCash = 0;
        decimal totalCard = 0;
        int transactionCount = 0;

        foreach (var sale in salesList)
        {
            yield return new(LineStyle.TwoColumn, sale.ReceiptNumber.ToString("D6"), CurrencyDisplayHelper.FormatConfiguredAmount(sale.Total));

            totalGross += sale.Total;
            totalNet += sale.Subtotal;
            totalTax += sale.Tax;
            transactionCount++;

            if (IsCashPayment(sale.PaymentType))
            {
                totalCash += sale.Total;
            }
            else if (IsCardPayment(sale.PaymentType))
            {
                totalCard += sale.Total;
            }
        }

        if (!salesList.Any())
        {
            yield return new(LineStyle.Centered, "NO TRANSACTIONS");
        }

        yield return new(LineStyle.Separator);
        yield return new(LineStyle.Empty);
        yield return new(LineStyle.Bold, "SUMMARY");
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.TwoColumn, "Transactions", transactionCount.ToString());
        yield return new(LineStyle.TwoColumn, "Total Gross Sales", CurrencyDisplayHelper.FormatConfiguredAmount(totalGross));
        yield return new(LineStyle.TwoColumn, "Total Net Sales", CurrencyDisplayHelper.FormatConfiguredAmount(totalNet));
        yield return new(LineStyle.TwoColumn, "Total Tax Collected", CurrencyDisplayHelper.FormatConfiguredAmount(totalTax));
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.TwoColumn, "Cash Payments", CurrencyDisplayHelper.FormatConfiguredAmount(totalCash));
        yield return new(LineStyle.TwoColumn, "Card Payments", CurrencyDisplayHelper.FormatConfiguredAmount(totalCard));
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.Empty);
        yield return new(LineStyle.Centered, "End of Report");
    }

    private static bool IsCashPayment(string? paymentType)
    {
        return string.Equals(paymentType, "Cash", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCardPayment(string? paymentType)
    {
        return string.Equals(paymentType, "Card", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentType, "Credit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentType, "Debit", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class XReportFileEntry
    {
        public XReportFileEntry(string pdfPath, DateTime? businessDate, DateTime lastModifiedLocal)
        {
            PdfPath = pdfPath;
            BusinessDate = businessDate;
            LastModifiedLocal = lastModifiedLocal;
        }

        public string PdfPath { get; }
        public DateTime? BusinessDate { get; }
        public DateTime LastModifiedLocal { get; }
    }

    private static string CenterText(string text)
    {
        text = Truncate(text, ReceiptWidth);
        if (text.Length >= ReceiptWidth) return text;
        var pad = (ReceiptWidth - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string FormatLine2Col(string left, string right)
    {
        right = Truncate(right, ReceiptWidth - 1);
        var leftMax = Math.Max(1, ReceiptWidth - right.Length - 1);
        left = Truncate(left, leftMax);

        var gap = ReceiptWidth - left.Length - right.Length;
        if (gap < 1) gap = 1;
        return left + new string(' ', gap) + right;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

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

    private enum LineStyle { Normal, Separator, TitleBold, Bold, Centered, TwoColumn, TwoColumnBold, Empty }

    private sealed record ReportLine(LineStyle Style, string Text = "", string? RightText = null);
    private sealed record PdfTextLine(string Text, bool IsBold = false);
}
