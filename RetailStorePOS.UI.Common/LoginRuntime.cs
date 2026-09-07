using System.Reflection;
using RetailStorePOS.UI.Common.Services;
using global::RetailStorePOS.Data;
using global::RetailStorePOS.Data.Models;
using global::RetailStorePOS.Data.Modules.Migrations;
using global::RetailStorePOS.Data.Modules.Products;
using global::RetailStorePOS.Data.Modules.Sales;
using global::RetailStorePOS.Data.Modules.Settings;
using global::RetailStorePOS.Data.Modules.Tax;
using global::RetailStorePOS.Data.Modules.UsersAuth;
using global::RetailStorePOS.Data.Modules.Reporting;
using global::RetailStorePOS.Data.Repositories;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Common;

public static class LoginRuntime
{
    private const string BootstrapAdminUsername = "admin";
    private const string BootstrapAdminDisplayName = "Administrator";
    // Bootstrap password and PIN are generated per-install at first run by
    // BootstrapCredentials (Services/BootstrapAdminCredentials.cs) and stored
    // DPAPI-encrypted in the local settings table. They are NOT compiled into
    // the binary anymore — the previous "1234" default was visible to anyone
    // who decompiled the app.
    private const int ResetRetryCount = 5;
    private const int ResetRetryDelayMilliseconds = 250;
    private static readonly object ProductSearchWarmupSync = new();

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
    public static AuthorizedAdvancedReportsService AdvancedReports { get; private set; } = null!;
    public static XReportRepository XReports { get; private set; } = null!;
    /// <summary>
    /// Raw, unguarded data-layer accessor. Avoid calling directly from
    /// production code — use <see cref="AdvancedReports"/> so the
    /// service-layer authorization guard runs. Exposed for migrations,
    /// integration tests, and the (future) admin maintenance console.
    /// </summary>
    internal static AdvancedReportsQueryService AdvancedReportsRaw { get; private set; } = null!;
    public static AuthorizationGuard Authorization { get; private set; } = null!;
    public static SnapshotSigningService SnapshotSigning { get; private set; } = null!;
    public static AdvancedReportsSnapshotStore AdvancedReportsSnapshots { get; private set; } = null!;
    public static OperationsSnapshotStore OperationsSnapshots { get; private set; } = null!;
    public static ProductPerformanceSnapshotStore ProductPerformanceSnapshots { get; private set; } = null!;
    public static SettingsRepository Settings { get; private set; } = null!;
    public static DatabaseBackupService DatabaseBackups { get; private set; } = null!;
    public static BootstrapAdminCredentials BootstrapCredentials { get; private set; } = null!;
    public static LicenseValidationService License { get; private set; } = null!;
    public static FeatureAccessService FeatureAccess { get; private set; } = null!;
    public static TaxCategoryRepository TaxCategories { get; private set; } = null!;
    public static TaxAuthorityRepository TaxAuthorities { get; private set; } = null!;
    public static TaxRuleRepository TaxRules { get; private set; } = null!;
    public static TaxGroupRepository TaxGroups { get; private set; } = null!;
    public static RegisterSessionRepository RegisterSessions { get; private set; } = null!;
    public static ReadinessService Readiness { get; private set; } = null!;

    public static BootstrapAdminHint? PendingBootstrapAdminHint { get; private set; }
    public static bool IsFreshStartResetRequested { get; private set; }
    public static bool WasRapidReopenAfterUncleanExit { get; private set; }
    public static bool IsOnboardingPhaseCleared { get; private set; }
    public static bool IsBootstrapPasswordChangeStillRequired { get; private set; }
    public static string SessionDebugInfo { get; private set; } = "No debug info available";
    private static Task? ProductSearchWarmupTask { get; set; }
    public static event EventHandler? ProductsUpdated;

