using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Threading;

namespace RetailStorePOS.UI.Common.Services;

public sealed class DeviceIdentityService
{
    private static readonly Lazy<string> StableInstallId = new(
        BuildStableInstallFingerprintHash,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<string> MachineFingerprint = new(
        BuildMachineFingerprintHash,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static string CreateStableInstallId()
    {
        return StableInstallId.Value;
    }

    public DeviceIdentityInfo CreateDeviceIdentity(string? appVersion = null)
    {
        var fingerprint = MachineFingerprint.Value;
        var deviceId = CreateDeviceId(fingerprint);

        return new DeviceIdentityInfo
        {
            FingerprintVersion = 1,
            DeviceId = deviceId,
            MachineFingerprintHash = fingerprint,
            AppProduct = ResolveAppProduct(),
            AppVersion = string.IsNullOrWhiteSpace(appVersion) ? ResolveAppVersion() : appVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    internal static string ResolveAppVersion()
    {
        var informationalVersion = typeof(DeviceIdentityService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        var version = typeof(DeviceIdentityService).Assembly.GetName().Version;
        return version is null ? "unknown" : version.ToString(3);
    }

    internal static string ResolveAppProduct()
    {
        var assembly = typeof(DeviceIdentityService).Assembly;

        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?
            .Product;
        if (!string.IsNullOrWhiteSpace(product))
        {
            return product;
        }

        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?
            .Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var name = assembly.GetName().Name;
        return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
    }

    private static string BuildMachineFingerprintHash()
    {
        var signals = new Dictionary<string, string?>
        {
            ["machine_guid"] = ReadMachineGuid(),
            ["smbios_uuid"] = ReadWmiValue("Win32_ComputerSystemProduct", "UUID"),
            ["baseboard_serial"] = ReadWmiValue("Win32_BaseBoard", "SerialNumber"),
            ["bios_serial"] = ReadWmiValue("Win32_BIOS", "SerialNumber"),
            ["system_disk_serial"] = ReadSystemDiskSerial()
        };

        var cleanedSignals = signals
            .Select(x => new
            {
                Key = x.Key,
                Value = NormalizeHardwareValue(x.Value)
            })
            .Where(x => IsUsableHardwareValue(x.Value))
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}={x.Value}");

        var canonicalText = string.Join("|", cleanedSignals);

        return Sha256Hex(canonicalText);
    }

    private static string BuildStableInstallFingerprintHash()
    {
        var signals = new Dictionary<string, string?>
        {
            ["smbios_uuid"] = ReadWmiValue("Win32_ComputerSystemProduct", "UUID"),
            ["baseboard_serial"] = ReadWmiValue("Win32_BaseBoard", "SerialNumber"),
            ["bios_serial"] = ReadWmiValue("Win32_BIOS", "SerialNumber"),
            ["system_enclosure_serial"] = ReadWmiValue("Win32_SystemEnclosure", "SerialNumber")
        };

        var cleanedSignals = signals
            .Select(x => new
            {
                Key = x.Key,
                Value = NormalizeHardwareValue(x.Value)
            })
            .Where(x => IsUsableHardwareValue(x.Value))
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}={x.Value}")
            .ToList();

        if (cleanedSignals.Count == 0)
        {
            return string.Empty;
        }

        var canonicalText = $"install-v1|{string.Join("|", cleanedSignals)}";
        return Sha256Hex(canonicalText);
    }

    private static string CreateDeviceId(string fingerprintHash)
    {
        var shortHash = fingerprintHash.Length >= 8
            ? fingerprintHash[..8]
            : fingerprintHash;

        return $"DEV-{shortHash}";
    }

    private static string? ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography",
                writable: false);

            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadWmiValue(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT {propertyName} FROM {className}");

            foreach (var item in searcher.Get())
            {
                var value = item[propertyName]?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ReadSystemDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0");

            foreach (var item in searcher.Get())
            {
                var value = item["SerialNumber"]?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string NormalizeHardwareValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }

    private static bool IsUsableHardwareValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var badValues = new HashSet<string>
        {
            "UNKNOWN",
            "NONE",
            "NULL",
            "DEFAULTSTRING",
            "TOBEFILLEDBYO.E.M.",
            "TOBEFILLEDBYOEM",
            "SYSTEMSERIALNUMBER",
            "00000000000000000000000000000000"
        };

        if (badValues.Contains(value))
            return false;

        if (value.All(c => c == '0'))
            return false;

        return true;
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

}
