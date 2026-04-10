namespace RetailStorePOS.Data.Models;

public sealed class ProductImportProgress
{
    public int TotalRows { get; init; }
    public int ProcessedRows { get; init; }
    public int CreatedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }

    public int ImportedCount => CreatedCount + UpdatedCount;

    public double ProgressPercent => TotalRows <= 0
        ? 0
        : Math.Min(100d, ProcessedRows * 100d / TotalRows);
}
