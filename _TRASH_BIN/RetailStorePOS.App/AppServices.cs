using Microsoft.Extensions.DependencyInjection;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Repositories;
using RetailStorePOS.Data.Services;

namespace RetailStorePOS.App;

public static class AppServices
{
    public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
    public static ProductRepository Products { get; private set; } = null!;
    public static SaleRepository Sales { get; private set; } = null!;
    public static SettingsRepository Settings { get; private set; } = null!;
    public static TaxCategoryRepository TaxCategories { get; private set; } = null!;
    public static UserRepository Users { get; private set; } = null!;
    public static AuditLogRepository AuditLogs { get; private set; } = null!;
    public static ProductImportService ProductImport { get; private set; } = null!;
    public static ProductSearchService ProductSearch { get; private set; } = null!;
    public static AuthService Auth { get; private set; } = null!;
    public static IBackupService Backup { get; private set; } = null!;
    public static AuditLogService Audit { get; private set; } = null!;
    public static IReceiptService ReceiptPdf { get; private set; } = null!;
    public static ILocalPreferencesService LocalPreferences { get; private set; } = null!;
    public static ProductImportCoordinator ProductImports { get; private set; } = null!;
    public static TelemetryService Telemetry { get; private set; } = null!;
    public static UpdateService Updater { get; private set; } = null!;

    public static event EventHandler? SettingsUpdated;
    public static event EventHandler? ProductsUpdated;

    public static void Initialize(string databasePath)
    {
        ConnectionFactory = new SqliteConnectionFactory(databasePath);
        LocalPreferences = new LocalPreferencesService();

        // Seed encrypted credentials vault on first run
        SecureStorageService.SeedIfNeeded(
            "https://avuzwbmiiavbuaxmnitp.supabase.co",
            "sb_publishable_A7KJ9QwfYBS6fkVqES9g7w_gtubrVNX"
        );

        Telemetry = new TelemetryService(LocalPreferences, ConnectionFactory);
        Telemetry.StartBackgroundSync();

        Products = new ProductRepository(ConnectionFactory);
        Sales = new SaleRepository(ConnectionFactory);
        Settings = new SettingsRepository(ConnectionFactory);
        TaxCategories = new TaxCategoryRepository(ConnectionFactory);
        Users = new UserRepository(ConnectionFactory);
        AuditLogs = new AuditLogRepository(ConnectionFactory);

        ProductImport = new ProductImportService(ConnectionFactory);
        ProductImports = new ProductImportCoordinator(ProductImport);
        ProductSearch = new ProductSearchService(Products);
        ProductSearch.BuildIndex();

        Audit = new AuditLogService(AuditLogs);
        Auth = new AuthService(Users, Audit);
        Backup = new BackupService(databasePath);
        ReceiptPdf = new ReceiptPdfService();
        Updater = new UpdateService();
    }

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<CheckoutViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<CloseDayViewModel>();

        // Register Services for DI
        services.AddSingleton<ILocalPreferencesService>(LocalPreferences);
        services.AddSingleton<IReceiptService>(ReceiptPdf);
        services.AddSingleton<TelemetryService>(Telemetry);

        return services.BuildServiceProvider();
    }

    public static void RaiseSettingsUpdated()
    {
        SettingsUpdated?.Invoke(null, EventArgs.Empty);
    }

    public static void RaiseProductsUpdated()
    {
        ProductSearch.RefreshIndex();
        ProductsUpdated?.Invoke(null, EventArgs.Empty);
    }

    public static void ReportException(Exception exception, string operationName)
    {
        _ = Telemetry?.LogErrorAsync(exception, operationName);
    }
}
