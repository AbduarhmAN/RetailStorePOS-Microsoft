using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Linq;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.WinUiLogin.Common;

/// <summary>
/// Prints an X Report silently and directly to the default Windows printer using System.Drawing.
/// Uses the same receipt-width Courier layout as the PDF generator but bypasses PDF entirely.
/// Mirrors the ReceiptPrintHelper pattern for consistent thermal/desktop printing.
/// </summary>
public sealed class XReportPrintHelper : IDisposable
{
    private const int ReceiptWidth = 48; // chars per line — matches XReportHelper
    private const string Separator = "------------------------------------------------";

    private List<ReportLine> _lines = new();
    private int _currentLineIndex;

    /// <summary>
    /// Prints the X Report directly to the default printer — no PDF, no dialog.
    /// </summary>
    public void PrintXReport(IEnumerable<Sale> sales, DateTime date, string storeName)
    {
        _lines = BuildReportLines(sales, date, storeName).ToList();
        _currentLineIndex = 0;

        if (_lines.Count == 0) return;

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.DocumentName = $"XReport_{date:yyyyMMdd}";
            printDoc.PrinterSettings.PrintToFile = false;
            printDoc.PrintPage += PrintDoc_PrintPage;
            printDoc.Print();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"XReport Silent Print Error: {ex.Message}");
        }
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        if (e.Graphics is null) return;

        var g = e.Graphics;

        // Disable anti-aliasing for crisp thermal output
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

        var bounds = e.MarginBounds;
        int left = bounds.Left;
        int right = bounds.Right - 4;
        int printWidth = right - left;
        float pageBottom = bounds.Bottom;

        float y = bounds.Top;

        // Scale fonts relative to printable width
        float scale = printWidth / 576f;
        float titleSize = Math.Max(14f * scale, 9f);
        float normalSize = Math.Max(10f * scale, 7f);

        using var fontNormal = new Font("Segoe UI", normalSize, FontStyle.Regular);
        using var fontBold = new Font("Segoe UI", normalSize, FontStyle.Bold);
        using var fontTitle = new Font("Segoe UI", titleSize, FontStyle.Bold);

        float lineHeight = g.MeasureString("X", fontNormal).Height + 2;
        float safeBottom = pageBottom - lineHeight * 2;

        while (_currentLineIndex < _lines.Count)
        {
            if (y + lineHeight > safeBottom)
            {
                e.HasMorePages = true;
                return;
            }

            var line = _lines[_currentLineIndex];

            switch (line.Style)
            {
                case LineStyle.Separator:
                    DrawLine(g, ref y, left, right);
                    break;

                case LineStyle.TitleBold:
                    DrawCentered(g, line.Text, fontTitle, ref y, left, printWidth);
                    break;

                case LineStyle.Bold:
                    DrawLeft(g, ref y, line.Text, fontBold, left);
                    break;

                case LineStyle.Centered:
                    DrawCentered(g, line.Text, fontNormal, ref y, left, printWidth);
                    break;

                case LineStyle.TwoColumn:
                    DrawLeftRight(g, ref y, line.Text, line.RightText ?? "", fontNormal, left, right);
                    break;

                case LineStyle.TwoColumnBold:
                    DrawLeftRight(g, ref y, line.Text, line.RightText ?? "", fontBold, left, right);
                    break;

                case LineStyle.Empty:
                    y += lineHeight;
                    break;

                default:
                    DrawLeft(g, ref y, line.Text, fontNormal, left);
                    break;
            }

            _currentLineIndex++;
        }

        e.HasMorePages = false;
    }

    // ──── Report Content Builder ────

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
        decimal totalCash = 0;

        foreach (var sale in salesList)
        {
            yield return new(LineStyle.TwoColumn, sale.ReceiptNumber.ToString("D6"),
                CurrencyDisplayHelper.FormatConfiguredAmount(sale.Total));

            decimal saleGross = 0;
            foreach (var item in sale.Items)
            {
                saleGross += (item.Price * item.Quantity);
            }
            if (saleGross < sale.Subtotal) saleGross = sale.Subtotal;

            totalGross += saleGross;
            totalNet += sale.Subtotal;

            if (sale.PaymentType != null && sale.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                totalCash += sale.Total;
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
        yield return new(LineStyle.TwoColumn, "Total Gross Sales", CurrencyDisplayHelper.FormatConfiguredAmount(totalGross));
        yield return new(LineStyle.TwoColumn, "Total Net Sales", CurrencyDisplayHelper.FormatConfiguredAmount(totalNet));
        yield return new(LineStyle.TwoColumn, "Total Cash in Drawer", CurrencyDisplayHelper.FormatConfiguredAmount(totalCash));
        yield return new(LineStyle.Separator);
        yield return new(LineStyle.Empty);
        yield return new(LineStyle.Centered, "End of Report");
    }

    // ──── Drawing Helpers (matching ReceiptPrintHelper) ────

    private static void DrawCentered(Graphics g, string text, Font font, ref float y, int left, int width)
    {
        var size = g.MeasureString(text, font, width);
        float x = left + (width - size.Width) / 2f;
        g.DrawString(text, font, Brushes.Black, x, y);
        y += size.Height + 1;
    }

    private static void DrawLeft(Graphics g, ref float y, string text, Font font, int left)
    {
        float lineHeight = g.MeasureString("X", font).Height;
        g.DrawString(text, font, Brushes.Black, left, y);
        y += lineHeight + 2;
    }

    private static void DrawLeftRight(Graphics g, ref float y, string leftText, string rightText, Font font, int left, int right)
    {
        float lineHeight = g.MeasureString("X", font).Height;
        g.DrawString(leftText, font, Brushes.Black, left, y);
        var rightSize = g.MeasureString(rightText, font);
        g.DrawString(rightText, font, Brushes.Black, right - rightSize.Width, y);
        y += lineHeight + 2;
    }

    private static void DrawLine(Graphics g, ref float y, int x1, int x2)
    {
        y += 4;
        g.DrawLine(Pens.Black, x1, y, x2, y);
        y += 6;
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }

    // ──── Internal types ────

    private enum LineStyle { Normal, Separator, TitleBold, Bold, Centered, TwoColumn, TwoColumnBold, Empty }

    private sealed record ReportLine(LineStyle Style, string Text = "", string? RightText = null);
}
