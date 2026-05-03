using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Modules.Settings;
using RetailStorePOS.Data.Modules.Telemetry;

namespace RetailStorePOS.App.Services;

public class TelemetryService
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private static readonly WorkflowBoundary TelemetryBoundary = TelemetryPlatformContract.TelemetryCaptureBoundary;

    private readonly ILocalPreferencesService _preferencesService;
    private readonly HttpClient _httpClient;
    private readonly TelemetryOutboxRepository? _outboxRepository;
    private readonly InstallationEventRepository? _installationEventRepository;
    private readonly SettingsRepository? _settingsRepository;
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private CancellationTokenSource? _syncCancellation;
    private Task? _syncLoopTask;
    private readonly string _supabaseUrl;

    private static readonly string AppVersion = ResolveAppVersion();

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
        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        _syncLoopTask = Task.Run(() => RunBackgroundSyncLoopAsync(_syncCancellation.Token));
        _ = Task.Run(() => FlushQueuedTelemetryAsync(_syncCancellation.Token));
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

        if (string.IsNullOrEmpty(prefs.InstallId))
        {
            var newId = Guid.NewGuid().ToString();
            System.Diagnostics.Debug.WriteLine($"[TELEMETRY] Generated new InstallId: {newId}");
            prefs.InstallId = newId;
            requiresSave = true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[TELEMETRY] Loaded existing InstallId: {prefs.InstallId}");
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

        return (
            InstallId: prefs.InstallId,
            DaysSinceInstall: daysSinceInstall,
            DaysSinceLastSeen: daysSinceLastSeen,
            CrashCount: prefs.CrashCount,
            LaunchCount: prefs.LaunchCount,
            FirstRunAt: prefs.FirstRunAt,
            LastUpdatePromptAt: prefs.LastUpdatePromptAt,
            LastUpdateInstalledAt: prefs.LastUpdateInstalledAt
        );
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
                    new
                    {
                        setup_completed_at_utc = setupCompletedUtc.ToString("O"),
                        source = "installer"
                    });
            }
        }
    }

    private async Task RecordInstallationEventAtomicAsync(
        string eventId,
        string installId,
        string eventType,
        DateTime occurredAtUtc,
        object payload,
        RetailStorePOS.Data.Models.TelemetryRuntimeState nextState,
        string? eventRunId = null,
        DateTime? eventLastActivityAt = null,
        string? eventLastActivitySource = null)
    {
        if (_installationEventRepository is null || _outboxRepository is null || _settingsRepository is null)
        {
            return;
        }

        var payloadJson = JsonSerializer.Serialize(payload);

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

            var remotePayload = new
            {
                id = eventId,
                install_id = installId,
                event_type = eventType,
                occurred_at = occurredAtUtc.ToString("O"),
                payload_json = payloadJson,
                created_at = DateTime.UtcNow.ToString("O"),
                run_id = eventRunId,
                last_activity_at = eventLastActivityAt?.ToString("O"),
                last_activity_source = eventLastActivitySource
            };

            _outboxRepository.Enqueue(connection, transaction, TelemetryPlatformContract.InstallationEventsUpsertPath, JsonSerializer.Serialize(remotePayload), true);

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
            var payload = new
            {
                type = "app_close",
                run_id = runId,
                reason,
                app_version = AppVersion,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var trackingData = await GetTrackingDataAsync(countLaunch: false);

            // Atomically clear state and record event with its run identifier
            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "app_close",
                DateTime.UtcNow,
                payload,
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
            var payload = new
            {
                type = "unclean_exit",
                run_id = runId,
                app_version = AppVersion,
                timestamp = DateTime.UtcNow.ToString("O"),
                started_at = startedAt?.ToString("O"),
                last_activity_at = lastActivityAt?.ToString("O"),
                last_activity_source = lastActivitySource
            };

            // Record unclean exit for the PRIOR run, passing prior activity data for estimation
            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "unclean_exit",
                DateTime.UtcNow,
                payload,
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
            var nextState = new RetailStorePOS.Data.Models.TelemetryRuntimeState { ActiveRunId = runId, ActiveRunStartedAt = occurredAtUtc };
            var initialPayload = new
            {
                type = "app_launch",
                install_id = trackingData.InstallId,
                run_id = runId,
                app_version = AppVersion,
                timestamp = occurredAtUtc.ToString("O"),
                app_launched_count = trackingData.LaunchCount
            };

            await RecordInstallationEventAtomicAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "app_launch",
                occurredAtUtc,
                initialPayload,
                nextState,
                eventRunId: runId);

            // 2. Trigger an immediate flush to push the queued event to the server
            _ = Task.Run(() => FlushQueuedTelemetryAsync());

            // 3. Optional network-dependent enrichment and status update
            string? country = null;
            try { country = await GetConnectedCountryAsync(); } catch { }

            string? currency = null;
            try { currency = System.Globalization.RegionInfo.CurrentRegion.ISOCurrencySymbol; } catch { }

            bool? internet = null;
            try { internet = NetworkInterface.GetIsNetworkAvailable(); } catch { }

            var enrichedPayload = new
            {
                type = "app_launch",
                install_id = trackingData.InstallId,
                run_id = runId,
                app_version = AppVersion,
                timestamp = occurredAtUtc.ToString("O"),
                windows_version = GetWindowsVersion(),
                os_architecture = RuntimeInformation.OSArchitecture.ToString(),
                dotnet_runtime_version = RuntimeInformation.FrameworkDescription,
                ram_bucket = GetRamBucket(),
                cpu_core_bucket = GetCpuCoreBucket(),
                screen_resolution_bucket = GetScreenResolutionBucket(),

                build_number = "1200",
                release_channel = "production",
                first_run_at = trackingData.FirstRunAt?.ToString("O"),
                last_seen_at = occurredAtUtc.ToString("O"),
                days_since_install = trackingData.DaysSinceInstall,
                days_since_last_seen = occurredAtUtc.ToString("O"),
                country_setting = country,
                currency_setting = currency,
                startup_time_bucket_ms = "1000-2000",
                feature_flags = new[] { "new_checkout", "tax_inclusive" },
                last_update_prompt_at = trackingData.LastUpdatePromptAt?.ToString("O"),
                last_update_installed_at = trackingData.LastUpdateInstalledAt?.ToString("O"),
                crash_count = trackingData.CrashCount,
                internet_status = internet,
                app_launched_count = trackingData.LaunchCount
            };

            await SendOrQueueAsync(TelemetryPlatformContract.InstallationsUpsertPath, JsonSerializer.Serialize(enrichedPayload), mergeDuplicates: true, operationName: "app_launch");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TELEMETRY] LogAppLaunchAsync FAILED: {ex.Message}");
        }
    }

    public async Task LogGenericEventAsync(string eventType, object payload)
    {
        try
        {
            var trackingData = await GetTrackingDataAsync(countLaunch: false);
            await RecordInstallationEventAsync(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                eventType,
                DateTime.UtcNow,
                payload);
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
            var localPayload = new
            {
                type = "error",
                install_id = trackingData.InstallId,
                app_version = AppVersion,
                timestamp = occurredAtUtc.ToString("O"),
                error_category = error.GetType().Name,
                error_message = error.Message,
                error_details = error.ToString(),
                operation_name = operationName,
                crash_count = prefs.CrashCount
            };

            var localJson = JsonSerializer.Serialize(localPayload);
            TryRecordLocalEvent(
                Guid.NewGuid().ToString(),
                trackingData.InstallId,
                "error",
                occurredAtUtc,
                localJson);

            var remotePayload = new
            {
                type = "error",
                install_id = trackingData.InstallId,
                app_version = AppVersion,
                timestamp = occurredAtUtc.ToString("O"),
                error_category = error.GetType().Name,
                error_message = error.ToString(),
                operation_name = operationName,
                crash_count = prefs.CrashCount
            };

            await SendOrQueueAsync(
                TelemetryPlatformContract.ErrorLogsInsertPath,
                JsonSerializer.Serialize(remotePayload),
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

    private async Task RecordInstallationEventAsync(string eventId, string installId, string eventType, DateTime occurredAtUtc, object payload)
    {
        if (_installationEventRepository is null || string.IsNullOrWhiteSpace(installId))
        {
            return;
        }

        var payloadJson = JsonSerializer.Serialize(payload);
        var inserted = TryRecordLocalEvent(eventId, installId, eventType, occurredAtUtc, payloadJson);
        if (!inserted)
        {
            return;
        }

        await SendOrQueueAsync(
            TelemetryPlatformContract.InstallationEventsUpsertPath,
            JsonSerializer.Serialize(new
            {
                id = eventId,
                install_id = installId,
                event_type = eventType,
                occurred_at = occurredAtUtc.ToString("O"),
                payload_json = payloadJson,
                created_at = DateTime.UtcNow.ToString("O")
            }),
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
        if (!e.IsAvailable || _syncCancellation is null || !CanAttemptRemoteTelemetry())
        {
            return;
        }

        _ = Task.Run(() => FlushQueuedTelemetryAsync(_syncCancellation.Token));
    }

    private async Task RunBackgroundSyncLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushQueuedTelemetryAsync(cancellationToken);
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
