param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Get-PrerequisiteManifest.ps1")

function Get-InnoSetupCompilerPath {
    $innoCandidates = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $registryKeys = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    $registryInstallLocation = Get-ItemProperty $registryKeys -ErrorAction SilentlyContinue |
        ForEach-Object {
            $displayNameProperty = $_.PSObject.Properties["DisplayName"]
            $installLocationProperty = $_.PSObject.Properties["InstallLocation"]

            $displayName = if ($null -ne $displayNameProperty) { [string]$displayNameProperty.Value } else { "" }
            $installLocation = if ($null -ne $installLocationProperty) { [string]$installLocationProperty.Value } else { "" }

            if ($displayName -like "Inno Setup*" -and -not [string]::IsNullOrWhiteSpace($installLocation)) {
                $installLocation
            }
        } |
        Select-Object -First 1

    if (-not [string]::IsNullOrWhiteSpace($registryInstallLocation)) {
        $innoCandidates += (Join-Path $registryInstallLocation "ISCC.exe")
    }

    $isccPath = $innoCandidates |
        Where-Object { Test-Path $_ } |
        Select-Object -Unique |
        Select-Object -First 1

    if (-not $isccPath) {
        throw "ISCC.exe was not found. Install Inno Setup 6, then rerun this script."
    }

    $isccPath
}

function Assert-PublishAssets {
    param(
        [Parameter(Mandatory)]
        [string]$PublishOutputPath,

        [Parameter(Mandatory)]
        [string]$ExecutableName
    )

    $requiredPublishAssets = @(
        $ExecutableName,
        "Assets\\Logo.png",
        "Assets\\StoreLogo.png",
        "Assets\\BrandLogo.webp",
        "Assets\\Square150x150Logo.scale-200.png",
        "Assets\\Square44x44Logo.scale-200.png",
        "Assets\\Square44x44Logo.targetsize-24_altform-unplated.png",
        "Assets\\Wide310x150Logo.scale-200.png",
        "Assets\\SplashScreen.scale-200.png",
        "Assets\\LockScreenLogo.scale-200.png",
        "Assets\\app_icon.ico"
    )

    $missingPublishAssets = @(
        $requiredPublishAssets |
        Where-Object { -not (Test-Path (Join-Path $PublishOutputPath $_)) }
    )

    if ($missingPublishAssets.Count -gt 0) {
        throw "Publish output is missing required runtime assets: $($missingPublishAssets -join ', ')"
    }
}

function Get-DownloadedPrerequisite {
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$DownloadedPrerequisites,

        [Parameter(Mandatory)]
        [string]$Key
    )

    $downloadedPrerequisite = $DownloadedPrerequisites.Prerequisites |
        Where-Object { $_.Key -eq $Key } |
        Select-Object -First 1

    if (-not $downloadedPrerequisite) {
        throw "Downloaded prerequisite '$Key' was not found in the staged manifest."
    }

    $downloadedPrerequisite
}

$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath
$runtimeIdentifier = "win-x64"
$artifactRoot = Join-Path $metadata.ArtifactsRoot "bootstrap-offline"
$publishOutputPath = Join-Path $artifactRoot "$($metadata.Version)\$runtimeIdentifier\publish"
$prerequisiteStagePath = Join-Path $artifactRoot "$($metadata.Version)\prerequisites"
$innoScriptPath = Join-Path $PSScriptRoot "setup_bootstrap_offline.iss"
$prerequisiteHelperPath = Join-Path $PSScriptRoot "BootstrapPrerequisiteActions.ps1"
$setupBaseFilename = "$($metadata.SetupBaseFilename)-Bootstrap-Offline"
$prerequisiteManifest = Get-RetailStorePOSPrerequisiteManifest
$dotNetDesktopRuntime = $prerequisiteManifest.DotNetDesktopRuntime
$windowsAppRuntime = $prerequisiteManifest.WindowsAppRuntime
$visualCppRedistributable = $prerequisiteManifest.VisualCppRedistributable

if (-not (Test-Path $publishOutputPath)) {
    New-Item -ItemType Directory -Path $publishOutputPath -Force | Out-Null
}

