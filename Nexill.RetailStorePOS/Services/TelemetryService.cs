using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Nexill.RetailStorePOS.Services.DeviceIdentity;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Modules.Settings;
using RetailStorePOS.Data.Modules.Telemetry;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.App.Services;

public class TelemetryService
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private static readonly WorkflowBoundary TelemetryBoundary = TelemetryPlatformContract.TelemetryCaptureBoundary;
    private static readonly TimeSpan BackgroundSyncInterval = TimeSpan.FromMinutes(5);
    private const string HeartbeatActivitySource = "heartbeat";
    private const string HardwareInstallIdSource = "hardware_v1_hash";
    private const string LegacyGuidInstallIdSource = "legacy_guid";
    private const string GuidFallbackInstallIdSource = "guid_fallback";
    private const string CustomInstallIdSource = "custom";
    private const string InstallIdMigratedEventType = "install_id_migrated";

    private readonly ILocalPreferencesService _preferencesService;
    private readonly HttpClient _httpClient;
    private readonly TelemetryOutboxRepository? _outboxRepository;
    private readonly InstallationEventRepository? _installationEventRepository;
    private readonly SettingsRepository? _settingsRepository;
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly object _runtimeStateLock = new();
    private CancellationTokenSource? _syncCancellation;
    private Task? _syncLoopTask;
    private readonly string _supabaseUrl;

    private static readonly string AppVersion = ResolveAppVersion();

    private sealed record InstallIdResolution(
        string InstallId,
        string InstallIdSource,
        string? PreviousInstallId,
        string? PreviousInstallIdSource,
        DateTime? InstallIdMigratedAtUtc,
        bool RequiresSave,
        bool WasMigrated);

    public TelemetryService(ILocalPreferencesService preferencesService)
        : this(preferencesService, null)
    {
    }

    public TelemetryService(ILocalPreferencesService preferencesService, SqliteConnectionFactory? connectionFactory)
    {
        _preferencesService = preferencesService;

        // Load credentials from DPAPI-encrypted vault
        _supabaseUrl = SecureStorageService.GetSecret("SupabaseUrl") ?? string.Empty;
        var supabaseKey = SecureStorageService.GetSecret("SupabaseKey") ?? string.Empty;
        _outboxRepository = connectionFactory is not null ? TelemetryPlatformContract.ResolveOutbox(new TelemetryOutboxRepository(connectionFactory)) : null;
        _installationEventRepository = connectionFactory is not null ? TelemetryPlatformContract.ResolveInstallationEvents(new InstallationEventRepository(connectionFactory)) : null;
        _settingsRepository = connectionFactory is not null ? TelemetryPlatformContract.ResolveSettingsState(new SettingsRepository(connectionFactory)) : null;

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(supabaseKey))
        {
            _httpClient.DefaultRequestHeaders.Add("apikey", supabaseKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
        }
    }

    public RetailStorePOS.Data.Models.TelemetryRuntimeState GetTelemetryState() => _settingsRepository?.GetTelemetryState() ?? new();

    public void SetTelemetryState(RetailStorePOS.Data.Models.TelemetryRuntimeState state) => _settingsRepository?.SetTelemetryState(state);

    public void TouchCurrentRun(string source = HeartbeatActivitySource)
    {
        if (_settingsRepository is null)
        {
            return;
        }

        var normalizedSource = string.IsNullOrWhiteSpace(source)
            ? HeartbeatActivitySource
            : source.Trim();

        try
        {
            UpdateRuntimeState(state =>
            {
                if (string.IsNullOrWhiteSpace(state.ActiveRunId))
                {
                    return state;
                }

                return state with
                {
                    LastActivityAt = DateTime.UtcNow,
                    LastActivitySource = normalizedSource
                };
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry activity touch failed: {ex.Message}");
        }
    }

    private void UpdateRuntimeState(Func<RetailStorePOS.Data.Models.TelemetryRuntimeState, RetailStorePOS.Data.Models.TelemetryRuntimeState> update)
    {
        if (_settingsRepository is null)
        {
            return;
        }

        lock (_runtimeStateLock)
        {
            var currentState = _settingsRepository.GetTelemetryState();
            var nextState = update(currentState);
            _settingsRepository.SetTelemetryState(nextState);
        }
    }

    private static string ResolveAppVersion()
    {
        var informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "unknown" : version.ToString(3);
    }

    public void StartBackgroundSync()
    {
        EnsureOptionalTelemetryBoundary();

        if (_outboxRepository is null || _syncCancellation is not null)
        {
            return;
        }

        _syncCancellation = new CancellationTokenSource();
        var cancellationToken = _syncCancellation.Token;
        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        _syncLoopTask = Task.Run(() => RunBackgroundSyncLoopAsync(cancellationToken));
        QueueTelemetryFlush(cancellationToken);
    }

    public void StopBackgroundSync()
    {
        if (_syncCancellation is null)
        {
            return;
        }

        NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
        _syncCancellation.Cancel();
        _syncCancellation.Dispose();
        _syncCancellation = null;
        _syncLoopTask = null;
    }

    private string? _cachedCountry;

    private async Task<string?> GetConnectedCountryAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedCountry))
        {
            return _cachedCountry;
        }

        var onlineCountry = await TryGetCountryFromOwnedEndpointAsync();
        if (IsValidIso2(onlineCountry))
        {
            _cachedCountry = onlineCountry!.Trim().ToUpperInvariant();
            Debug.WriteLine($"[TELEMETRY] Country resolved from owned endpoint: {_cachedCountry}");
            return _cachedCountry;
        }

        Debug.WriteLine("[TELEMETRY] Country unavailable from owned endpoint; sending null.");
        return null;
    }

    private async Task<string?> TryGetCountryFromOwnedEndpointAsync()
    {
        if (!CanAttemptRemoteTelemetry())
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_supabaseUrl}/functions/v1/location");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[TELEMETRY] Location function returned {(int)response.StatusCode}");
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("country", out var countryProperty))
            {
                return null;
            }

            return countryProperty.GetString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TELEMETRY] Location function failed: {ex.Message}");
            return null;
        }
    }

    private bool IsValidIso2(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        return code.Length == 2 && char.IsLetter(code[0]) && char.IsLetter(code[1]);
    }

    private async Task<(string InstallId, string DaysSinceInstall, string DaysSinceLastSeen, int CrashCount, int LaunchCount, DateTime? FirstRunAt, DateTime? LastUpdatePromptAt, DateTime? LastUpdateInstalledAt)> GetTrackingDataAsync(bool countLaunch = true)
    {
        var prefs = await _preferencesService.LoadPreferencesAsync();
        var nowUtc = DateTime.UtcNow;
        var now = nowUtc.Date;

        bool requiresSave = false;

        var stableHardwareInstallId = DeviceIdentityService.CreateStableInstallId();
        var installIdResolution = ResolveInstallId(prefs, stableHardwareInstallId, nowUtc);
        requiresSave |= installIdResolution.RequiresSave;

        if (installIdResolution.WasMigrated)
        {
            Debug.WriteLine(
                $"[TELEMETRY] Promoted InstallId from {installIdResolution.PreviousInstallId} to {installIdResolution.InstallId} (source: {installIdResolution.InstallIdSource})");
        }
        else if (installIdResolution.RequiresSave)
        {
            Debug.WriteLine(
                $"[TELEMETRY] Resolved InstallId: {installIdResolution.InstallId} (source: {installIdResolution.InstallIdSource})");
        }
        else
        {
            Debug.WriteLine(
                $"[TELEMETRY] Loaded existing InstallId: {installIdResolution.InstallId} (source: {installIdResolution.InstallIdSource})");
        }

        if (!prefs.FirstRunAt.HasValue)
        {
            prefs.FirstRunAt = nowUtc;
            requiresSave = true;
        }

        if (!prefs.InstalledAtUtc.HasValue)
        {
            prefs.InstalledAtUtc = nowUtc;
            requiresSave = true;
        }

        var daysSinceInstall = (now - prefs.FirstRunAt.Value.Date).TotalDays.ToString("F0");

        var daysSinceLastSeen = "0";
        if (prefs.LastSeenAt.HasValue)
        {
            daysSinceLastSeen = (now - prefs.LastSeenAt.Value.Date).TotalDays.ToString("F0");
        }

        if (countLaunch)
        {
            prefs.LastSeenAt = nowUtc;
            prefs.LaunchCount++;
            requiresSave = true;
        }

        if (requiresSave)
        {
            await _preferencesService.SavePreferencesAsync(prefs);
        }

        if (installIdResolution.WasMigrated)
        {
            await RecordInstallIdMigrationAsync(installIdResolution);
        }

        return (
            InstallId: prefs.InstallId!,
            DaysSinceInstall: daysSinceInstall,
            DaysSinceLastSeen: daysSinceLastSeen,
            CrashCount: prefs.CrashCount,
            LaunchCount: prefs.LaunchCount,
            FirstRunAt: prefs.FirstRunAt,
            LastUpdatePromptAt: prefs.LastUpdatePromptAt,
            LastUpdateInstalledAt: prefs.LastUpdateInstalledAt
        );
    }

    private static InstallIdResolution ResolveInstallId(LocalPreferences prefs, string? stableHardwareInstallId, DateTime nowUtc)
    {
        var normalizedHardwareInstallId = NormalizeInstallId(stableHardwareInstallId);
        var normalizedInstallId = NormalizeInstallId(prefs.InstallId);
        var normalizedRecordedSource = NormalizeInstallIdSource(prefs.InstallIdSource);
        bool requiresSave = false;

        if (!string.Equals(normalizedInstallId, prefs.InstallId, StringComparison.Ordinal))
        {
            prefs.InstallId = normalizedInstallId;
            requiresSave = true;
        }

        if (!string.Equals(normalizedRecordedSource, prefs.InstallIdSource, StringComparison.Ordinal))
        {
            prefs.InstallIdSource = normalizedRecordedSource;
            requiresSave = true;
        }

        if (string.IsNullOrWhiteSpace(normalizedInstallId))
        {
            var newInstallId = !string.IsNullOrWhiteSpace(normalizedHardwareInstallId)
                ? normalizedHardwareInstallId!
                : Guid.NewGuid().ToString();
            var installIdSource = !string.IsNullOrWhiteSpace(normalizedHardwareInstallId)
                ? HardwareInstallIdSource
                : GuidFallbackInstallIdSource;

            prefs.InstallId = newInstallId;
            prefs.InstallIdSource = installIdSource;
            requiresSave = true;

            return new InstallIdResolution(
                newInstallId,
                installIdSource,
                prefs.PreviousInstallId,
                prefs.PreviousInstallIdSource,
                prefs.InstallIdMigratedAtUtc,
                requiresSave,
                WasMigrated: false);
        }

        if (!string.IsNullOrWhiteSpace(normalizedHardwareInstallId) &&
            !string.Equals(normalizedInstallId, normalizedHardwareInstallId, StringComparison.OrdinalIgnoreCase))
        {
            var previousInstallId = normalizedInstallId!;
            var previousInstallIdSource = InferInstallIdSource(previousInstallId, normalizedHardwareInstallId, normalizedRecordedSource);

            prefs.PreviousInstallId = previousInstallId;
            prefs.PreviousInstallIdSource = previousInstallIdSource;
            prefs.InstallId = normalizedHardwareInstallId;
            prefs.InstallIdSource = HardwareInstallIdSource;
            prefs.InstallIdMigratedAtUtc = nowUtc;
            requiresSave = true;

            return new InstallIdResolution(
                normalizedHardwareInstallId!,
                HardwareInstallIdSource,
                previousInstallId,
                previousInstallIdSource,
                prefs.InstallIdMigratedAtUtc,
                requiresSave,
                WasMigrated: true);
        }

        var inferredSource = InferInstallIdSource(normalizedInstallId!, normalizedHardwareInstallId, normalizedRecordedSource);
        if (!string.Equals(inferredSource, prefs.InstallIdSource, StringComparison.Ordinal))
        {
            prefs.InstallIdSource = inferredSource;
            requiresSave = true;
        }

        return new InstallIdResolution(
            normalizedInstallId!,
            prefs.InstallIdSource ?? inferredSource,
            prefs.PreviousInstallId,
            prefs.PreviousInstallIdSource,
            prefs.InstallIdMigratedAtUtc,
            requiresSave,
            WasMigrated: false);
    }

    private async Task RecordInstallIdMigrationAsync(InstallIdResolution resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution.PreviousInstallId))
        {
            return;
        }

        var migratedAtUtc = resolution.InstallIdMigratedAtUtc ?? DateTime.UtcNow;

        await RecordInstallationEventAsync(
            Guid.NewGuid().ToString(),
            resolution.InstallId,
            InstallIdMigratedEventType,
            migratedAtUtc,
            new TelemetryInstallIdMigratedPayload
            {
                Type = InstallIdMigratedEventType,
                InstallId = resolution.InstallId,
                InstallIdSource = resolution.InstallIdSource,
                PreviousInstallId = resolution.PreviousInstallId,
                PreviousInstallIdSource = resolution.PreviousInstallIdSource,
                Timestamp = migratedAtUtc.ToString("O"),
                MigrationReason = "hardware_identity_promoted"
            },
            TelemetryJsonContext.Default.TelemetryInstallIdMigratedPayload);
    }

    private static string? NormalizeInstallId(string? installId)
    {
        return string.IsNullOrWhiteSpace(installId) ? null : installId.Trim();
    }

    private static string? NormalizeInstallIdSource(string? installIdSource)
    {
        return string.IsNullOrWhiteSpace(installIdSource) ? null : installIdSource.Trim();
    }

    private static string InferInstallIdSource(
        string installId,
        string? stableHardwareInstallId,
        string? recordedSource)
    {
        if (!string.IsNullOrWhiteSpace(stableHardwareInstallId) &&
            string.Equals(installId, stableHardwareInstallId, StringComparison.OrdinalIgnoreCase))
        {
            return HardwareInstallIdSource;
        }

        if (!string.IsNullOrWhiteSpace(recordedSource))
        {
            return recordedSource.Trim();
        }

        if (Guid.TryParse(installId, out _))
        {
            return LegacyGuidInstallIdSource;
        }

        return CustomInstallIdSource;
    }

    public async Task ImportBootstrapLifecycleEventsAsync()
    {
        if (_installationEventRepository is null)
        {
            return;
        }

        var trackingData = await GetTrackingDataAsync(countLaunch: false);
        var prefs = await _preferencesService.LoadPreferencesAsync();

        if (prefs.SetupCompletedAtUtc.HasValue)
        {
            var setupCompletedUtc = NormalizeInstallerTimestamp(prefs.SetupCompletedAtUtc.Value);
            var setupEventId = await EnsureBootstrapEventIdAsync(
                prefs,
                eventType: "setup_completed",
                occurredAtUtc: setupCompletedUtc);

            if (!string.IsNullOrWhiteSpace(setupEventId))
            {
                await RecordInstallationEventAsync(
                    setupEventId,
                    trackingData.InstallId,
                    "setup_completed",
                    setupCompletedUtc,
                    new TelemetrySetupCompletedPayload
                    {
                        SetupCompletedAtUtc = setupCompletedUtc.ToString("O"),
                        Source = "installer"
                    },
                    TelemetryJsonContext.Default.TelemetrySetupCompletedPayload);
            }
        }
    }

    private async Task RecordInstallationEventAtomicAsync<TPayload>(
        string eventId,
        string installId,
        string eventType,
        DateTime occurredAtUtc,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadJsonTypeInfo,
        RetailStorePOS.Data.Models.TelemetryRuntimeState nextState,
        string? eventRunId = null,
        DateTime? eventLastActivityAt = null,
        string? eventLastActivitySource = null)
    {
        if (_installationEventRepository is null || _outboxRepository is null || _settingsRepository is null)
        {
            return;
        }

        var payloadJson = SerializeTelemetryPayload(payload, payloadJsonTypeInfo);

        using var connection = _settingsRepository.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Populate the event row with explicit run and activity metadata
            _installationEventRepository.TryInsert(
                connection,
                transaction,
                eventId,
                installId,
                eventType,
                occurredAtUtc,
                payloadJson,
                runId: eventRunId,
                lastActivityAt: eventLastActivityAt,
                lastActivitySource: eventLastActivitySource);

            var remotePayload = new TelemetryInstallationEventUpsertPayload
            {
                Id = eventId,
                InstallId = installId,
                EventType = eventType,
                OccurredAt = occurredAtUtc.ToString("O"),
                PayloadJson = payloadJson,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                RunId = eventRunId,
                LastActivityAt = eventLastActivityAt?.ToString("O"),
                LastActivitySource = eventLastActivitySource
            };

            _outboxRepository.Enqueue(
                connection,
                transaction,
                TelemetryPlatformContract.InstallationEventsUpsertPath,
                SerializeTelemetryPayload(
                    remotePayload,
                    TelemetryJsonContext.Default.TelemetryInstallationEventUpsertPayload),
                true);

            // Persist the NEXT state (usually cleared for close/unclean_exit)
            _settingsRepository.SetTelemetryState(connection, transaction, nextState);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry atomic write failed: {ex.Message}");
            throw;
        }
    }

    public async Task LogAppClosedAsync(string runId, string reason = "app_exit")
    {
        try
        {
            var payload = new TelemetryAppClosePayload
            {
                Type = "app_close",
                RunId = runId,
                Reason = reason,
                AppVersion = AppVersion,
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            var trackingData = await GetTrackingDataAsync(countLaunch: false);

            // Atomically clear state and record event with its run identifier
            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "app_close",
                DateTime.UtcNow,
                payload,
                TelemetryJsonContext.Default.TelemetryAppClosePayload,
                nextState: new RetailStorePOS.Data.Models.TelemetryRuntimeState(),
                eventRunId: runId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry close logging exception: {ex.Message}");
        }
    }

    public async Task LogUncleanExitAsync(string runId, DateTime? startedAt, DateTime? lastActivityAt, string? lastActivitySource)
    {
        try
        {
            var trackingData = await GetTrackingDataAsync(countLaunch: false);
            var payload = new TelemetryUncleanExitPayload
            {
                Type = "unclean_exit",
                RunId = runId,
                AppVersion = AppVersion,
                Timestamp = DateTime.UtcNow.ToString("O"),
                StartedAt = startedAt?.ToString("O"),
                LastActivityAt = lastActivityAt?.ToString("O"),
                LastActivitySource = lastActivitySource
            };

            // Record unclean exit for the PRIOR run, passing prior activity data for estimation
            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "unclean_exit",
                DateTime.UtcNow,
                payload,
                TelemetryJsonContext.Default.TelemetryUncleanExitPayload,
                nextState: new RetailStorePOS.Data.Models.TelemetryRuntimeState(),
                eventRunId: runId,
                eventLastActivityAt: lastActivityAt,
                eventLastActivitySource: lastActivitySource);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry unclean_exit logging exception: {ex.Message}");
        }
    }

    public async Task LogAppLaunchAsync(string runId)
    {
        try
        {
            var trackingData = await GetTrackingDataAsync();
            var occurredAtUtc = DateTime.UtcNow;

            // 1. Record local event FIRST (Atomic with state update)
            var nextState = new RetailStorePOS.Data.Models.TelemetryRuntimeState
            {
                ActiveRunId = runId,
                ActiveRunStartedAt = occurredAtUtc,
                LastActivityAt = occurredAtUtc,
                LastActivitySource = "app_launch"
            };
            var initialPayload = new TelemetryAppLaunchInitialPayload
            {
                Type = "app_launch",
                InstallId = trackingData.InstallId,
                RunId = runId,
                AppVersion = AppVersion,
                Timestamp = occurredAtUtc.ToString("O"),
                AppLaunchedCount = trackingData.LaunchCount
            };

            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "app_launch",
                occurredAtUtc,
                initialPayload,
                TelemetryJsonContext.Default.TelemetryAppLaunchInitialPayload,
                nextState,
                eventRunId: runId);

            // 2. Trigger an immediate flush to push the queued event to the server
            QueueTelemetryFlush();

            // 3. Optional network-dependent enrichment and status update
            string? country = null;
            try
            {
                country = await GetConnectedCountryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TELEMETRY] Country enrichment failed: {ex.Message}");
            }

            string? currency = null;
            try
            {
                currency = System.Globalization.RegionInfo.CurrentRegion.ISOCurrencySymbol;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TELEMETRY] Currency enrichment failed: {ex.Message}");
            }

            bool? internet = null;
            try
            {
                internet = NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TELEMETRY] Network enrichment failed: {ex.Message}");
            }

            var enrichedPayload = new TelemetryAppLaunchEnrichedPayload
            {
                Type = "app_launch",
                InstallId = trackingData.InstallId,
                RunId = runId,
                AppVersion = AppVersion,
                Timestamp = occurredAtUtc.ToString("O"),
                WindowsVersion = GetWindowsVersion(),
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                DotnetRuntimeVersion = RuntimeInformation.FrameworkDescription,
                RamBucket = GetRamBucket(),
                CpuCoreBucket = GetCpuCoreBucket(),
                ScreenResolutionBucket = GetScreenResolutionBucket(),
                BuildNumber = "1200",
                ReleaseChannel = "production",
                FirstRunAt = trackingData.FirstRunAt?.ToString("O"),
                LastSeenAt = occurredAtUtc.ToString("O"),
                DaysSinceInstall = trackingData.DaysSinceInstall,
                DaysSinceLastSeen = occurredAtUtc.ToString("O"),
                CountrySetting = country,
                CurrencySetting = currency,
                StartupTimeBucketMs = "1000-2000",
                FeatureFlags = new[] { "new_checkout", "tax_inclusive" },
                LastUpdatePromptAt = trackingData.LastUpdatePromptAt?.ToString("O"),
                LastUpdateInstalledAt = trackingData.LastUpdateInstalledAt?.ToString("O"),
                CrashCount = trackingData.CrashCount,
                InternetStatus = internet,
                AppLaunchedCount = trackingData.LaunchCount
            };

            await SendOrQueueAsync(
                TelemetryPlatformContract.InstallationsUpsertPath,
                SerializeTelemetryPayload(
                    enrichedPayload,
                    TelemetryJsonContext.Default.TelemetryAppLaunchEnrichedPayload),
                mergeDuplicates: true,
                operationName: "app_launch");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TELEMETRY] LogAppLaunchAsync FAILED: {ex.Message}");
        }
    }

    public Task LogTimeWarningIgnoredAsync(DateTime localTimeUtc)
    {
        return LogKnownEventAsync(
            "time_warning_ignored",
            new TelemetryTimeWarningIgnoredPayload
            {
                LocalTime = localTimeUtc.ToString("O")
            },
            TelemetryJsonContext.Default.TelemetryTimeWarningIgnoredPayload);
    }

    public Task LogCrashFeedbackAsync(string? category, string? details)
    {
        return LogKnownEventAsync(
            "crash_feedback",
            new TelemetryCrashFeedbackPayload
            {
                Category = category,
                Details = details
            },
            TelemetryJsonContext.Default.TelemetryCrashFeedbackPayload);
    }

    public Task LogLicenseVerificationAsync(string outcome, string? errorCode)
    {
        return LogKnownEventAsync(
            "license_verification",
            new TelemetryLicenseVerificationPayload
            {
                Outcome = outcome,
                ErrorCode = errorCode ?? string.Empty
            },
            TelemetryJsonContext.Default.TelemetryLicenseVerificationPayload);
    }

    public Task LogReadinessRunCompletedAsync(
        string runId,
        string status,
        int blockingFailures,
        int observations,
        string? profile)
    {
        return LogKnownEventAsync(
            "readiness_run_completed",
            new TelemetryReadinessRunCompletedPayload
            {
                RunId = runId,
                Status = status,
                BlockingFailures = blockingFailures,
                Observations = observations,
                Profile = profile
            },
            TelemetryJsonContext.Default.TelemetryReadinessRunCompletedPayload);
    }

    private async Task LogKnownEventAsync<TPayload>(
        string eventType,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadJsonTypeInfo)
    {
        try
        {
            var trackingData = await GetTrackingDataAsync(countLaunch: false);
            await RecordInstallationEventAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                eventType,
                DateTime.UtcNow,
                payload,
                payloadJsonTypeInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry generic event logging exception: {ex.Message}");
        }
    }

    public async Task LogErrorAsync(Exception error, string operationName = "unhandled_exception")
    {
        try
        {
            var prefs = await _preferencesService.LoadPreferencesAsync();
            prefs.CrashCount++;
            await _preferencesService.SavePreferencesAsync(prefs);

            var trackingData = await GetTrackingDataAsync(countLaunch: false);
            var occurredAtUtc = DateTime.UtcNow;
            var localPayload = new TelemetryErrorLocalPayload
            {
                Type = "error",
                InstallId = trackingData.InstallId,
                AppVersion = AppVersion,
                Timestamp = occurredAtUtc.ToString("O"),
                ErrorCategory = error.GetType().Name,
                ErrorMessage = error.Message,
                ErrorDetails = error.ToString(),
                OperationName = operationName,
                CrashCount = prefs.CrashCount
            };

            var localJson = SerializeTelemetryPayload(
                localPayload,
                TelemetryJsonContext.Default.TelemetryErrorLocalPayload);
            TryRecordLocalEvent(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "error",
                occurredAtUtc,
                localJson);

            var remotePayload = new TelemetryErrorRemotePayload
            {
                Type = "error",
                InstallId = trackingData.InstallId,
                AppVersion = AppVersion,
                Timestamp = occurredAtUtc.ToString("O"),
                ErrorCategory = error.GetType().Name,
                ErrorMessage = error.ToString(),
                OperationName = operationName,
                CrashCount = prefs.CrashCount
            };

            await SendOrQueueAsync(
                TelemetryPlatformContract.ErrorLogsInsertPath,
                SerializeTelemetryPayload(
                    remotePayload,
                    TelemetryJsonContext.Default.TelemetryErrorRemotePayload),
                mergeDuplicates: false,
                operationName: operationName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry Error logging exception: {ex.Message}");
        }
    }

    private async Task<string?> EnsureBootstrapEventIdAsync(LocalPreferences prefs, string eventType, DateTime occurredAtUtc)
    {
        var eventId = prefs.SetupCompletedEventId;
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            return eventId;
        }

        eventId = Guid.NewGuid().ToString();
        prefs.SetupCompletedEventId = eventId;

        await _preferencesService.SavePreferencesAsync(prefs);
        Debug.WriteLine($"[TELEMETRY] Generated bootstrap event id for {eventType} at {occurredAtUtc:O}");
        return eventId;
    }

    private Task RecordInstallationEventAsync<TPayload>(
        string eventId,
        string installId,
        string eventType,
        DateTime occurredAtUtc,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadJsonTypeInfo)
    {
        var payloadJson = SerializeTelemetryPayload(payload, payloadJsonTypeInfo);
        return RecordInstallationEventJsonAsync(eventId, installId, eventType, occurredAtUtc, payloadJson);
    }

    private async Task RecordInstallationEventJsonAsync(string eventId, string installId, string eventType, DateTime occurredAtUtc, string payloadJson)
    {
        if (_installationEventRepository is null || string.IsNullOrWhiteSpace(installId))
        {
            return;
        }

        var inserted = TryRecordLocalEvent(eventId, installId, eventType, occurredAtUtc, payloadJson);
        if (!inserted)
        {
            return;
        }

        var remotePayload = new TelemetryInstallationEventUpsertPayload
        {
            Id = eventId,
            InstallId = installId,
            EventType = eventType,
            OccurredAt = occurredAtUtc.ToString("O"),
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await SendOrQueueAsync(
            TelemetryPlatformContract.InstallationEventsUpsertPath,
            SerializeTelemetryPayload(
                remotePayload,
                TelemetryJsonContext.Default.TelemetryInstallationEventUpsertPayload),
            mergeDuplicates: true,
            operationName: eventType);
    }

    private bool TryRecordLocalEvent(string eventId, string installId, string eventType, DateTime occurredAtUtc, string payloadJson)
    {
        if (_installationEventRepository is null || string.IsNullOrWhiteSpace(installId))
        {
            return false;
        }

        return _installationEventRepository.TryInsert(eventId, installId, eventType, occurredAtUtc, payloadJson);
    }

    private static string SerializeTelemetryPayload<TPayload>(
        TPayload payload,
        JsonTypeInfo<TPayload> payloadJsonTypeInfo)
    {
        return JsonSerializer.Serialize(payload, payloadJsonTypeInfo);
    }

    private static DateTime NormalizeInstallerTimestamp(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
    }

    public async Task FlushQueuedTelemetryAsync(CancellationToken cancellationToken = default)
    {
        EnsureOptionalTelemetryBoundary();

        if (_outboxRepository is null || !CanAttemptRemoteTelemetry())
        {
            return;
        }

        if (!await _flushGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var pending = _outboxRepository.GetPending(50); // Increased batch size
            if (pending.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var item in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Simple exponential backoff: 2^attempt minutes, capped at 4 hours
                if (item.AttemptCount > 0 && item.LastAttemptAt.HasValue)
                {
                    var minutesToWait = Math.Min(240, Math.Pow(2, item.AttemptCount));
                    if (now < item.LastAttemptAt.Value.AddMinutes(minutesToWait))
                    {
                        continue;
                    }
                }

                var sent = await TrySendPayloadAsync(item.Endpoint, item.PayloadJson, item.MergeDuplicates, cancellationToken);
                if (sent)
                {
                    _outboxRepository.MarkSent(item.Id);
                    continue;
                }

                _outboxRepository.MarkFailed(item.Id, "Telemetry sync failed while sending queued item.");
            }

            _outboxRepository.CleanupSentOlderThan(DateTime.UtcNow.AddDays(-30));
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async Task SendOrQueueAsync(string endpoint, string payloadJson, bool mergeDuplicates, string operationName)
    {
        if (await TrySendPayloadAsync(endpoint, payloadJson, mergeDuplicates))
        {
            await FlushQueuedTelemetryAsync();
            return;
        }

        if (_outboxRepository is not null)
        {
            _outboxRepository.Enqueue(endpoint, payloadJson, mergeDuplicates);
            Debug.WriteLine($"Telemetry queued for later sync: {operationName}");
        }
    }

    private async Task<bool> TrySendPayloadAsync(string endpoint, string payloadJson, bool mergeDuplicates, CancellationToken cancellationToken = default)
    {
        if (!CanAttemptRemoteTelemetry())
        {
            return false;
        }

        try
        {
            var requestUri = TelemetryPlatformContract.BuildRequestUri(_supabaseUrl, endpoint, mergeDuplicates);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            if (mergeDuplicates)
            {
                request.Headers.Add("Prefer", "resolution=merge-duplicates, return=minimal");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[TELEMETRY] HTTP {(int)response.StatusCode}: {errorBody}");
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TELEMETRY] Send failed exception: {ex.Message}");
            return false;
        }
    }

    private void NetworkChange_NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        var syncCancellation = _syncCancellation;
        if (!e.IsAvailable || syncCancellation is null || !CanAttemptRemoteTelemetry())
        {
            return;
        }

        CancellationToken cancellationToken;
        try
        {
            cancellationToken = syncCancellation.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        QueueTelemetryFlush(cancellationToken);
    }

    private void QueueTelemetryFlush(CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await FlushQueuedTelemetryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Telemetry queued flush failed: {ex.Message}");
                StartupTrace.Write($"Telemetry queued flush failed: {ex}");
            }
        }, CancellationToken.None);
    }

    private async Task RunBackgroundSyncLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(BackgroundSyncInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                TouchCurrentRun();

                try
                {
                    await FlushQueuedTelemetryAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Telemetry background sync tick failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Telemetry sync loop stopped: {ex.Message}");
        }
    }

    private bool CanAttemptRemoteTelemetry()
    {
        return !string.IsNullOrWhiteSpace(_supabaseUrl);
    }

    private static void EnsureOptionalTelemetryBoundary()
    {
        if (TelemetryBoundary.OfflineCritical)
        {
            throw new InvalidOperationException("Telemetry capture must remain optional for offline workflows.");
        }

        if (!TelemetryPlatformContract.LifecycleEventCommand.OfflineAllowed)
        {
            throw new InvalidOperationException($"Telemetry contract '{TelemetryPlatformContract.LifecycleEventCommand.ContractKey}' must remain queueable during offline workflows.");
        }
    }

    // --- Safe Hardware Bucketing Methods ---

    private string GetRamBucket()
    {
        try
        {
            // Note: Exact logic might require WMI or GC memory APIs
            // using GC.GetGCMemoryInfo().TotalAvailableMemoryBytes as approximation
            var memBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var memGB = memBytes / (1024 * 1024 * 1024.0);

            if (memGB <= 4) return "0-4GB";
            if (memGB <= 8) return "4-8GB";
            if (memGB <= 16) return "8-16GB";
            if (memGB <= 32) return "16-32GB";
            return "32GB+";
        }
        catch
        {
            return "unknown";
        }
    }

    private string GetCpuCoreBucket()
    {
        var cores = Environment.ProcessorCount;
        if (cores <= 2) return "1-2 Cores";
        if (cores <= 4) return "3-4 Cores";
        if (cores <= 8) return "5-8 Cores";
        if (cores <= 16) return "9-16 Cores";
        return "16+ Cores";
    }

    private string GetScreenResolutionBucket()
    {
        try
        {
            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);

            if (width <= 0 || height <= 0)
            {
                return "unknown";
            }

            return $"{width}x{height}";
        }
        catch
        {
            return "unknown";
        }
    }

    private string GetWindowsVersion()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return "unknown";
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}

