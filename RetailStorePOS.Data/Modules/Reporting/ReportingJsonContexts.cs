using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetailStorePOS.Data.Modules.Reporting;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(DashboardSnapshot), TypeInfoPropertyName = nameof(DashboardSnapshot))]
[JsonSerializable(typeof(DashboardMetricsPayload), TypeInfoPropertyName = nameof(DashboardMetricsPayload))]
[JsonSerializable(typeof(TopProductEntry), TypeInfoPropertyName = nameof(TopProductEntry))]
[JsonSerializable(typeof(SparklineEntry), TypeInfoPropertyName = nameof(SparklineEntry))]
[JsonSerializable(typeof(InventorySummary), TypeInfoPropertyName = nameof(InventorySummary))]
[JsonSerializable(typeof(ReadinessReportData), TypeInfoPropertyName = nameof(ReadinessReportData))]
[JsonSerializable(typeof(ReadinessRun), TypeInfoPropertyName = nameof(ReadinessRun))]
[JsonSerializable(typeof(ScenarioResult), TypeInfoPropertyName = nameof(ScenarioResult))]
[JsonSerializable(typeof(List<ScenarioResult>), TypeInfoPropertyName = "ScenarioResultList")]
internal sealed partial class ReportingFileJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AdvancedReportsSnapshot), TypeInfoPropertyName = nameof(AdvancedReportsSnapshot))]
[JsonSerializable(typeof(AdvancedReportsSnapshotPayload), TypeInfoPropertyName = nameof(AdvancedReportsSnapshotPayload))]
[JsonSerializable(typeof(DailyRevenueEntry), TypeInfoPropertyName = nameof(DailyRevenueEntry))]
[JsonSerializable(typeof(TopProductSnapshotEntry), TypeInfoPropertyName = nameof(TopProductSnapshotEntry))]
[JsonSerializable(typeof(OperationsSnapshot), TypeInfoPropertyName = nameof(OperationsSnapshot))]
[JsonSerializable(typeof(OperationsSnapshotPayload), TypeInfoPropertyName = nameof(OperationsSnapshotPayload))]
[JsonSerializable(typeof(HeatmapCellEntry), TypeInfoPropertyName = nameof(HeatmapCellEntry))]
[JsonSerializable(typeof(CashierEntry), TypeInfoPropertyName = nameof(CashierEntry))]
[JsonSerializable(typeof(ProductPerformanceSnapshot), TypeInfoPropertyName = nameof(ProductPerformanceSnapshot))]
[JsonSerializable(typeof(ProductPerformanceSnapshotPayload), TypeInfoPropertyName = nameof(ProductPerformanceSnapshotPayload))]
[JsonSerializable(typeof(ProductPerformanceEntry), TypeInfoPropertyName = nameof(ProductPerformanceEntry))]
internal sealed partial class ReportingSignedSnapshotJsonContext : JsonSerializerContext
{
}
