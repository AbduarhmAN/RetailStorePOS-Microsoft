using System;

namespace Nexill.RetailStorePOS.Services.DeviceIdentity;

public sealed class DeviceIdentityInfo
{
    public int FingerprintVersion { get; init; } = 1;

    public string DeviceId { get; init; } = string.Empty;

    public string MachineFingerprintHash { get; init; } = string.Empty;

    public string AppProduct { get; init; } = DeviceIdentityService.ResolveAppProduct();

    public string? AppVersion { get; init; } = DeviceIdentityService.ResolveAppVersion();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
