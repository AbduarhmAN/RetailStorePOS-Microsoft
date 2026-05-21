using System;
using System.Collections.Concurrent;
using RetailStorePOS.Data.Modules.Settings;

namespace RetailStorePOS.App.Services.Licensing;

/// <summary>
/// Runtime feature-access decisions for license-gated functionality.
///
/// Decision precedence (highest first):
/// <list type="number">
///   <item>Per-feature developer override stored in <see cref="SettingsRepository"/>.
///         Lets QA pin a feature on or off regardless of license state.</item>
///   <item>Global "force free mode" flag — denies every paid feature at once.</item>
///   <item>Validated license activation snapshot supplied by
///         <see cref="LicenseValidationService"/>. When a current, signed,
///         non-expired snapshot is present, the feature must be in its
///         <c>Features</c> list to be allowed.</item>
///   <item>Pre-release default policy: paid features that the build knows about
///         are allowed even with no snapshot, so the app remains usable while
///         licensing is being rolled out. Unknown feature keys are denied.
///         Flip this to deny-by-default once licensing is mandatory.</item>
/// </list>
///
/// All decisions are local, synchronous, and deny-by-default for unknown keys.
/// The class is safe to keep as a singleton — the snapshot accessor is invoked
/// on every <see cref="CanUse(string)"/> call, so live updates from
/// <see cref="LicenseValidationService.SnapshotChanged"/> become visible
/// immediately after <see cref="OnSnapshotChanged"/> is invoked.
/// </summary>
public sealed class FeatureAccessService
{
    // Settings keys persisted via SettingsRepository (KV table).
    internal const string ForceFreeModeKey = "feature.dev.force_free_mode";
    internal const string DeveloperOverridePrefix = "feature.dev.override.";

    // Known paid feature keys. Listed here so misspelled keys at call sites are
    // visible in code review rather than silently denied.
    public static class Features
    {
        public const string AdvancedReports = "AdvancedReports";
        public const string DashboardDatePill = "DashboardDatePill";
    }

    private readonly SettingsRepository _settings;
    private readonly Func<LicenseActivationSnapshot?> _snapshotAccessor;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ConcurrentDictionary<string, bool?> _overrideCache = new(StringComparer.Ordinal);
    private bool? _forceFreeModeCache;

    /// <summary>
    /// Raised whenever a feature-access decision could change (override toggled,
    /// force-free-mode toggled, or license snapshot refreshed).
    /// </summary>
    public event EventHandler? FeatureAccessChanged;

    /// <summary>
    /// Production constructor — used before licensing is wired. Equivalent to
    /// passing an always-null snapshot accessor.
    /// </summary>
    public FeatureAccessService(SettingsRepository settings)
        : this(settings, snapshotAccessor: null, clock: null)
    {
    }

