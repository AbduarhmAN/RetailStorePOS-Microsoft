using System;
using System.Collections.Generic;
using RetailStorePOS.App.Services.Licensing;
using RetailStorePOS.Data.Modules.Reporting;

namespace RetailStorePOS.App.Services;

/// <summary>
/// Authorization-aware wrapper around <see cref="AdvancedReportsQueryService"/>.
/// Every public method calls <see cref="AuthorizationGuard.RequireFeature"/>
/// with <see cref="FeatureAccessService.Features.AdvancedReports"/> before
/// delegating, so a caller who has not signed in, has been locked out, or
/// holds a license without AdvancedReports gets an
/// <see cref="UnauthorizedAccessException"/> instead of fresh data.
///
/// This is the deny-by-default "service-layer authorization" line described in
/// Application Gaps 9.4. The previous build relied entirely on UI visibility
/// to gate paid-feature data, which is bypassable by a patched binary or any
/// future programmatic caller. The wrapper closes that hole at the seam where
/// data leaves the data layer.
/// </summary>
public sealed class AuthorizedAdvancedReportsService
{
    private readonly AdvancedReportsQueryService _query;
    private readonly AuthorizationGuard _guard;

    public AuthorizedAdvancedReportsService(
        AdvancedReportsQueryService query,
        AuthorizationGuard guard)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetRevenueByDay" />
    public List<DailyRevenuePoint> GetRevenueByDay(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetRevenueByDay(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetTopProducts" />
    public List<TopProductRow> GetTopProducts(DateTime startUtc, DateTime endUtc, int limit = 5)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetTopProducts(startUtc, endUtc, limit);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetBusiestHourAndDay" />
    public BusiestPeriodSnapshot GetBusiestHourAndDay(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetBusiestHourAndDay(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetKpiSnapshot" />
    public KpiSnapshot GetKpiSnapshot(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetKpiSnapshot(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetProductPerformance" />
    public List<ProductPerformanceRow> GetProductPerformance(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetProductPerformance(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetHourDayHeatmap" />
    public List<HourDayCell> GetHourDayHeatmap(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetHourDayHeatmap(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetCashierPerformance" />
    public List<CashierPerformanceRow> GetCashierPerformance(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetCashierPerformance(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetAbcXyzClassification" />
    public List<AbcXyzRow> GetAbcXyzClassification(DateTime startUtc, DateTime endUtc)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetAbcXyzClassification(startUtc, endUtc);
    }

    /// <inheritdoc cref="AdvancedReportsQueryService.GetBasketAffinity" />
    public List<BasketAffinityRow> GetBasketAffinity(
        DateTime startUtc, DateTime endUtc, int minPairCount = 2, int topN = 100)
    {
        _guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
        return _query.GetBasketAffinity(startUtc, endUtc, minPairCount, topN);
    }
}
