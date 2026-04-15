using System.Diagnostics;
using System.Net.Http;
using Microsoft.Win32;
using RetailStorePOS.Data;

namespace RetailStorePOS.App.Services;

public sealed class PrerequisiteBootstrapService
{
    private const int ExitCodeUserCanceled = 1223;
    private const int ExitCodeRebootRequired = 3010;
    private const int ExitCodeRebootInitiated = 1641;
    private static readonly HttpClient DownloadClient = new();

    private static readonly IReadOnlyList<PrerequisiteDefinition> Definitions =
    [
        new(
            key: "DotNetDesktopRuntime",
            displayName: ".NET Desktop Runtime 8 (x64)",
            downloadUrl: "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.25/windowsdesktop-runtime-8.0.25-win-x64.exe",
            fileName: "windowsdesktop-runtime-8.0.25-win-x64.exe",
            silentArguments: "/install /quiet /norestart",
            isInstalled: IsDotNetDesktopRuntimeInstalled),
        new(
            key: "WindowsAppRuntime",
            displayName: "Windows App Runtime 1.8.5 (x64)",
            downloadUrl: "https://aka.ms/windowsappsdk/1.8/1.8.5/windowsappruntimeinstall-x64.exe",
            fileName: "WindowsAppRuntimeInstall-x64.exe",
            silentArguments: "--quiet",
            isInstalled: IsWindowsAppRuntimeInstalled),
        new(
            key: "VisualCppRedistributable",
            displayName: "Microsoft Visual C++ Redistributable 2015-2022 (x64)",
            downloadUrl: "https://aka.ms/vs/17/release/vc_redist.x64.exe",
            fileName: "vc_redist.x64.exe",
            silentArguments: "/install /quiet /norestart",
            isInstalled: IsVisualCppRedistributableInstalled)
    ];

    public Task<IReadOnlyList<PrerequisiteDefinition>> GetMissingPrerequisitesAsync()
    {
        return Task.Run<IReadOnlyList<PrerequisiteDefinition>>(() =>
            Definitions
                .Where(definition => !definition.IsInstalled())
                .ToArray());
    }

    public string GetInstallerCachePath(PrerequisiteDefinition prerequisite)
    {
        var root = AppDataPaths.Combine("Prerequisites");
        Directory.CreateDirectory(root);
        return Path.Combine(root, prerequisite.FileName);
    }

    public async Task DownloadInstallerAsync(
        PrerequisiteDefinition prerequisite,
        string destinationPath,
        IProgress<PrerequisiteDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        using var response = await DownloadClient.GetAsync(
            prerequisite.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var bytesRead = await responseStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            progress?.Report(new PrerequisiteDownloadProgress(
                prerequisite,
                totalRead,
                totalBytes,
                GetPercent(totalRead, totalBytes)));
        }

        progress?.Report(new PrerequisiteDownloadProgress(
            prerequisite,
            totalRead,
            totalBytes,
            100));
    }

    public async Task<PrerequisiteInstallResult> InstallPrerequisiteAsync(
        PrerequisiteDefinition prerequisite,
        string installerPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = prerequisite.SilentArguments,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            return new PrerequisiteInstallResult(false, false, $"Could not start {prerequisite.DisplayName}: {ex.Message}");
        }

        if (process is null)
        {
            return new PrerequisiteInstallResult(false, false, $"Could not start {prerequisite.DisplayName}.");
        }

        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode switch
        {
            0 => new PrerequisiteInstallResult(true, false, null),
            ExitCodeUserCanceled => new PrerequisiteInstallResult(false, false, $"The elevation request for {prerequisite.DisplayName} was canceled."),
            ExitCodeRebootRequired or ExitCodeRebootInitiated => new PrerequisiteInstallResult(
                false,
                true,
                $"{prerequisite.DisplayName} was installed, but Windows must restart before Retail Store POS can continue."),
            _ => new PrerequisiteInstallResult(false, false, $"{prerequisite.DisplayName} failed with exit code {process.ExitCode}.")
        };
    }

    public bool IsInstalled(PrerequisiteDefinition prerequisite)
    {
        return prerequisite.IsInstalled();
    }

    private static int GetPercent(long downloadedBytes, long? totalBytes)
    {
        if (totalBytes is null || totalBytes <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round((double)downloadedBytes / totalBytes.Value * 100, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static bool IsDotNetDesktopRuntimeInstalled()
    {
        return HasRegistrySubkeyVersionAtLeast(
                   RegistryView.Registry64,
                   @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App",
                   new Version(8, 0, 0))
               || HasRegistrySubkeyVersionAtLeast(
                   RegistryView.Registry32,
                   @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App",
                   new Version(8, 0, 0));
    }

    private static bool IsVisualCppRedistributableInstalled()
    {
        return HasVisualCppInstalledFlag(RegistryView.Registry64) || HasVisualCppInstalledFlag(RegistryView.Registry32);
    }

    private static bool IsWindowsAppRuntimeInstalled()
    {
        return HasWindowsAppRuntimePackage(
                   RegistryView.Registry64,
                   @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications")
               || HasWindowsAppRuntimePackage(
                   RegistryView.Registry64,
                   @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
    }

    private static bool HasRegistrySubkeyVersionAtLeast(RegistryView registryView, string subKeyPath, Version minimumVersion)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        if (subKey is null)
        {
            return false;
        }

        foreach (var childName in subKey.GetSubKeyNames())
        {
            if (Version.TryParse(childName, out var version) && version >= minimumVersion)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisualCppInstalledFlag(RegistryView registryView)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
        using var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
        var installedValue = subKey?.GetValue("Installed");
        return installedValue is int intValue && intValue == 1;
    }

    private static bool HasWindowsAppRuntimePackage(RegistryView registryView, string subKeyPath)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        if (subKey is null)
        {
            return false;
        }

        foreach (var childName in subKey.GetSubKeyNames())
        {
            if (childName.StartsWith("Microsoft.WindowsAppRuntime.1.8", StringComparison.OrdinalIgnoreCase) &&
                childName.Contains("_x64__", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record PrerequisiteDefinition(
    string Key,
    string DisplayName,
    string DownloadUrl,
    string FileName,
    string SilentArguments,
    Func<bool> IsInstalled);

public sealed record PrerequisiteDownloadProgress(
    PrerequisiteDefinition Prerequisite,
    long DownloadedBytes,
    long? TotalBytes,
    int Percent);

public sealed record PrerequisiteInstallResult(
    bool Success,
    bool RequiresRestart,
    string? ErrorMessage);
