using System;
using System.Threading.Tasks;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Models;
using RetailStorePOS.UI.Common.Services;
using global::RetailStorePOS.Data;
using global::RetailStorePOS.Data.Models;
using global::RetailStorePOS.Data.Modules.Products;
using global::RetailStorePOS.Data.Modules.Sales;
using global::RetailStorePOS.Data.Modules.Settings;
using global::RetailStorePOS.Data.Modules.Tax;
using global::RetailStorePOS.Data.Modules.UsersAuth;
using global::RetailStorePOS.Data.Modules.Reporting;
using global::RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.WinUiLogin;

public static class LoginRuntime
{
    public static string DatabasePath => RetailStorePOS.UI.Common.LoginRuntime.DatabasePath;
    public static SqliteConnectionFactory ConnectionFactory => RetailStorePOS.UI.Common.LoginRuntime.ConnectionFactory;
    public static ILocalPreferencesService LocalPreferences => RetailStorePOS.UI.Common.LoginRuntime.LocalPreferences;
    public static TelemetryService Telemetry => RetailStorePOS.UI.Common.LoginRuntime.Telemetry;
    public static ProductSearchService ProductSearch => RetailStorePOS.UI.Common.LoginRuntime.ProductSearch;
    public static UserRepository Users => RetailStorePOS.UI.Common.LoginRuntime.Users;
    public static AuditLogRepository AuditLogs => RetailStorePOS.UI.Common.LoginRuntime.AuditLogs;
    public static AuditLogService Audit => RetailStorePOS.UI.Common.LoginRuntime.Audit;
    public static AuthService Auth => RetailStorePOS.UI.Common.LoginRuntime.Auth;
    public static ProductRepository Products => RetailStorePOS.UI.Common.LoginRuntime.Products;
    public static SaleRepository Sales => RetailStorePOS.UI.Common.LoginRuntime.Sales;
    public static AuthorizedAdvancedReportsService AdvancedReports => RetailStorePOS.UI.Common.LoginRuntime.AdvancedReports;
    public static XReportRepository XReports => RetailStorePOS.UI.Common.LoginRuntime.XReports;
    public static AuthorizationGuard Authorization => RetailStorePOS.UI.Common.LoginRuntime.Authorization;
    public static SnapshotSigningService SnapshotSigning => RetailStorePOS.UI.Common.LoginRuntime.SnapshotSigning;
    public static AdvancedReportsSnapshotStore AdvancedReportsSnapshots => RetailStorePOS.UI.Common.LoginRuntime.AdvancedReportsSnapshots;
    public static OperationsSnapshotStore OperationsSnapshots => RetailStorePOS.UI.Common.LoginRuntime.OperationsSnapshots;
    public static ProductPerformanceSnapshotStore ProductPerformanceSnapshots => RetailStorePOS.UI.Common.LoginRuntime.ProductPerformanceSnapshots;
    public static SettingsRepository Settings => RetailStorePOS.UI.Common.LoginRuntime.Settings;
    public static DatabaseBackupService DatabaseBackups => RetailStorePOS.UI.Common.LoginRuntime.DatabaseBackups;
    public static BootstrapAdminCredentials BootstrapCredentials => RetailStorePOS.UI.Common.LoginRuntime.BootstrapCredentials;
    public static LicenseValidationService License => RetailStorePOS.UI.Common.LoginRuntime.License;
    public static FeatureAccessService FeatureAccess => RetailStorePOS.UI.Common.LoginRuntime.FeatureAccess;
    public static TaxCategoryRepository TaxCategories => RetailStorePOS.UI.Common.LoginRuntime.TaxCategories;
    public static TaxAuthorityRepository TaxAuthorities => RetailStorePOS.UI.Common.LoginRuntime.TaxAuthorities;
    public static TaxRuleRepository TaxRules => RetailStorePOS.UI.Common.LoginRuntime.TaxRules;
    public static TaxGroupRepository TaxGroups => RetailStorePOS.UI.Common.LoginRuntime.TaxGroups;
    public static RegisterSessionRepository RegisterSessions => RetailStorePOS.UI.Common.LoginRuntime.RegisterSessions;
    public static global::RetailStorePOS.UI.Common.Services.ReadinessService Readiness => RetailStorePOS.UI.Common.LoginRuntime.Readiness;

    public static BootstrapAdminHint? PendingBootstrapAdminHint
    {
        get
        {
            var hint = RetailStorePOS.UI.Common.LoginRuntime.PendingBootstrapAdminHint;
            return hint == null ? null : new BootstrapAdminHint(hint.Username, hint.Password, hint.Pin);
        }
    }

    public static bool IsFreshStartResetRequested => RetailStorePOS.UI.Common.LoginRuntime.IsFreshStartResetRequested;
    public static bool WasRapidReopenAfterUncleanExit => RetailStorePOS.UI.Common.LoginRuntime.WasRapidReopenAfterUncleanExit;
    public static bool IsOnboardingPhaseCleared => RetailStorePOS.UI.Common.LoginRuntime.IsOnboardingPhaseCleared;
    public static bool IsBootstrapPasswordChangeStillRequired => RetailStorePOS.UI.Common.LoginRuntime.IsBootstrapPasswordChangeStillRequired;
    public static string SessionDebugInfo => RetailStorePOS.UI.Common.LoginRuntime.SessionDebugInfo;

    public static event EventHandler? ProductsUpdated
    {
        add => RetailStorePOS.UI.Common.LoginRuntime.ProductsUpdated += value;
        remove => RetailStorePOS.UI.Common.LoginRuntime.ProductsUpdated -= value;
    }

    public static void Initialize() => RetailStorePOS.UI.Common.LoginRuntime.Initialize();
    public static void ApplyPersistedLanguageSetting() => RetailStorePOS.UI.Common.LoginRuntime.ApplyPersistedLanguageSetting();
    
    public static BootstrapAdminHint? ConsumeBootstrapAdminHint()
    {
        var hint = RetailStorePOS.UI.Common.LoginRuntime.ConsumeBootstrapAdminHint();
        return hint == null ? null : new BootstrapAdminHint(hint.Username, hint.Password, hint.Pin);
    }

    public static BootstrapAdminHint? GetBootstrapAdminHintForDisplay()
    {
        var hint = RetailStorePOS.UI.Common.LoginRuntime.GetBootstrapAdminHintForDisplay();
        return hint == null ? null : new BootstrapAdminHint(hint.Username, hint.Password, hint.Pin);
    }

    public static void ReportException(Exception exception, string operationName) => RetailStorePOS.UI.Common.LoginRuntime.ReportException(exception, operationName);
    public static void RaiseProductsUpdated() => RetailStorePOS.UI.Common.LoginRuntime.RaiseProductsUpdated();
    public static Task WarmProductSearchIndexAsync() => RetailStorePOS.UI.Common.LoginRuntime.WarmProductSearchIndexAsync();
    public static void CompleteBootstrapAdminPasswordChange() => RetailStorePOS.UI.Common.LoginRuntime.CompleteBootstrapAdminPasswordChange();
    public static Task ResetFreshStartAsync() => RetailStorePOS.UI.Common.LoginRuntime.ResetFreshStartAsync();
    public static void CancelFreshStartReset() => RetailStorePOS.UI.Common.LoginRuntime.CancelFreshStartReset();
}
