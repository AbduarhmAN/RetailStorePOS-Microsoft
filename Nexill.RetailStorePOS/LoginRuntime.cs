using System.Reflection;
using RetailStorePOS.App.Modules.Products;
using RetailStorePOS.App.Services;
using global::RetailStorePOS.Data;
using global::RetailStorePOS.Data.Modules.Migrations;
using global::RetailStorePOS.Data.Modules.Products;
using global::RetailStorePOS.Data.Modules.Sales;
using global::RetailStorePOS.Data.Modules.Settings;
using global::RetailStorePOS.Data.Modules.Tax;
using global::RetailStorePOS.Data.Modules.UsersAuth;
using global::RetailStorePOS.Data.Modules.Reporting;
using global::RetailStorePOS.Data.Repositories;
using RetailStorePOS.WinUiLogin.Common;
using global::Nexill.RetailStorePOS.Modules.Reporting;

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
    public static RegisterSessionRepository RegisterSessions { get; private set; } = null!;
    public static global::Nexill.RetailStorePOS.Modules.Reporting.ReadinessService Readiness { get; private set; } = null!;

    public static BootstrapAdminHint? PendingBootstrapAdminHint { get; private set; }
    public static bool IsFreshStartResetRequested { get; private set; }
    public static bool WasRapidReopenAfterUncleanExit { get; private set; }
    public static string SessionDebugInfo { get; private set; } = "No debug info available";
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

        // Priority 1: Direct OS Environment Variables (Visual Studio Debugging / Live Override)
        var supabaseUrl = (Environment.GetEnvironmentVariable("SUPABASE_URL", EnvironmentVariableTarget.User)
                          ?? Environment.GetEnvironmentVariable("SUPABASE_URL"))?.Trim();
        var supabaseKey = (Environment.GetEnvironmentVariable("SUPABASE_KEY", EnvironmentVariableTarget.User)
                          ?? Environment.GetEnvironmentVariable("SUPABASE_KEY"))?.Trim();

        // Priority 2: Assembly Metadata (App installer default behavior)
        if (string.IsNullOrWhiteSpace(supabaseUrl))
        {
            supabaseUrl = GetAssemblyMetadata("RetailStorePOSSupabaseUrl");
        }

        if (string.IsNullOrWhiteSpace(supabaseKey))
        {
            supabaseKey = GetAssemblyMetadata("RetailStorePOSSupabaseKey");
        }

        // FORCE ERASE BAD KEY IF DETECTED
        if (supabaseKey != null && supabaseKey.StartsWith("sb_publishable_"))
        {
            supabaseKey = null; // Throw it away, do not seed it
        }

        if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
        {
            SecureStorageService.SeedIfNeeded(supabaseUrl, supabaseKey);
        }

        Telemetry = new TelemetryService(LocalPreferences, ConnectionFactory);

        // Task Group 3: Lifecycle Orchestration - Detect unfinished run
        try
        {
            var state = Telemetry.GetTelemetryState();
            if (!string.IsNullOrEmpty(state.ActiveRunId))
            {
                StartupTrace.Write($"Lifecycle: Detected unfinished run {state.ActiveRunId} started at {state.ActiveRunStartedAt}");
                
                // Emit unclean_exit for the unfinished prior run with crash-boundary signals
                _ = Telemetry.LogUncleanExitAsync(
                    state.ActiveRunId,
                    state.ActiveRunStartedAt,
                    state.LastActivityAt,
                    state.LastActivitySource);

                // CRITICAL FIX: We must check if the PRIOR session was recent.
                // We use ActiveRunStartedAt because we aren't using a heartbeat timer.
                if (state.ActiveRunStartedAt.HasValue)
                {
                    var lastStart = state.ActiveRunStartedAt.Value.ToUniversalTime();
                    var secondsSinceLastStart = (DateTime.UtcNow - lastStart).TotalSeconds;
                    StartupTrace.Write($"Lifecycle: Seconds since last session start: {secondsSinceLastStart:F1}");

                    if (secondsSinceLastStart <= 60)
                    {
                        StartupTrace.Write("Lifecycle: Rapid reopen detected! Triggering feedback dialog flag.");
                        WasRapidReopenAfterUncleanExit = true;
                    }

                    SessionDebugInfo = $"Last Start: {state.ActiveRunStartedAt:O}\n" +
                                       $"Current Start: {DateTime.UtcNow:O}\n" +
                                       $"Diff (sec): {secondsSinceLastStart:F1}\n" +
                                       $"Result: {(WasRapidReopenAfterUncleanExit ? "SHOW" : "SKIP")}";
                }
            }
            else
            {
                StartupTrace.Write("Lifecycle: No unfinished run detected. (Clean start)");
            }

            // Create new active run
            var newRunId = Guid.NewGuid().ToString();
            _ = Telemetry.LogAppLaunchAsync(newRunId);
        }
        catch (Exception ex)
        {
            ReportException(ex, "WinUiLogin.LifecycleOrchestration");
        }

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
        RegisterSessions = new RegisterSessionRepository(ConnectionFactory);

        var workspaceManager = new ReadinessWorkspaceManager();
        Readiness = new ReadinessService(
            new ReadinessRunner(workspaceManager, new ReadinessReportStore()), 
            new ReadinessReportStore()
        );

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

    private static string? GetAssemblyMetadata(string key)
    {
        return typeof(LoginRuntime).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == key)?.Value;
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

            await DeleteAppDataRootAsync().ConfigureAwait(false);
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

    private static async Task DeleteAppDataRootAsync()
    {
        var activeRoot = AppDataPaths.GetRootFolder();
        await DeleteDirectoryAsync(activeRoot);

        var tempRoot = Path.Combine(Path.GetTempPath(), "RetailStorePOS");
        if (!string.Equals(tempRoot, activeRoot, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteDirectoryAsync(tempRoot);
        }
    }

    private static async Task DeleteDirectoryAsync(string rootFolder)
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
                await Task.Delay(ResetRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(ResetRetryDelayMilliseconds);
            }
        }

        Directory.Delete(rootFolder, recursive: true);
    }
}

public sealed record BootstrapAdminHint(string Username, string Password);
