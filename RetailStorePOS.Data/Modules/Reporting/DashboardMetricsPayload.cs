namespace RetailStorePOS.Data.Modules.Reporting;

using System.Collections.Generic;

public class DashboardMetricsPayload
{
    public decimal TodayRevenue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public decimal TodayProfit { get; set; }
    public int TodayInvoiceCount { get; set; }
    public int YesterdayInvoiceCount { get; set; }
    public decimal TodayMedianInvoice { get; set; }
    public decimal DailyTarget { get; set; }
    public decimal TodayDiscountAmount { get; set; }
    public int TodayDiscountCount { get; set; }
    public List<TopProductEntry> TopProducts { get; set; } = new();
    public List<SparklineEntry> DailySparklines { get; set; } = new();
    public InventorySummary Inventory { get; set; } = new();
}

public class TopProductEntry 
{ 
    public string Name { get; set; } = string.Empty; 
    public decimal Revenue { get; set; } 
}

public class SparklineEntry 
{ 
    public string LocalDay { get; set; } = string.Empty; 
    public decimal Revenue { get; set; } 
    public decimal Profit { get; set; } 
    public int InvoiceCount { get; set; } 
}

public class InventorySummary 
{ 
    public int ShelfLow { get; set; } 
    public int WarehouseLow { get; set; } 
    public int ShelfEmpty { get; set; } 
    public int WarehouseEmpty { get; set; }
    public int TotalProducts { get; set; }
}
