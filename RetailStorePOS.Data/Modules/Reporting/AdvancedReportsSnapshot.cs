using System;
using System.Collections.Generic;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Persisted snapshot for the Advanced Analytics Revenue Dashboard.
///
/// Lifecycle:
///   - Producer (background worker / fallback live-query) writes via
///     <see cref="AdvancedReportsSnapshotStore.SaveSnapshot"/>.
///   - Consumer (RevenueDashboardPage) reads via
///     <see cref="AdvancedReportsSnapshotStore.LoadSnapshot"/>.
///   - Signature is verified on load; tampered or stale files are treated
///     as missing, triggering a fresh live query.
///
/// Versioning: the <see cref="SchemaVersion"/> field lets newer app builds
/// reject snapshots produced by older builds with incompatible payloads.
/// </summary>
public sealed class AdvancedReportsSnapshot : ISignedSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Identifier of the period the snapshot represents (e.g. "Last7Days").</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the snapshot was captured.</summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Window this snapshot covers - inclusive UTC start.</summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>Window this snapshot covers - exclusive UTC end.</summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>The actual computed metrics.</summary>
    public AdvancedReportsSnapshotPayload Payload { get; set; } = new();
}

/// <summary>
/// Pure data payload for the Pro Revenue Dashboard.
/// </summary>
public sealed class AdvancedReportsSnapshotPayload
{
    // KPI tiles
    public decimal TotalRevenue { get; set; }
    public decimal GrossProfit { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public decimal ProfitMarginPercent { get; set; }

    // Comparison tile (previous-period equivalents)
    public decimal PreviousRevenue { get; set; }
    public decimal PreviousProfit { get; set; }
    public int PreviousTransactionCount { get; set; }
    public decimal PreviousAverageTransactionValue { get; set; }

    // Trend chart
    public List<DailyRevenueEntry> DailyTrend { get; set; } = new();

    // Top products
    public List<TopProductSnapshotEntry> TopProducts { get; set; } = new();

    // Busiest hour / day
    public int? BusiestHour { get; set; }
    public int BusiestHourTransactionCount { get; set; }
    public int? BusiestDayOfWeek { get; set; }
    public int BusiestDayOfWeekTransactionCount { get; set; }
}

public sealed class DailyRevenueEntry
{
    public string Day { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public int InvoiceCount { get; set; }
}

public sealed class TopProductSnapshotEntry
{
    public long ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public double Units { get; set; }
    public decimal Profit { get; set; }
}
