using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.WinUiLogin.Common;

public static class XReportHelper
{
    private const float PdfPageWidthPoints = 227f;
    private const int ImageWidthPixels = 1080;
    private const int ImageMarginPixels = 48;

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
        var lines = BuildReportLines(sales, date, storeName).ToList();
        var imageBytes = RenderReportImage(lines, out var imageWidth, out var imageHeight);
        return BuildImagePdf(imageBytes, imageWidth, imageHeight);
    }

    private static byte[] RenderReportImage(IReadOnlyList<ReportLine> lines, out int imageWidth, out int imageHeight)
    {
        imageWidth = ImageWidthPixels;
        var contentWidth = imageWidth - (ImageMarginPixels * 2);

        using var measureBitmap = new Bitmap(1, 1);
        using var measureGraphics = Graphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var titleFont = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var boldFont = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var normalFont = new Font("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel);

        var normalLineHeight = measureGraphics.MeasureString("Ag", normalFont).Height + 8f;
        var boldLineHeight = measureGraphics.MeasureString("Ag", boldFont).Height + 8f;
        var titleLineHeight = measureGraphics.MeasureString("Ag", titleFont).Height + 12f;

        var estimatedHeight = (int)Math.Ceiling((ImageMarginPixels * 2)
            + lines.Sum(line => line.Style switch
            {
                LineStyle.Separator => 18f,
                LineStyle.Empty => normalLineHeight * 0.7f,
                LineStyle.TitleBold => titleLineHeight,
                LineStyle.Bold => boldLineHeight,
                _ => normalLineHeight
            })
            + 24f);

        imageHeight = Math.Max(estimatedHeight, 400);

        using var bitmap = new Bitmap(imageWidth, imageHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float y = ImageMarginPixels;
        float left = ImageMarginPixels;
        float right = imageWidth - ImageMarginPixels;

        foreach (var line in lines)
        {
            switch (line.Style)
            {
                case LineStyle.Separator:
                    y += 4f;
                    graphics.DrawLine(Pens.Black, left, y, right, y);
                    y += 12f;
                    break;

                case LineStyle.TitleBold:
                    DrawCentered(graphics, line.Text, titleFont, Brushes.Black, ref y, left, contentWidth);
                    break;

                case LineStyle.Bold:
                    DrawLeft(graphics, line.Text, boldFont, Brushes.Black, ref y, left);
                    break;

                case LineStyle.Centered:
                    DrawCentered(graphics, line.Text, normalFont, Brushes.Black, ref y, left, contentWidth);
                    break;

                case LineStyle.TwoColumn:
                    DrawLeftRight(graphics, line.Text, line.RightText ?? string.Empty, normalFont, Brushes.Black, ref y, left, right);
                    break;

                case LineStyle.TwoColumnBold:
                    DrawLeftRight(graphics, line.Text, line.RightText ?? string.Empty, boldFont, Brushes.Black, ref y, left, right);
                    break;

                case LineStyle.Empty:
                    y += normalLineHeight * 0.7f;
                    break;

                default:
                    DrawLeft(graphics, line.Text, normalFont, Brushes.Black, ref y, left);
                    break;
            }
        }

        using var imageStream = new MemoryStream();
        bitmap.Save(imageStream, ImageFormat.Jpeg);
        return imageStream.ToArray();
    }

    private static byte[] BuildImagePdf(byte[] jpegBytes, int imageWidth, int imageHeight)
    {
        var pageWidth = PdfPageWidthPoints;
        var pageHeight = Math.Max(200f, pageWidth * imageHeight / imageWidth);
        var imageDrawCommand = $"q\n{pageWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)} 0 0 {pageHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)} 0 0 cm\n/Im1 Do\nQ\n";
        var contentBytes = Encoding.ASCII.GetBytes(imageDrawCommand);

        using var ms = new MemoryStream();

        static void WriteAscii(Stream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        WriteAscii(ms, "%PDF-1.4\n");
        WriteAscii(ms, "%\xe2\xe3\xcf\xd3\n");

        var offsets = new long[5];

        offsets[0] = ms.Position;
        WriteAscii(ms, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[1] = ms.Position;
        WriteAscii(ms, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[2] = ms.Position;
        WriteAscii(ms, $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)} {pageHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}] /Resources << /XObject << /Im1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        offsets[3] = ms.Position;
        WriteAscii(ms, $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {imageWidth} /Height {imageHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >>\nstream\n");
        ms.Write(jpegBytes, 0, jpegBytes.Length);
        WriteAscii(ms, "\nendstream\nendobj\n");

        offsets[4] = ms.Position;
        WriteAscii(ms, $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        ms.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(ms, "endstream\nendobj\n");

        var xrefOffset = ms.Position;
        WriteAscii(ms, "xref\n");
        WriteAscii(ms, "0 6\n");
        WriteAscii(ms, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(ms, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(ms, "trailer\n");
        WriteAscii(ms, "<< /Size 6 /Root 1 0 R >>\n");
        WriteAscii(ms, "startxref\n");
        WriteAscii(ms, $"{xrefOffset}\n");
        WriteAscii(ms, "%%EOF\n");

        return ms.ToArray();
    }

    private static IEnumerable<ReportLine> BuildReportLines(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        var salesList = sales.OrderBy(s => s.CreatedAt).ToList();

        yield return new(LineStyle.Separator);
        yield return new(LineStyle.TitleBold, storeName);
        yield return new(LineStyle.Centered, "X REPORT (DAILY READ)");
        yield return new(LineStyle.Centered, $"Date: {date:MM/dd/yyyy}");
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
        yield return new(LineStyle.TwoColumn, "Cash in Drawer", CurrencyDisplayHelper.FormatConfiguredAmount(totalCash));
        yield return new(LineStyle.TwoColumn, "Card Payments", CurrencyDisplayHelper.FormatConfiguredAmount(totalCard));
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.Empty);
        yield return new(LineStyle.Centered, "End of Report");
    }

    private static void DrawCentered(Graphics graphics, string text, Font font, Brush brush, ref float y, float left, float width)
    {
        var size = graphics.MeasureString(text, font, (int)width);
        var x = left + ((width - size.Width) / 2f);
        graphics.DrawString(text, font, brush, x, y);
        y += size.Height + 4f;
    }

    private static void DrawLeft(Graphics graphics, string text, Font font, Brush brush, ref float y, float left)
    {
        graphics.DrawString(text, font, brush, left, y);
        y += graphics.MeasureString(text, font).Height + 4f;
    }

    private static void DrawLeftRight(Graphics graphics, string leftText, string rightText, Font font, Brush brush, ref float y, float left, float right)
    {
        graphics.DrawString(leftText, font, brush, left, y);
        var rightSize = graphics.MeasureString(rightText, font);
        graphics.DrawString(rightText, font, brush, right - rightSize.Width, y);
        y += Math.Max(graphics.MeasureString(leftText, font).Height, rightSize.Height) + 4f;
    }

    private enum LineStyle { Normal, Separator, TitleBold, Bold, Centered, TwoColumn, TwoColumnBold, Empty }

    private sealed record ReportLine(LineStyle Style, string Text = "", string? RightText = null);
}
