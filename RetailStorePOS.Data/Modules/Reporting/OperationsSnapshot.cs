using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Persisted snapshot for the Operations (Pro) page: hour-of-week heatmap
/// cells + cashier performance rows for a given period.
/// </summary>
public sealed class OperationsSnapshot : ISignedSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string PeriodKey { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public DateTime PeriodStartUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public OperationsSnapshotPayload Payload { get; set; } = new();
}

public sealed class OperationsSnapshotPayload
{
    public List<HeatmapCellEntry> Heatmap { get; set; } = new();
    public List<CashierEntry> Cashiers { get; set; } = new();
}

public sealed class HeatmapCellEntry
{
    public int DayOfWeek { get; set; }
    public int Hour { get; set; }
    public int TransactionCount { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class CashierEntry
{
    public string CashierName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal AverageTicket { get; set; }
    public double UnitsSold { get; set; }
    public double ItemsPerTicket { get; set; }
}

public sealed class OperationsSnapshotStore : SignedSnapshotStore<OperationsSnapshot>
{
    public OperationsSnapshotStore(SnapshotSigningService signing) : base(signing) { }

    protected override string FilePrefix => "operations-";

    protected override int CurrentSchemaVersion => OperationsSnapshot.CurrentSchemaVersion;

    protected override JsonTypeInfo<OperationsSnapshot> SnapshotJsonTypeInfo =>
        ReportingSignedSnapshotJsonContext.Default.OperationsSnapshot;
}
