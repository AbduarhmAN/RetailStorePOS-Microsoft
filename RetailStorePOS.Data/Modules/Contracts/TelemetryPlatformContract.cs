namespace RetailStorePOS.Data.Modules.Contracts;

public static class TelemetryPlatformContract
{
    public const string OutboxTableName = "telemetry_outbox";
    public const string InstallationEventsTableName = "installation_events";
    public const string InstallationsUpsertPath = "/rest/v1/installations";
    public const string InstallationEventsUpsertPath = "/rest/v1/installation_events?on_conflict=id";
    public const string ErrorLogsInsertPath = "/rest/v1/error_logs";

    public static readonly OwnedDataAsset OutboxAsset = new()
    {
        AssetKey = "telemetry.outbox",
        AssetType = OwnedDataAssetType.OperationalState,
        OwnerModuleKey = "Telemetry",
        PrimaryConsumers = ["Telemetry"],
        Notes = "Queued telemetry payloads for optional asynchronous delivery."
    };

    public static readonly OwnedDataAsset InstallationEventsAsset = new()
    {
        AssetKey = "telemetry.installation-events",
        AssetType = OwnedDataAssetType.Table,
        OwnerModuleKey = "Telemetry",
        PrimaryConsumers = ["Telemetry", "Reporting"],
        Notes = "Locally persisted lifecycle events owned by the Telemetry platform module."
    };

    public static readonly ModuleContract LifecycleEventCommand = new()
    {
        ContractKey = "telemetry.lifecycle-event",
        OwnerModuleKey = "Telemetry",
        ContractType = ModuleContractType.Command,
        Purpose = "Queue lifecycle and error telemetry without taking ownership of business workflows.",
        Consumers = ["Sales", "Reporting", "App Runtime"],
        OfflineAllowed = true
    };

    public static readonly ModuleContract SettingsStateQuery = new()
    {
        ContractKey = "telemetry.settings-state",
        OwnerModuleKey = "Settings",
        ContractType = ModuleContractType.Query,
        Purpose = "Read telemetry runtime state from the Settings module through an explicit contract.",
        Consumers = ["Telemetry"],
        OfflineAllowed = true
    };

    public static readonly WorkflowBoundary TelemetryCaptureBoundary = new()
    {
        WorkflowKey = "telemetry.capture",
        CoordinatorModuleKey = "Telemetry",
        ParticipatingModules = ["Settings", "Telemetry"],
        OfflineCritical = false,
        WriteSequence =
        [
            "Telemetry reads runtime state through telemetry.settings-state.",
            "Telemetry stores installation events in telemetry.installation-events.",
            "Telemetry queues outbound payloads in telemetry.outbox.",
            "Telemetry flushes queued payloads asynchronously when network is available."
        ]
    };

    public static IReadOnlyList<ModuleContract> Contracts { get; } =
    [
        LifecycleEventCommand,
        SettingsStateQuery
    ];

    public static IReadOnlySet<string> AllowedQueueEndpoints { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            InstallationsUpsertPath,
            InstallationEventsUpsertPath,
            ErrorLogsInsertPath
        };

    public static T ResolveOutbox<T>(T participant) where T : class => RequireParticipant(LifecycleEventCommand.ContractKey, participant);
    public static T ResolveInstallationEvents<T>(T participant) where T : class => RequireParticipant(LifecycleEventCommand.ContractKey, participant);
    public static T ResolveSettingsState<T>(T participant) where T : class => RequireParticipant(SettingsStateQuery.ContractKey, participant);

    public static string BuildRequestUri(string supabaseUrl, string endpoint, bool mergeDuplicates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supabaseUrl);
        endpoint = NormalizeQueuedEndpoint(endpoint);

        return mergeDuplicates && !endpoint.Contains("on_conflict=", StringComparison.OrdinalIgnoreCase)
            ? $"{supabaseUrl}{endpoint}?on_conflict=install_id"
            : $"{supabaseUrl}{endpoint}";
    }

    public static string NormalizeQueuedEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        if (!AllowedQueueEndpoints.Contains(endpoint))
        {
            throw new InvalidOperationException($"Endpoint '{endpoint}' is outside the telemetry platform contract.");
        }

        return endpoint;
    }

    private static T RequireParticipant<T>(string contractKey, T participant) where T : class
    {
        ArgumentNullException.ThrowIfNull(participant, contractKey);
        return participant;
    }
}
