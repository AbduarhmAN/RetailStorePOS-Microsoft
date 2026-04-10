using System.Collections.ObjectModel;

namespace RetailStorePOS.WinUiLogin.Models;

public sealed class ReceiptSummary
{
    public long SaleId { get; set; }

    public long ReceiptNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public decimal Tendered { get; set; }

    public decimal Change { get; set; }

    public string? PdfPath { get; set; }

    public ObservableCollection<ReceiptLineItem> Items { get; } = new();
}