    public static void Initialize(string databaseName = "RetailStorePOS")
    {
        if (Auth is not null)
        {
            return;
        }

        DatabasePath = DatabasePaths.GetDatabasePath(databaseName);
        StartupTrace.Write($"LoginRuntime.Initialize:database-path:{DatabasePath}");

        // Initialise the backup service early — both the corruption-recovery
        // path BEFORE DatabaseInitializer and the daily-snapshot path AFTER
        // it run through the same instance.
        DatabaseBackups = new DatabaseBackupService(AppDataPaths.Combine("backups"));

        // Pre-migration integrity check: if the on-disk DB is corrupt, try
        // to restore from the most recent backup before DatabaseInitializer
        // tries to open it. This is the line that protects the store from a
        // power-loss-mid-write or antivirus-quarantine outage.
        TryRecoverCorruptDatabase(DatabasePath);

        DatabaseInitializer.Initialize(DatabasePath);
        StartupTrace.Write("LoginRuntime.Initialize:startup migrations complete");
        var maintenanceResult = DatabaseInitializer.RunMaintenanceMigrations(DatabasePath);
        if (!maintenanceResult.Success)
        {
            throw new InvalidOperationException(
                $"Database maintenance migrations did not complete successfully. Current schema version: {maintenanceResult.Version}.");
        }

        StartupTrace.Write($"LoginRuntime.Initialize:maintenance migrations complete:schema={maintenanceResult.Version}");

        // Daily backup snapshot — idempotent: only runs when the most recent
        // backup is older than 23 hours so a normal restart does not flood
        // the backup folder. Failures are reported but do not abort startup.
        TryCreateDailyBackup(DatabasePath);

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
                StartupTrace.Write(
                    $"Lifecycle: Detected unfinished run {state.ActiveRunId} started at {state.ActiveRunStartedAt}, " +
                    $"last activity at {state.LastActivityAt} ({state.LastActivitySource ?? "unknown"})");
                
                var lastKnownActivity = state.LastActivityAt?.ToUniversalTime() ?? state.ActiveRunStartedAt?.ToUniversalTime();
                if (lastKnownActivity.HasValue)
                {
                    var secondsSinceLastActivity = (DateTime.UtcNow - lastKnownActivity.Value).TotalSeconds;
                    StartupTrace.Write($"Lifecycle: Seconds since last known activity: {secondsSinceLastActivity:F1}");

                    if (secondsSinceLastActivity <= 60)
                    {
                        StartupTrace.Write("Lifecycle: Rapid reopen detected! Triggering feedback dialog flag.");
                        WasRapidReopenAfterUncleanExit = true;
                    }

                    SessionDebugInfo = $"Last Start: {state.ActiveRunStartedAt:O}\n" +
                                       $"Last Activity: {state.LastActivityAt:O}\n" +
                                       $"Last Activity Source: {state.LastActivitySource ?? "unknown"}\n" +
                                       $"Current Start: {DateTime.UtcNow:O}\n" +
                                       $"Diff (sec): {secondsSinceLastActivity:F1}\n" +
                                       $"Result: {(WasRapidReopenAfterUncleanExit ? "SHOW" : "SKIP")}";
                }
            }
            else
            {
                StartupTrace.Write("Lifecycle: No unfinished run detected. (Clean start)");
            }

