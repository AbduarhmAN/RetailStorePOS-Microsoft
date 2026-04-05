using RetailStorePOS.App.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Repositories;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin;

public static class LoginRuntime
{
    private const string BootstrapAdminUsername = "admin";
    private const string BootstrapAdminDisplayName = "Administrator";
    private const string BootstrapAdminPassword = "1234";
    private const string BootstrapAdminPin = "1234";
    private const int ResetRetryCount = 5;
    private const int ResetRetryDelayMilliseconds = 250;

    public static string DatabasePath { get; private set; } = string.Empty;
    public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
    public static ILocalPreferencesService LocalPreferences { get; private set; } = null!;
    public static TelemetryService Telemetry { get; private set; } = null!;
    public static ProductSearchService ProductSearch { get; private set; } = null!;
    public static UserRepository Users { get; private set; } = null!;
    public static AuditLogRepository AuditLogs { get; private set; } = null!;
    public static AuditLogService Audit { get; private set; } = null!;
    public static AuthService Auth { get; private set; } = null!;
    public static ProductRepository Products { get; private set; } = null!;
    public static SaleRepository Sales { get; private set; } = null!;
    public static SettingsRepository Settings { get; private set; } = null!;
    public static TaxCategoryRepository TaxCategories { get; private set; } = null!;
    public static TaxAuthorityRepository TaxAuthorities { get; private set; } = null!;
    public static TaxRuleRepository TaxRules { get; private set; } = null!;
    public static TaxGroupRepository TaxGroups { get; private set; } = null!;
    public static BootstrapAdminHint? PendingBootstrapAdminHint { get; private set; }
    public static bool IsFreshStartResetRequested { get; private set; }
    public static event EventHandler? ProductsUpdated;

    public static void Initialize()
    {
        if (Auth is not null)
        {
            return;
        }

        DatabasePath = DatabasePaths.GetDatabasePath("RetailStorePOS");
        DatabaseInitializer.Initialize(DatabasePath);

        ConnectionFactory = new SqliteConnectionFactory(DatabasePath);
        LocalPreferences = new LocalPreferencesService();

        // Use the same fallback URL logic just in case it hasn't seeded, but usually the installer does it
        SecureStorageService.SeedIfNeeded(
            "https://avuzwbmiiavbuaxmnitp.supabase.co",
            "sb_publishable_A7KJ9QwfYBS6fkVqES9g7w_gtubrVNX"
        );

        Telemetry = new TelemetryService(LocalPreferences, ConnectionFactory);
        try
        {
            Telemetry.StartBackgroundSync();
        }
        catch (Exception ex)
        {
            ReportException(ex, "WinUiLogin.Telemetry.StartBackgroundSync");
        }

        Products = new ProductRepository(ConnectionFactory);
        Sales = new SaleRepository(ConnectionFactory);
        Settings = new SettingsRepository(ConnectionFactory);
        TaxCategories = new TaxCategoryRepository(ConnectionFactory);
        TaxAuthorities = new TaxAuthorityRepository(ConnectionFactory);
        TaxRules = new TaxRuleRepository(ConnectionFactory);
        TaxGroups = new TaxGroupRepository(ConnectionFactory, TaxRules);
        Users = new UserRepository(ConnectionFactory);
        AuditLogs = new AuditLogRepository(ConnectionFactory);
        Audit = new AuditLogService(AuditLogs);
        Auth = new AuthService(Users, Audit);
        EnsureBootstrapAdminOnFirstRun();
        ProductSearch = new ProductSearchService(Products);
        ProductSearch.BuildIndex();
    }

    public static BootstrapAdminHint? ConsumeBootstrapAdminHint()
    {
        var hint = PendingBootstrapAdminHint;
        PendingBootstrapAdminHint = null;
        return hint;
    }

    public static void ReportException(Exception exception, string operationName)
    {
        _ = Telemetry?.LogErrorAsync(exception, operationName);
    }

    public static void RaiseProductsUpdated()
    {
        ProductSearch.RefreshIndex();
        ProductsUpdated?.Invoke(null, EventArgs.Empty);
    }

    public static async Task ResetFreshStartAsync()
    {
        if (IsFreshStartResetRequested)
        {
            return;
        }

        IsFreshStartResetRequested = true;

        try
        {
            Telemetry?.StopBackgroundSync();

            // Force SQLite to completely release its Win32 file lock on pos.db
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Additional tiny delay to let the OS fully flush the handle
            await Task.Delay(150);

            await Task.Run(DeleteAppDataRoot).ConfigureAwait(false);
        }
        catch
        {
            IsFreshStartResetRequested = false;
            throw;
        }
    }

    public static void CancelFreshStartReset()
    {
        IsFreshStartResetRequested = false;
    }

    private static void EnsureBootstrapAdminOnFirstRun()
    {
        if (Auth is null || !Auth.NeedsSetup)
        {
            return;
        }

        try
        {
            StartupTrace.Write("LoginRuntime.EnsureBootstrapAdminOnFirstRun:start");
            Auth.CreateFirstAdmin(
                BootstrapAdminUsername,
                BootstrapAdminDisplayName,
                BootstrapAdminPassword,
                BootstrapAdminPin);
            Auth.Logout();
            PendingBootstrapAdminHint = new BootstrapAdminHint(
                BootstrapAdminUsername,
                BootstrapAdminPassword);
            StartupTrace.Write("LoginRuntime.EnsureBootstrapAdminOnFirstRun:created");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LoginRuntime.EnsureBootstrapAdminOnFirstRun:failed:{ex.Message}");
            ReportException(ex, "WinUiLogin.EnsureBootstrapAdminOnFirstRun");
        }
    }

    private static void DeleteAppDataRoot()
    {
        var activeRoot = AppDataPaths.GetRootFolder();
        DeleteDirectory(activeRoot);

        var tempRoot = Path.Combine(Path.GetTempPath(), "RetailStorePOS");
        if (!string.Equals(tempRoot, activeRoot, StringComparison.OrdinalIgnoreCase))
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static void DeleteDirectory(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return;
        }

        for (var attempt = 1; attempt < ResetRetryCount; attempt++)
        {
            try
            {
                Directory.Delete(rootFolder, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(ResetRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(ResetRetryDelayMilliseconds);
            }
        }

        Directory.Delete(rootFolder, recursive: true);
    }
}

public sealed record BootstrapAdminHint(string Username, string Password);


