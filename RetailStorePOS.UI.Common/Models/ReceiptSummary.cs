using System.Collections.ObjectModel;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Common.Models;

public sealed partial class ReceiptSummary : ObservableObject
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


