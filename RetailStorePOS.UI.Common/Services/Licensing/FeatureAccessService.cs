using System;
using System.Collections.Concurrent;
using RetailStorePOS.Data.Modules.Settings;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Runtime feature-access decisions for license-gated functionality.
///
/// Decision precedence (highest first):
/// <list type="number">
///   <item>In Debug builds only, a per-feature developer override stored in
///         <see cref="SettingsRepository"/> lets QA pin a feature on or off
///         regardless of license state. Release builds ignore these overrides.</item>
///   <item>Global "force free mode" flag — denies every paid feature at once.</item>
///   <item>Validated license activation snapshot supplied by
///         <see cref="LicenseValidationService"/>. When a current, signed,
///         non-expired snapshot is present, the feature must be in its
///         <c>Features</c> list or be covered by a declared parent
///         entitlement to be allowed.</item>
///   <item>Default policy: Denies all paid features by default when no license is
///         active. Known and unknown feature keys alike are locked down until
///         an entitlement is verified.</item>
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
        ClearLegacyOverridesOnce();
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

        // 1. Debug-only developer override (so QA can pin a state).
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
        //    certificate. We deliberately do not fall back to the general
        //    default policy if the snapshot exists but is missing the feature, so a
        //    user with a Standard license cannot accidentally see Premium UI.
        var snapshot = SafeGetSnapshot();
        if (snapshot is not null && snapshot.IsCurrentlyValid(_clock()))
        {
            return SnapshotAllowsFeature(snapshot, featureKey);
        }

        // 4. Default policy. See class doc — locks down all paid features by default
        //    for users who have not activated yet.
        return GetDefaultPolicy(featureKey);
    }

    /// <summary>
    /// True iff a validated license snapshot is currently in force. UI can use
    /// this to switch a "Free Tier" badge for "Active license".
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

#if !DEBUG
        // Positive local overrides are a QA facility, not a production
        // entitlement source. Release builds always use the signed license.
        return null;
#else

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
#endif
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

    private const string LegacyOverridesClearedKey = "feature.dev.legacy_overrides_cleared";

    private void ClearLegacyOverridesOnce()
    {
        var alreadyCleared = _settings.GetSetting(LegacyOverridesClearedKey, "0");
        if (alreadyCleared != "1")
        {
            try
            {
                SetDeveloperOverride(Features.AdvancedReports, null);
                SetDeveloperOverride(Features.DashboardDatePill, null);
                _settings.SetSetting(LegacyOverridesClearedKey, "1");
            }
            catch (Exception)
            {
                // Degrade gracefully if settings storage is failing/read-only
            }
        }
    }

    private static bool GetDefaultPolicy(string featureKey)
    {
        // Default policy: Locks down all paid features (including AdvancedReports
        // and DashboardDatePill) by default when no license entitlement is present.
        return featureKey switch
        {
            Features.AdvancedReports => false,
            Features.DashboardDatePill => false,
            _ => false
        };
    }

    private static bool SnapshotAllowsFeature(LicenseActivationSnapshot snapshot, string featureKey)
    {
        if (snapshot.HasFeature(featureKey))
        {
            return true;
        }

        // DashboardDatePill is an enhancement inside the paid reports tier.
        // Existing Premium certificates that list AdvancedReports should not
        // need a second activation just to unlock the dashboard period picker.
        return string.Equals(featureKey, Features.DashboardDatePill, StringComparison.Ordinal)
            && snapshot.HasFeature(Features.AdvancedReports);
    }
}