            // Do not start telemetry writes during the splash/login path. On
            // large local stores this can compete with page creation and push
            // the app into memory pressure before the user can interact.
            StartupTrace.Write("Lifecycle: Startup telemetry deferred");
        }
        catch (Exception ex)
        {
            ReportException(ex, "WinUiLogin.LifecycleOrchestration");
        }

        StartupTrace.Write("Telemetry.StartBackgroundSync:deferred");

        StartupTrace.Write("LoginRuntime.Initialize:repositories:start");
        Products = new ProductRepository(ConnectionFactory);
        Sales = new SaleRepository(ConnectionFactory);
        AdvancedReportsRaw = new AdvancedReportsQueryService(ConnectionFactory);
        XReports = new XReportRepository(ConnectionFactory);
        SnapshotSigning = new SnapshotSigningService();
        AdvancedReportsSnapshots = new AdvancedReportsSnapshotStore(SnapshotSigning);
        OperationsSnapshots = new OperationsSnapshotStore(SnapshotSigning);
        ProductPerformanceSnapshots = new ProductPerformanceSnapshotStore(SnapshotSigning);
        Settings = new SettingsRepository(ConnectionFactory);
        BootstrapCredentials = new BootstrapAdminCredentials(Settings);

        // License validation owns the activation certificate cache and is the
        // source of truth for FeatureAccessService once a user activates. Build
        // it before FeatureAccess so the FeatureAccess constructor can capture
        // a live accessor that always returns the latest snapshot. The cached
        // snapshot (if any) is loaded eagerly so the very first feature check
        // after login already sees the activated state.
        License = new LicenseValidationService(Settings);
        try
        {
            License.LoadCachedSnapshot();
        }
        catch (Exception ex)
        {
            // Never fail startup because the cached license blob is unreadable.
            // LicenseValidationService already deletes a corrupt blob; this catch
            // is the last safety net for unexpected failures (DPAPI churn, disk
            // pressure) so the user can still get to the login screen.
            ReportException(ex, "WinUiLogin.License.LoadCachedSnapshot");
        }

        FeatureAccess = new FeatureAccessService(
            Settings,
            snapshotAccessor: () => License?.GetCurrentSnapshot(),
            clock: () => DateTimeOffset.UtcNow);

        License.SnapshotChanged += (_, _) => FeatureAccess.OnSnapshotChanged();
        TaxCategories = new TaxCategoryRepository(ConnectionFactory);
        TaxAuthorities = new TaxAuthorityRepository(ConnectionFactory);
        TaxRules = new TaxRuleRepository(ConnectionFactory);
        TaxGroups = new TaxGroupRepository(ConnectionFactory, TaxRules);
        Users = new UserRepository(ConnectionFactory);
        AuditLogs = new AuditLogRepository(ConnectionFactory);
        Audit = new AuditLogService(AuditLogs, exceptionReporter: ReportException);
        Auth = new AuthService(Users, Audit);
        // FeatureAccess was constructed earlier (right after Settings + License)
        // because it needs the snapshot accessor. Now that Auth exists too we
        // can build the deny-by-default service-layer guard. The guard takes
        // both so service entry points can refuse on unauthenticated session
        // OR missing license feature.
        Authorization = new AuthorizationGuard(Auth, FeatureAccess);
        AdvancedReports = new AuthorizedAdvancedReportsService(AdvancedReportsRaw, Authorization);
        RegisterSessions = new RegisterSessionRepository(ConnectionFactory);
        RefreshOnboardingState();
        StartupTrace.Write("LoginRuntime.Initialize:repositories:end");

        var workspaceManager = new ReadinessWorkspaceManager();
        Readiness = new ReadinessService(
            new ReadinessRunner(workspaceManager, new ReadinessReportStore()), 
            new ReadinessReportStore()
        );

        StartupTrace.Write("LoginRuntime.Initialize:bootstrap-admin:start");
        EnsureBootstrapAdminOnFirstRun();
        RefreshBootstrapAdminPasswordState();
        ProductSearch = new ProductSearchService(Products);
        StartupTrace.Write("LoginRuntime.Initialize:complete");
    }


    public static void ApplyPersistedLanguageSetting()
    {
        if (Settings is null)
        {
            return;
        }

        try
        {
            var language = LocalizationHelper.NormalizeLanguageTag(Settings.GetAppLanguage());
            LocalizationService.Instance.SetLanguage(language);
            StartupTrace.Write($"LoginRuntime.Initialize:language-applied:{language}");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LoginRuntime.Initialize:language-apply-failed:{ex.Message}");
        }
    }

    public static BootstrapAdminHint? ConsumeBootstrapAdminHint()
    {
        var hint = PendingBootstrapAdminHint ?? GetBootstrapAdminHintForDisplay();
        PendingBootstrapAdminHint = null;
        return hint;
    }

    public static BootstrapAdminHint? GetBootstrapAdminHintForDisplay()
    {
        if (Users is null || BootstrapCredentials is null)
        {
            return PendingBootstrapAdminHint;
        }

        var admin = Users.GetByUsername(BootstrapAdminUsername);
        if (admin is null || !admin.IsAdmin || !admin.IsActive || !admin.MustChangePassword)
        {
            return null;
        }

        return GetValidBootstrapAdminHint(admin) ?? RotateBootstrapAdminTemporaryCredentials(admin);
    }

    public static void ReportException(Exception exception, string operationName)
    {
        try
        {
            StartupTrace.Write($"LoginRuntime.ReportException:{operationName}:{exception}");
            var telemetry = Telemetry;
            if (telemetry is null)
            {
                return;
            }

            _ = telemetry.LogErrorAsync(exception, operationName).ContinueWith(
                task => StartupTrace.Write($"LoginRuntime.ReportException.TelemetryFailed:{operationName}:{task.Exception}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LoginRuntime.ReportException failed:{operationName}:{ex}");
        }
    }

    public static void RaiseProductsUpdated()
    {
        ProductSearch.RefreshIndex();
        ProductsUpdated?.Invoke(null, EventArgs.Empty);
    }

    public static Task WarmProductSearchIndexAsync()
    {
        if (ProductSearch is null)
        {
            return Task.CompletedTask;
        }

        lock (ProductSearchWarmupSync)
        {
            return ProductSearchWarmupTask ??= Task.Run(() =>
            {
                try
                {
                    ProductSearch.BuildIndex();
                    ProductsUpdated?.Invoke(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    ReportException(ex, "WinUiLogin.ProductSearch.Warmup");
                }
            });
        }
    }

    public static void CompleteBootstrapAdminPasswordChange()
    {
        if (Settings is null)
        {
            return;
        }

        Settings.ClearOnboardingPhase();
        BootstrapCredentials?.Clear();
        PendingBootstrapAdminHint = null;
        IsOnboardingPhaseCleared = true;
        IsBootstrapPasswordChangeStillRequired = false;
    }

    private static string? GetAssemblyMetadata(string key)
    {
        // Installer metadata belongs to the executable, not this shared UI
        // assembly. Keep the shared assembly fallback for test hosts and tools.
        var entryAssemblyValue = Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == key)?.Value;

        if (!string.IsNullOrWhiteSpace(entryAssemblyValue))
        {
            return entryAssemblyValue;
        }

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

            // Generate per-install random credentials. The plaintext is shown
            // ONCE via PendingBootstrapAdminHint so the operator can capture
            // it; the persisted copy is DPAPI-encrypted and gets cleared after
            // the operator changes the password.
            var creds = BootstrapCredentials.GenerateAndPersist();

            Auth.CreateFirstAdmin(
                BootstrapAdminUsername,
                BootstrapAdminDisplayName,
                creds.Password,
                creds.Pin);
            Auth.Logout();
            PendingBootstrapAdminHint = new BootstrapAdminHint(
                BootstrapAdminUsername,
                creds.Password,
                creds.Pin);
            StartupTrace.Write("LoginRuntime.EnsureBootstrapAdminOnFirstRun:created");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LoginRuntime.EnsureBootstrapAdminOnFirstRun:failed:{ex.Message}");
            ReportException(ex, "WinUiLogin.EnsureBootstrapAdminOnFirstRun");
        }
    }

    private static void RefreshOnboardingState()
    {
        if (Settings is null)
        {
            return;
        }

        IsOnboardingPhaseCleared = Settings.IsOnboardingPhaseCleared();
    }

    private static void RefreshBootstrapAdminPasswordState()
    {
        if (Users is null)
        {
            return;
        }

        var admin = Users.GetByUsername(BootstrapAdminUsername);
        if (admin is null || !admin.IsAdmin || !admin.IsActive || !admin.MustChangePassword)
        {
            IsBootstrapPasswordChangeStillRequired = false;
            return;
        }

        // The bootstrap password is no longer hardcoded. Check whether the
        // persisted temp password still verifies against the stored hash;
        // if it does, the operator has not yet performed the change-password
        // flow. Once they do, BootstrapCredentials.Clear() empties the
        // temp store and this check returns false.
        var hint = GetValidBootstrapAdminHint(admin) ?? RotateBootstrapAdminTemporaryCredentials(admin);
        IsBootstrapPasswordChangeStillRequired = hint is not null;

        if (!IsBootstrapPasswordChangeStillRequired)
        {
            // Password was changed (or temp store was wiped). Make sure we
            // do not keep stale credentials on disk forever.
            BootstrapCredentials?.Clear();
        }
    }

    private static BootstrapAdminHint? GetValidBootstrapAdminHint(User admin)
    {
        var creds = BootstrapCredentials?.Load();
        if (creds is null)
        {
            return null;
        }

        if (creds.Pin.Length != 4 || !creds.Pin.All(char.IsDigit))
        {
            return null;
        }

        var passwordMatches = UserRepository.VerifyPassword(creds.Password, admin.PasswordHash);
        var pinMatches = UserRepository.VerifyPin(creds.Pin, admin.PinHash);
        return passwordMatches || pinMatches
            ? new BootstrapAdminHint(BootstrapAdminUsername, creds.Password, creds.Pin)
            : null;
    }

    private static BootstrapAdminHint? RotateBootstrapAdminTemporaryCredentials(User admin)
    {
        try
        {
            var creds = BootstrapCredentials.GenerateAndPersist();
            admin.PasswordHash = UserRepository.HashPassword(creds.Password);
            admin.PinHash = UserRepository.HashPin(creds.Pin);
            admin.MustChangePassword = true;
            Users.Update(admin);

            var hint = new BootstrapAdminHint(BootstrapAdminUsername, creds.Password, creds.Pin);
            PendingBootstrapAdminHint = hint;
            IsBootstrapPasswordChangeStillRequired = true;
            StartupTrace.Write("LoginRuntime.RotateBootstrapAdminTemporaryCredentials:rotated");
            return hint;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"LoginRuntime.RotateBootstrapAdminTemporaryCredentials:failed:{ex.Message}");
            ReportException(ex, "WinUiLogin.RotateBootstrapAdminTemporaryCredentials");
            return null;
        }
    }

    private static void TryRecoverCorruptDatabase(string databasePath)
    {
        if (DatabaseBackups is null || !File.Exists(databasePath))
        {
            return;
        }

        try
        {
            var integrity = DatabaseBackups.VerifyIntegrity(databasePath);
            if (integrity.IsOk)
            {
                return;
            }

            StartupTrace.Write(
                $"LoginRuntime.Initialize:integrity-check-failed:{integrity.Detail}");
            ReportException(
                new InvalidDataException($"Database integrity check failed: {integrity.Detail}"),
                "WinUiLogin.Database.IntegrityCheck");

            var restore = DatabaseBackups.TryRestoreFromLatestBackup(databasePath);
            if (restore.Succeeded)
            {
                StartupTrace.Write(
                    $"LoginRuntime.Initialize:restored-from-backup:{restore.BackupPath}");
            }
            else
            {
                StartupTrace.Write(
                    $"LoginRuntime.Initialize:restore-failed:{restore.ErrorMessage ?? "no backup"}");
                // We do not throw here. DatabaseInitializer will fail with a
                // clearer error if the file is still unusable, and the
                // unrecoverable-corruption UX is owned by the caller.
            }
        }
        catch (Exception ex)
        {
            // Best-effort. A failure during recovery should not prevent
            // DatabaseInitializer from getting its turn at the file.
            ReportException(ex, "WinUiLogin.Database.Recovery");
        }
    }

    private static void TryCreateDailyBackup(string databasePath)
    {
        if (DatabaseBackups is null || !File.Exists(databasePath))
        {
            return;
        }

        try
        {
            var latest = DatabaseBackups.GetLatestBackupTimestampUtc();
            if (latest is not null && DateTime.UtcNow - latest.Value < TimeSpan.FromHours(23))
            {
                // Most recent backup is younger than 23h. Skip — the previous
                // session already produced today's snapshot.
                return;
            }

            var backupPath = DatabaseBackups.CreateBackup(databasePath);
            DatabaseBackups.TrimBackups(keepCount: 7);
            StartupTrace.Write($"LoginRuntime.Initialize:daily-backup:{backupPath}");
        }
        catch (Exception ex)
        {
            // Reliability fix is meant to make recoverable failures visible,
            // not to take the app down. Surface via ReportException and move on.
            ReportException(ex, "WinUiLogin.Database.DailyBackup");
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

public sealed record BootstrapAdminHint(string Username, string Password, string Pin);
