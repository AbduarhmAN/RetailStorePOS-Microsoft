using System;
using System.Threading.Tasks;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Models;
using RetailStorePOS.UI.Common.Services;

namespace TestRunner.Reporting.App;

public static class LoginRuntime
{
    public static string DatabasePath => RetailStorePOS.UI.Common.LoginRuntime.DatabasePath;
    public static global::RetailStorePOS.Data.SqliteConnectionFactory ConnectionFactory => RetailStorePOS.UI.Common.LoginRuntime.ConnectionFactory;
    public static ILocalPreferencesService LocalPreferences => RetailStorePOS.UI.Common.LoginRuntime.LocalPreferences;
    public static TelemetryService Telemetry => RetailStorePOS.UI.Common.LoginRuntime.Telemetry;
    public static ProductSearchService ProductSearch => RetailStorePOS.UI.Common.LoginRuntime.ProductSearch;
    public static global::RetailStorePOS.Data.Modules.UsersAuth.UserRepository Users => RetailStorePOS.UI.Common.LoginRuntime.Users;
    public static global::RetailStorePOS.Data.Modules.Settings.SettingsRepository Settings => RetailStorePOS.UI.Common.LoginRuntime.Settings;
    public static global::RetailStorePOS.Data.Repositories.AuditLogRepository AuditLogs => RetailStorePOS.UI.Common.LoginRuntime.AuditLogs;
    public static global::RetailStorePOS.UI.Common.Services.AuditLogService Audit => RetailStorePOS.UI.Common.LoginRuntime.Audit;
    public static global::RetailStorePOS.Data.Modules.Products.ProductRepository Products => RetailStorePOS.UI.Common.LoginRuntime.Products;
    public static global::RetailStorePOS.Data.Modules.Sales.SaleRepository Sales => RetailStorePOS.UI.Common.LoginRuntime.Sales;
    public static global::RetailStorePOS.UI.Common.Services.AuthorizedAdvancedReportsService AdvancedReports => RetailStorePOS.UI.Common.LoginRuntime.AdvancedReports;
    public static global::RetailStorePOS.Data.Modules.Reporting.XReportRepository XReports => RetailStorePOS.UI.Common.LoginRuntime.XReports;

    public static AuthorizationGuard Authorization => RetailStorePOS.UI.Common.LoginRuntime.Authorization;
    public static global::RetailStorePOS.Data.Modules.Reporting.SnapshotSigningService SnapshotSigning => RetailStorePOS.UI.Common.LoginRuntime.SnapshotSigning;
    public static global::RetailStorePOS.Data.Modules.Reporting.AdvancedReportsSnapshotStore AdvancedReportsSnapshots => RetailStorePOS.UI.Common.LoginRuntime.AdvancedReportsSnapshots;
    public static global::RetailStorePOS.Data.Modules.Reporting.OperationsSnapshotStore OperationsSnapshots => RetailStorePOS.UI.Common.LoginRuntime.OperationsSnapshots;
    public static global::RetailStorePOS.Data.Modules.Reporting.ProductPerformanceSnapshotStore ProductPerformanceSnapshots => RetailStorePOS.UI.Common.LoginRuntime.ProductPerformanceSnapshots;
    public static global::RetailStorePOS.Data.DatabaseBackupService DatabaseBackups => RetailStorePOS.UI.Common.LoginRuntime.DatabaseBackups;
    public static BootstrapAdminCredentials BootstrapCredentials => RetailStorePOS.UI.Common.LoginRuntime.BootstrapCredentials;
    public static global::RetailStorePOS.Data.Modules.Tax.TaxCategoryRepository TaxCategories => RetailStorePOS.UI.Common.LoginRuntime.TaxCategories;
    public static global::RetailStorePOS.Data.Modules.Tax.TaxAuthorityRepository TaxAuthorities => RetailStorePOS.UI.Common.LoginRuntime.TaxAuthorities;
    public static global::RetailStorePOS.Data.Modules.Tax.TaxRuleRepository TaxRules => RetailStorePOS.UI.Common.LoginRuntime.TaxRules;
    public static global::RetailStorePOS.Data.Modules.Tax.TaxGroupRepository TaxGroups => RetailStorePOS.UI.Common.LoginRuntime.TaxGroups;
    public static global::RetailStorePOS.Data.Modules.Sales.RegisterSessionRepository RegisterSessions => RetailStorePOS.UI.Common.LoginRuntime.RegisterSessions;
    
    public static bool IsFreshStartResetRequested => RetailStorePOS.UI.Common.LoginRuntime.IsFreshStartResetRequested;
    public static bool WasRapidReopenAfterUncleanExit => RetailStorePOS.UI.Common.LoginRuntime.WasRapidReopenAfterUncleanExit;
    
    public static global::RetailStorePOS.UI.Common.Services.ReadinessService Readiness => RetailStorePOS.UI.Common.LoginRuntime.Readiness;
    public static LicenseValidationService License => RetailStorePOS.UI.Common.LoginRuntime.License;
    public static FeatureAccessService FeatureAccess => RetailStorePOS.UI.Common.LoginRuntime.FeatureAccess;
    public static AuthService Auth => RetailStorePOS.UI.Common.LoginRuntime.Auth;

    public static void Initialize() => RetailStorePOS.UI.Common.LoginRuntime.Initialize("TestRunnerReportingDb");
    public static void ApplyPersistedLanguageSetting() => RetailStorePOS.UI.Common.LoginRuntime.ApplyPersistedLanguageSetting();
    public static void ReportException(Exception exception, string operationName) => RetailStorePOS.UI.Common.LoginRuntime.ReportException(exception, operationName);
}
