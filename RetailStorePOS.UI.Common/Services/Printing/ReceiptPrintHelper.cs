using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using RetailStorePOS.UI.Common.Models;

namespace RetailStorePOS.UI.Common.Services.Printing;

/// <summary>
/// Prints a receipt silently and directly to the selected Windows printer, or the default printer when none is selected.
/// Uses a professional 2-column layout (Description + Amount) matching industry-standard POS receipts.
/// Supports multi-page pagination for long receipts.
/// </summary>
public sealed class ReceiptPrintHelper : IDisposable
{
    private ReceiptSummary? _receipt;
    private string? _storeName;
    private string? _storeAddress;
    private string? _currencyCode;
    private string? _preferredPrinterName;

    // Pagination state
    private enum PrintPhase { Header, Items, Totals, Done }
    private PrintPhase _phase = PrintPhase.Header;
    private int _currentItemIndex = 0;

    public Task PrintReceiptAsync(
        IntPtr hwnd,
        Microsoft.UI.Xaml.Controls.Panel printContainer,
        ReceiptSummary receipt,
        string storeName,
        string? storeAddress,
        string currencyCode,
        string? preferredPrinterName = null)
    {
        _receipt = receipt;
        _storeName = storeName;
        _storeAddress = storeAddress;
        _currencyCode = currencyCode;
        _preferredPrinterName = preferredPrinterName;

        if (_receipt is null || _receipt.Items.Count == 0)
        {
            return Task.CompletedTask;
        }

        _phase = PrintPhase.Header;
        _currentItemIndex = 0;

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrintToFile = false;
            ApplyPreferredPrinter(printDoc, _preferredPrinterName);
            printDoc.PrintPage += PrintDoc_PrintPage;
            printDoc.Print();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Silent Print Error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        if (e.Graphics is null || _receipt is null) return;

        var g = e.Graphics;

        // Disable anti-aliasing for crisp thermal output
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

        var bounds = e.MarginBounds;
        int left = bounds.Left;
        int right = bounds.Right - 4; // small safety margin so text doesn't clip at the paper edge
        int printWidth = right - left;
        float pageBottom = bounds.Bottom;

        float y = bounds.Top;

        // Scale fonts relative to printable width
        float scale = printWidth / 576f;
        float titleSize = Math.Max(14f * scale, 9f);
        float normalSize = Math.Max(10f * scale, 7f);
        float boldSize = Math.Max(12f * scale, 8f);

        using var fontTitle = new Font("Courier New", titleSize, FontStyle.Bold);
        using var fontNormal = new Font("Courier New", normalSize, FontStyle.Regular);
        using var fontBold = new Font("Courier New", normalSize, FontStyle.Bold);
        using var fontBigBold = new Font("Courier New", boldSize, FontStyle.Bold);

        float lineHeight = g.MeasureString("X", fontNormal).Height + 2;
        float safeBottom = pageBottom - lineHeight * 2;

        // ──────────────────────────────────────────
        // PHASE: Header (page 1 only)
        // ──────────────────────────────────────────
        if (_phase == PrintPhase.Header)
        {
            DrawLine(g, ref y, left, right);

            // Store Name — centered
            DrawCentered(g, _storeName ?? "Store", fontTitle, ref y, left, printWidth);

            // Store Address — centered
            if (!string.IsNullOrWhiteSpace(_storeAddress))
            {
                DrawCentered(g, _storeAddress, fontNormal, ref y, left, printWidth);
            }

            DrawLine(g, ref y, left, right);

            // Receipt # and Cashier
            DrawLeftRight(g, ref y, $"Slip: #{_receipt.ReceiptNumber:D6}", $"Sale: {_receipt.SaleId}", fontNormal, left, right);
            DrawLeft(g, ref y, $"Staff: {_receipt.CashierName}", fontNormal, left);
            DrawLeft(g, ref y,
                $"Date: {_receipt.CreatedAt.ToLocalTime().ToString("MM/dd/yy  h:mm tt", CultureInfo.InvariantCulture)}",
                fontNormal, left);

            DrawLine(g, ref y, left, right);
            y += 2;

            // Column header: Description | Qty | Amount
            Draw3Columns(g, ref y, "Description", "Qty", "Amount", fontBold, left, printWidth);
            DrawDottedLine(g, ref y, left, right);

            _phase = PrintPhase.Items;
        }

        // ──────────────────────────────────────────
        // PHASE: Line Items (multi-page safe)
        // ──────────────────────────────────────────
        if (_phase == PrintPhase.Items)
        {
            while (_currentItemIndex < _receipt.Items.Count)
            {
                if (y + lineHeight > safeBottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                var item = _receipt.Items[_currentItemIndex];

                string desc = Truncate(item.Name, 24);
                string qty = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                string amount = FormatMoney(item.LineTotal, _currencyCode);

                Draw3Columns(g, ref y, desc, qty, amount, fontNormal, left, printWidth);

                _currentItemIndex++;
            }

            _phase = PrintPhase.Totals;
        }

        // ──────────────────────────────────────────
        // PHASE: Totals & Footer
        // ──────────────────────────────────────────
        if (_phase == PrintPhase.Totals)
        {
            float totalsHeight = lineHeight * 10;
            if (y + totalsHeight > safeBottom)
            {
                e.HasMorePages = true;
                return;
            }

            DrawLine(g, ref y, left, right);

            DrawLeftRight(g, ref y, "Subtotal", FormatMoney(_receipt.Subtotal, _currencyCode), fontNormal, left, right);
            DrawLeftRight(g, ref y, "Tax", FormatMoney(_receipt.Tax, _currencyCode), fontNormal, left, right);

            y += lineHeight / 2f;
            DrawLeftRight(g, ref y, "Total " + CurrencyDisplayHelper.ResolveDisplayCode(_currencyCode), FormatMoney(_receipt.Total, _currencyCode), fontBigBold, left, right);
            y += lineHeight / 2f;

            DrawLine(g, ref y, left, right);

            DrawLeftRight(g, ref y, "Tendered", FormatMoney(_receipt.Tendered, _currencyCode), fontNormal, left, right);
            DrawLeftRight(g, ref y, "Change", FormatMoney(_receipt.Change, _currencyCode), fontBigBold, left, right);

            DrawLine(g, ref y, left, right);

            // Footer
            y += lineHeight;
            DrawCentered(g, "Welcome again", fontNormal, ref y, left, printWidth);

            // Cutter clearance
            y += 60;

            _phase = PrintPhase.Done;
        }

        e.HasMorePages = false;
    }

    private static void ApplyPreferredPrinter(PrintDocument printDoc, string? preferredPrinterName)
    {
        if (string.IsNullOrWhiteSpace(preferredPrinterName))
        {
            return;
        }

        if (!PrinterSelectionHelper.IsInstalledPrinter(preferredPrinterName))
        {
            return;
        }

        printDoc.PrinterSettings.PrinterName = preferredPrinterName.Trim();
    }

    // ──── Drawing Helpers ────

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

    private static void Draw3Columns(Graphics g, ref float y, string col1, string col2, string col3, Font font, int left, int printWidth)
    {
        float lineHeight = g.MeasureString("X", font).Height;
        int right = left + printWidth;

        // Column layout: Description 50% | Qty 12% | Amount 38%
        int col1W = (int)(printWidth * 0.50f);
        int col2W = (int)(printWidth * 0.12f);

        int col2X = left + col1W;
        int col3X = col2X + col2W;

        // Description — left aligned
        g.DrawString(col1, font, Brushes.Black, left, y);

        // Qty — center aligned in its zone
        var qtySize = g.MeasureString(col2, font);
        g.DrawString(col2, font, Brushes.Black, col2X + (col2W - qtySize.Width) / 2f, y);

        // Amount — right aligned to the paper edge
        var amtSize = g.MeasureString(col3, font);
        g.DrawString(col3, font, Brushes.Black, right - amtSize.Width, y);

        y += lineHeight + 2;
    }

    private static void DrawLine(Graphics g, ref float y, int x1, int x2)
    {
        y += 4;
        g.DrawLine(Pens.Black, x1, y, x2, y);
        y += 6;
    }

    private static void DrawDottedLine(Graphics g, ref float y, int x1, int x2)
    {
        y += 2;
        using var pen = new Pen(Color.Gray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        g.DrawLine(pen, x1, y, x2, y);
        y += 4;
    }

    private static string FormatMoney(decimal amount, string? currencyCode)
    {
        return CurrencyDisplayHelper.FormatAmount(amount, currencyCode);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 3 ? value[..maxLength] : value[..(maxLength - 3)] + "...";
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}