    /// <summary>
    /// Full constructor used once <see cref="LicenseValidationService"/> is
    /// online. <paramref name="snapshotAccessor"/> is invoked on every
    /// <see cref="CanUse(string)"/> call so the latest snapshot is always the
    /// one consulted; the call is expected to be cheap (just an in-memory read).
    /// </summary>
    public FeatureAccessService(
        SettingsRepository settings,
        Func<LicenseActivationSnapshot?>? snapshotAccessor,
        Func<DateTimeOffset>? clock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _snapshotAccessor = snapshotAccessor ?? (static () => null);
        _clock = clock ?? (static () => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Returns true when the install is permitted to use <paramref name="featureKey"/>.
    /// </summary>
    public bool CanUse(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return false;
        }

        // 1. Per-feature developer override wins over everything (so QA can pin a state).
        var overrideValue = GetDeveloperOverride(featureKey);
        if (overrideValue.HasValue)
        {
            return overrideValue.Value;
        }

        // 2. Global "force free" cuts off all paid features at once.
        if (IsForceFreeMode)
        {
            return false;
        }

        // 3. Validated license snapshot (when present and currently valid) is
        //    the source of truth: allow iff the feature is listed in the
        //    certificate. We deliberately do not fall back to the pre-release
        //    default if the snapshot exists but is missing the feature, so a
        //    user with a Standard license cannot accidentally see Premium UI.
        var snapshot = SafeGetSnapshot();
        if (snapshot is not null && snapshot.IsCurrentlyValid(_clock()))
        {
            return snapshot.HasFeature(featureKey);
        }

        // 4. Pre-release default policy. See class doc — keep allowing known
        //    paid features so the app stays usable for users who have not
        //    activated yet. Unknown keys remain denied.
        return GetDefaultPolicy(featureKey);
    }

    /// <summary>
    /// True iff a validated license snapshot is currently in force. UI can use
    /// this to switch a "pre-release access" badge for "Active license".
    /// </summary>
    public bool IsLicenseEnforcementActive
    {
        get
        {
            var snapshot = SafeGetSnapshot();
            return snapshot is not null && snapshot.IsCurrentlyValid(_clock());
        }
    }

    /// <summary>
    /// Returns the current snapshot if any. Useful for surfacing the active
    /// permission group / expiry on a settings or about page.
    /// </summary>
    public LicenseActivationSnapshot? CurrentSnapshot => SafeGetSnapshot();

    /// <summary>
    /// True when the user has flipped the global "force free mode" toggle. While
    /// true, every paid feature is denied regardless of any positive override
    /// or active license snapshot.
    /// </summary>
    public bool IsForceFreeMode
    {
        get
        {
            if (_forceFreeModeCache.HasValue)
            {
                return _forceFreeModeCache.Value;
            }

            var raw = _settings.GetSetting(ForceFreeModeKey, "0");
            var value = string.Equals(raw, "1", StringComparison.Ordinal);
            _forceFreeModeCache = value;
            return value;
        }
    }

    /// <summary>
    /// Persists the global "force free mode" flag and notifies subscribers.
    /// </summary>
    public void SetForceFreeMode(bool enabled)
    {
        _settings.SetSetting(ForceFreeModeKey, enabled ? "1" : "0");
        _forceFreeModeCache = enabled;
        FeatureAccessChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the developer override for <paramref name="featureKey"/> if one is
    /// set; null when no override is in effect (default policy applies).
    /// </summary>
    public bool? GetDeveloperOverride(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return null;
        }

        if (_overrideCache.TryGetValue(featureKey, out var cached))
        {
            return cached;
        }

        var raw = _settings.GetSetting(DeveloperOverridePrefix + featureKey, string.Empty);
        bool? value = raw switch
        {
            "1" => true,
            "0" => false,
            _ => null
        };

        _overrideCache[featureKey] = value;
        return value;
    }

    /// <summary>
    /// Sets or clears the developer override for <paramref name="featureKey"/>.
    /// Pass null to clear the override and fall back to the default policy.
    /// </summary>
    public void SetDeveloperOverride(string featureKey, bool? value)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new ArgumentException("Feature key is required.", nameof(featureKey));
        }

        var key = DeveloperOverridePrefix + featureKey;
        var serialized = value switch
        {
            true => "1",
            false => "0",
            null => string.Empty
        };

        _settings.SetSetting(key, serialized);
        _overrideCache[featureKey] = value;
        FeatureAccessChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Cycles the developer override for <paramref name="featureKey"/> through
    /// (no override) → force-allow → force-deny → (no override). Useful for a
    /// hidden debug hotkey on the Reports page.
    /// </summary>
    public bool? CycleDeveloperOverride(string featureKey)
    {
        var current = GetDeveloperOverride(featureKey);
        bool? next = current switch
        {
            null => true,
            true => false,
            false => null
        };
        SetDeveloperOverride(featureKey, next);
        return next;
    }

    /// <summary>
    /// Called by the licensing wiring whenever a new snapshot is loaded,
    /// activated, or cleared. Triggers <see cref="FeatureAccessChanged"/> so
    /// any UI bound to feature access re-evaluates.
    /// </summary>
    public void OnSnapshotChanged()
    {
        FeatureAccessChanged?.Invoke(this, EventArgs.Empty);
    }

    private LicenseActivationSnapshot? SafeGetSnapshot()
    {
        try
        {
            return _snapshotAccessor();
        }
        catch
        {
            // Never let a faulty accessor break the feature-access path.
            return null;
        }
    }

    private static bool GetDefaultPolicy(string featureKey)
    {
        // Pre-release default: known paid feature keys are allowed so the build
        // can exercise their UI without a license certificate. Unknown keys are
        // denied so typos at call sites do not silently bypass gating.
        return featureKey switch
        {
            Features.AdvancedReports => true,
            Features.DashboardDatePill => true,
            _ => false
        };
    }
}
