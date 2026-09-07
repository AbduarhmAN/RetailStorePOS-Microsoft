using System;
namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Deny-by-default authorization helper used by service-layer code paths to
/// refuse calls that the UI layer might have otherwise let through.
///
/// The pre-existing permission model lived entirely in XAML visibility / button
/// IsEnabled bindings (see <c>AuthService.CanCheckout</c>, <c>CanManageUsers</c>
/// etc.). That works for a normal user clicking buttons, but it does not
/// protect against:
/// <list type="bullet">
///   <item>A patched binary that re-enables a hidden button.</item>
///   <item>A code path that reaches the service directly (tests, debug
///         console, future programmatic callers, accidental ViewModel calls
///         from the wrong page).</item>
///   <item>Race conditions where a user clicks a feature button just before
///         their license expires or their permissions are revoked.</item>
/// </list>
///
/// Each <c>Require*</c> method throws <see cref="UnauthorizedAccessException"/>
/// when the policy is not satisfied. Callers handle the exception by aborting
/// the operation; UI surfaces should already have the corresponding button
/// disabled, so a thrown exception generally indicates either a programming
/// error or a tamper attempt.
/// </summary>
public sealed class AuthorizationGuard
{
    private readonly AuthService _auth;
    private readonly FeatureAccessService _features;

    public AuthorizationGuard(AuthService auth, FeatureAccessService features)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _features = features ?? throw new ArgumentNullException(nameof(features));
    }

    // --- Predicates ---------------------------------------------------------

    /// <summary>True iff a non-locked session is active.</summary>
    public bool IsAuthenticated => _auth.IsLoggedIn;

    /// <summary>True iff the active user has the IsAdmin flag.</summary>
    public bool IsAdmin => !_auth.IsLocked && _auth.CurrentUser?.IsAdmin == true;

    /// <summary>True iff the current install is permitted to use a feature.</summary>
    public bool HasFeature(string featureKey) => _features.CanUse(featureKey);

    // --- Throwing helpers ---------------------------------------------------

    /// <summary>Throws when no user is signed in or the session is locked.</summary>
    public void RequireAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Sign in required.");
        }
    }

    /// <summary>Throws when the current user is not an administrator.</summary>
    public void RequireAdmin()
    {
        RequireAuthenticated();
        if (!IsAdmin)
        {
            throw new UnauthorizedAccessException("Administrator role required.");
        }
    }

    /// <summary>
    /// Throws when the current install is not permitted to use
    /// <paramref name="featureKey"/>. Useful as the first line of every
    /// paid-feature service method.
    /// </summary>
    public void RequireFeature(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new ArgumentException("Feature key is required.", nameof(featureKey));
        }

        RequireAuthenticated();

        if (!_features.CanUse(featureKey))
        {
            throw new UnauthorizedAccessException(
                $"This action requires the '{featureKey}' feature, which is not enabled by your license.");
        }
    }

    /// <summary>
    /// Generic permission predicate. Use for ad-hoc checks that don't map to
    /// a single feature key (e.g. "current user can override price").
    /// </summary>
    public void RequirePermission(Func<bool> check, string permissionName)
    {
        if (check is null) throw new ArgumentNullException(nameof(check));
        if (string.IsNullOrWhiteSpace(permissionName))
            throw new ArgumentException("Permission name is required.", nameof(permissionName));

        RequireAuthenticated();

        bool allowed;
        try
        {
            allowed = check();
        }
        catch (Exception ex)
        {
            // A throwing predicate must NOT count as "allowed".
            throw new UnauthorizedAccessException(
                $"Permission check '{permissionName}' threw: {ex.Message}", ex);
        }

        if (!allowed)
        {
            throw new UnauthorizedAccessException(
                $"This action requires the '{permissionName}' permission.");
        }
    }
}
