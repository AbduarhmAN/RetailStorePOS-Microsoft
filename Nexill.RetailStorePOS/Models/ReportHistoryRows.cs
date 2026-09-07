using Microsoft.UI.Xaml;

namespace RetailStorePOS.WinUiLogin.Models;

public sealed partial class RegisterReportCashierSummaryRow
{
    public string CashierName { get; init; } = string.Empty;
    public string SaleCountText { get; init; } = string.Empty;
    public string GrossSalesText { get; init; } = string.Empty;
    public string BreakdownText { get; init; } = string.Empty;
}

public sealed partial class RegisterSessionHistoryRow
{
    public long SessionId { get; init; }
    public string TitleText { get; init; } = string.Empty;
    public string SubtitleText { get; init; } = string.Empty;
    public string MetaText { get; init; } = string.Empty;
    public string GrossSalesText { get; init; } = string.Empty;
    public string StateText { get; init; } = string.Empty;
}

public sealed partial class XReportHistoryRow
{
    public long RunId { get; init; }
    public string TitleText { get; init; } = string.Empty;
    public string SubtitleText { get; init; } = string.Empty;
    public string MetaText { get; init; } = string.Empty;
}

public sealed partial class RegisterReportCashierIntervalRow
{
    public string CashierName { get; init; } = string.Empty;
    public string TimeRangeText { get; init; } = string.Empty;
    public string NoteText { get; init; } = string.Empty;
    public Visibility NoteVisibility { get; init; }
}

public sealed partial class RegisterReportAdjustmentRow
{
    public string Reason { get; init; } = string.Empty;
    public string DetailText { get; init; } = string.Empty;
    public string AmountText { get; init; } = string.Empty;
}
