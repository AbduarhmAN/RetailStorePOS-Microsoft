Set-StrictMode -Version Latest

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Get-RetailStorePOSPrerequisiteManifest {
    $dotNetDesktopRuntime = [pscustomobject]@{
        Key = "DotNetDesktopRuntime"
        DisplayName = ".NET Desktop Runtime 8 (x64)"
        MinimumVersion = "8.0.0"
        DisplayVersion = "8.0.25"
        Filename = "windowsdesktop-runtime-8.0.25-win-x64.exe"
        Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.25/windowsdesktop-runtime-8.0.25-win-x64.exe"
        SilentArguments = "/install /quiet /norestart"
    }

    $windowsAppRuntime = [pscustomobject]@{
        Key = "WindowsAppRuntime"
        DisplayName = "Windows App Runtime 1.8.5 (x64)"
        MinimumVersion = "1.8.260209005"
        DisplayVersion = "1.8.5 (1.8.260209005)"
        Filename = "WindowsAppRuntimeInstall-x64.exe"
        Url = "https://aka.ms/windowsappsdk/1.8/1.8.5/windowsappruntimeinstall-x64.exe"
        SilentArguments = "--quiet"
        PackageNamePattern = "Microsoft.WindowsAppRuntime.1.8*"
    }

    $visualCppRedistributable = [pscustomobject]@{
        Key = "VisualCppRedistributable"
        DisplayName = "Microsoft Visual C++ Redistributable 2015-2022 (x64)"
        MinimumVersion = "14.0.0.0"
        DisplayVersion = "Latest supported"
        Filename = "vc_redist.x64.exe"
        Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
        SilentArguments = "/install /quiet /norestart"
    }

    [pscustomobject]@{
        Architecture = "x64"
        DotNetDesktopRuntime = $dotNetDesktopRuntime
        WindowsAppRuntime = $windowsAppRuntime
        VisualCppRedistributable = $visualCppRedistributable
        Prerequisites = @(
            $dotNetDesktopRuntime,
            $windowsAppRuntime,
            $visualCppRedistributable
        )
    }
}

function Save-RetailStorePOSPrerequisiteInstallers {
    param(
        [Parameter(Mandatory)]
        [string]$DestinationDirectory,

        [switch]$Force
    )

    if (-not (Test-Path $DestinationDirectory)) {
        New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    }

    $manifest = Get-RetailStorePOSPrerequisiteManifest
    $downloadedPrerequisites = @()

    foreach ($prerequisite in $manifest.Prerequisites) {
        $targetPath = Join-Path $DestinationDirectory $prerequisite.Filename

        if ((Test-Path $targetPath) -and -not $Force) {
            Write-Host "Using cached prerequisite installer: $($prerequisite.DisplayName)" -ForegroundColor DarkGray
        }
        else {
            Write-Host "Downloading prerequisite installer: $($prerequisite.DisplayName)" -ForegroundColor Cyan
            Invoke-WebRequest -Uri $prerequisite.Url -OutFile $targetPath -UseBasicParsing
        }

        if (-not (Test-Path $targetPath)) {
            throw "Prerequisite download failed for '$($prerequisite.DisplayName)'."
        }

        $packageNamePattern = $null
        if ($prerequisite.PSObject.Properties.Match("PackageNamePattern").Count -gt 0) {
            $packageNamePattern = $prerequisite.PackageNamePattern
        }

        $downloadedPrerequisites += [pscustomobject]@{
            Key = $prerequisite.Key
            DisplayName = $prerequisite.DisplayName
            MinimumVersion = $prerequisite.MinimumVersion
            DisplayVersion = $prerequisite.DisplayVersion
            Filename = $prerequisite.Filename
            Url = $prerequisite.Url
            SilentArguments = $prerequisite.SilentArguments
            PackageNamePattern = $packageNamePattern
            SourcePath = (Resolve-Path $targetPath).Path
        }
    }

    [pscustomobject]@{
        DestinationDirectory = (Resolve-Path $DestinationDirectory).Path
        Prerequisites = $downloadedPrerequisites
    }
}

if ($MyInvocation.InvocationName -ne ".") {
    Get-RetailStorePOSPrerequisiteManifest
}