Write-Host "Publishing $($metadata.AppName) $($metadata.Version) as a framework-dependent bootstrap artifact..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c $Configuration `
    -r $runtimeIdentifier `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=false `
    -p:PublishSingleFile=false `
    --self-contained false `
    -o $publishOutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Framework-dependent bootstrap publish failed."
}

Assert-PublishAssets -PublishOutputPath $publishOutputPath -ExecutableName $metadata.WinUiExecutableName

Write-Host "Staging offline prerequisite installers..." -ForegroundColor Cyan
$downloadedPrerequisites = Save-RetailStorePOSPrerequisiteInstallers -DestinationDirectory $prerequisiteStagePath
$downloadedDotNetDesktopRuntime = Get-DownloadedPrerequisite -DownloadedPrerequisites $downloadedPrerequisites -Key "DotNetDesktopRuntime"
$downloadedWindowsAppRuntime = Get-DownloadedPrerequisite -DownloadedPrerequisites $downloadedPrerequisites -Key "WindowsAppRuntime"
$downloadedVisualCppRedistributable = Get-DownloadedPrerequisite -DownloadedPrerequisites $downloadedPrerequisites -Key "VisualCppRedistributable"

$isccPath = Get-InnoSetupCompilerPath

Write-Host "Building offline bootstrap installer..." -ForegroundColor Cyan
$isccArguments = @(
    "/DMyAppName=$($metadata.AppName)",
    "/DMyAppVersion=$($metadata.Version)",
    "/DMyAppPublisher=$($metadata.BrandName)",
    "/DMyAppExeName=$($metadata.WinUiExecutableName)",
    "/DMyOutputBaseFilename=$setupBaseFilename",
    "/DMySetupIconFile=$($metadata.AppIconPath)",
    "/DMyLicenseFile=$($metadata.RenderedLicenseFilePath)",
    "/DMyWizardImageFile=$($metadata.WizardImagePath)",
    "/DMyWizardSmallImageFile=$($metadata.WizardSmallImagePath)",
    "/DMyPublishDir=$publishOutputPath",
    "/DMyPrereqHelperSourcePath=$prerequisiteHelperPath",
    "/DMyDotNetDesktopRuntimeDisplayName=$($dotNetDesktopRuntime.DisplayName)",
    "/DMyDotNetDesktopRuntimeMinimumVersion=$($dotNetDesktopRuntime.MinimumVersion)",
    "/DMyDotNetDesktopRuntimeFilename=$($dotNetDesktopRuntime.Filename)",
    "/DMyDotNetDesktopRuntimeSilentArgs=$($dotNetDesktopRuntime.SilentArguments)",
    "/DMyDotNetDesktopRuntimeSourcePath=$($downloadedDotNetDesktopRuntime.SourcePath)",
    "/DMyWindowsAppRuntimeDisplayName=$($windowsAppRuntime.DisplayName)",
    "/DMyWindowsAppRuntimeMinimumVersion=$($windowsAppRuntime.MinimumVersion)",
    "/DMyWindowsAppRuntimeFilename=$($windowsAppRuntime.Filename)",
    "/DMyWindowsAppRuntimeSilentArgs=$($windowsAppRuntime.SilentArguments)",
    "/DMyWindowsAppRuntimePackageNamePattern=$($windowsAppRuntime.PackageNamePattern)",
    "/DMyWindowsAppRuntimeSourcePath=$($downloadedWindowsAppRuntime.SourcePath)",
    "/DMyVisualCppRedistributableDisplayName=$($visualCppRedistributable.DisplayName)",
    "/DMyVisualCppRedistributableFilename=$($visualCppRedistributable.Filename)",
    "/DMyVisualCppRedistributableSilentArgs=$($visualCppRedistributable.SilentArguments)",
    "/DMyVisualCppRedistributableSourcePath=$($downloadedVisualCppRedistributable.SourcePath)",
    "/O$($metadata.ArtifactsRoot)\installer-bootstrap-offline",
    $innoScriptPath
)

& $isccPath @isccArguments

if ($LASTEXITCODE -ne 0) {
    throw "Offline bootstrap installer build failed."
}

Write-Host "Offline bootstrap installer output is under artifacts\\installer-bootstrap-offline." -ForegroundColor Green