internal sealed class TelemetrySetupCompletedPayload
{
    public string SetupCompletedAtUtc { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

internal sealed class TelemetryInstallIdMigratedPayload
{
    public string Type { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string InstallIdSource { get; set; } = string.Empty;
    public string? PreviousInstallId { get; set; }
    public string? PreviousInstallIdSource { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string MigrationReason { get; set; } = string.Empty;
}

internal sealed class TelemetryAppClosePayload
{
    public string Type { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

internal sealed class TelemetryUncleanExitPayload
{
    public string Type { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? LastActivityAt { get; set; }
    public string? LastActivitySource { get; set; }
}

internal sealed class TelemetryAppLaunchInitialPayload
{
    public string Type { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public int AppLaunchedCount { get; set; }
}

internal sealed class TelemetryAppLaunchEnrichedPayload
{
    public string Type { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string DotnetRuntimeVersion { get; set; } = string.Empty;
    public string RamBucket { get; set; } = string.Empty;
    public string CpuCoreBucket { get; set; } = string.Empty;
    public string ScreenResolutionBucket { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string ReleaseChannel { get; set; } = string.Empty;
    public string? FirstRunAt { get; set; }
    public string LastSeenAt { get; set; } = string.Empty;
    public string DaysSinceInstall { get; set; } = string.Empty;
    public string DaysSinceLastSeen { get; set; } = string.Empty;
    public string? CountrySetting { get; set; }
    public string? CurrencySetting { get; set; }
    public string StartupTimeBucketMs { get; set; } = string.Empty;
    public string[] FeatureFlags { get; set; } = [];
    public string? LastUpdatePromptAt { get; set; }
    public string? LastUpdateInstalledAt { get; set; }
    public int CrashCount { get; set; }
    public bool? InternetStatus { get; set; }
    public int AppLaunchedCount { get; set; }
}

internal sealed class TelemetryErrorLocalPayload
{
    public string Type { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string ErrorCategory { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public int CrashCount { get; set; }
}

internal sealed class TelemetryErrorRemotePayload
{
    public string Type { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string ErrorCategory { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public int CrashCount { get; set; }
}

internal sealed class TelemetryTimeWarningIgnoredPayload
{
    public string LocalTime { get; set; } = string.Empty;
}

internal sealed class TelemetryCrashFeedbackPayload
{
    public string? Category { get; set; }
    public string? Details { get; set; }
}

internal sealed class TelemetryLicenseVerificationPayload
{
    public string Outcome { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
}

internal sealed class TelemetryReadinessRunCompletedPayload
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int BlockingFailures { get; set; }
    public int Observations { get; set; }
    public string? Profile { get; set; }
}

internal sealed class TelemetryInstallationEventUpsertPayload
{
    public string Id { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string OccurredAt { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? RunId { get; set; }
    public string? LastActivityAt { get; set; }
    public string? LastActivitySource { get; set; }
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(TelemetrySetupCompletedPayload), TypeInfoPropertyName = nameof(TelemetrySetupCompletedPayload))]
[JsonSerializable(typeof(TelemetryInstallIdMigratedPayload), TypeInfoPropertyName = nameof(TelemetryInstallIdMigratedPayload))]
[JsonSerializable(typeof(TelemetryAppClosePayload), TypeInfoPropertyName = nameof(TelemetryAppClosePayload))]
[JsonSerializable(typeof(TelemetryUncleanExitPayload), TypeInfoPropertyName = nameof(TelemetryUncleanExitPayload))]
[JsonSerializable(typeof(TelemetryAppLaunchInitialPayload), TypeInfoPropertyName = nameof(TelemetryAppLaunchInitialPayload))]
[JsonSerializable(typeof(TelemetryAppLaunchEnrichedPayload), TypeInfoPropertyName = nameof(TelemetryAppLaunchEnrichedPayload))]
[JsonSerializable(typeof(TelemetryErrorLocalPayload), TypeInfoPropertyName = nameof(TelemetryErrorLocalPayload))]
[JsonSerializable(typeof(TelemetryErrorRemotePayload), TypeInfoPropertyName = nameof(TelemetryErrorRemotePayload))]
[JsonSerializable(typeof(TelemetryTimeWarningIgnoredPayload), TypeInfoPropertyName = nameof(TelemetryTimeWarningIgnoredPayload))]
[JsonSerializable(typeof(TelemetryCrashFeedbackPayload), TypeInfoPropertyName = nameof(TelemetryCrashFeedbackPayload))]
[JsonSerializable(typeof(TelemetryLicenseVerificationPayload), TypeInfoPropertyName = nameof(TelemetryLicenseVerificationPayload))]
[JsonSerializable(typeof(TelemetryReadinessRunCompletedPayload), TypeInfoPropertyName = nameof(TelemetryReadinessRunCompletedPayload))]
[JsonSerializable(typeof(TelemetryInstallationEventUpsertPayload), TypeInfoPropertyName = nameof(TelemetryInstallationEventUpsertPayload))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext
{
}
