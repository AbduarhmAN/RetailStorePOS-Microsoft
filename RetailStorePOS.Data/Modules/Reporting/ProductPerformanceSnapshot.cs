using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Persisted snapshot for the Product Performance (Pro) page: per-product
/// stats over a period, used to populate the Top sellers / Top movers /
/// Slow movers / Dead stock tabs.
/// </summary>
public sealed class ProductPerformanceSnapshot : ISignedSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string PeriodKey { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public DateTime PeriodStartUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public ProductPerformanceSnapshotPayload Payload { get; set; } = new();
}

public sealed class ProductPerformanceSnapshotPayload
{
    public List<ProductPerformanceEntry> Rows { get; set; } = new();
}

public sealed class ProductPerformanceEntry
{
    public long ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public double UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPercent { get; set; }
    public DateTime? LastSaleAt { get; set; }
    public decimal CurrentStock { get; set; }
}

public sealed class ProductPerformanceSnapshotStore : SignedSnapshotStore<ProductPerformanceSnapshot>
{
    public ProductPerformanceSnapshotStore(SnapshotSigningService signing) : base(signing) { }

    protected override string FilePrefix => "product-performance-";

    protected override int CurrentSchemaVersion => ProductPerformanceSnapshot.CurrentSchemaVersion;

    protected override JsonTypeInfo<ProductPerformanceSnapshot> SnapshotJsonTypeInfo =>
        ReportingSignedSnapshotJsonContext.Default.ProductPerformanceSnapshot;
}
