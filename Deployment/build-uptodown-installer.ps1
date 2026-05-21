param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath
$innoScriptPath = Join-Path $PSScriptRoot "setup_script.iss"

Write-Host "Publishing $($metadata.AppName) $($metadata.Version) for Uptodown/Inno Setup..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=x64 `
    -p:Version=$($metadata.Version) `
    -p:AssemblyVersion=$($metadata.AssemblyVersion) `
    -p:FileVersion=$($metadata.AssemblyVersion) `
    -p:InformationalVersion=$($metadata.Version) `
    -p:WindowsPackageType=None `
    --self-contained true

if ($LASTEXITCODE -ne 0) {
    throw "WinUI publish failed."
}

$requiredPublishAssets = @(
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

$missingPublishAssets = $requiredPublishAssets |
    Where-Object { -not (Test-Path (Join-Path $metadata.PublishOutputPath $_)) }

if ($missingPublishAssets.Count -gt 0) {
    throw "Publish output is missing required runtime assets: $($missingPublishAssets -join ', ')"
}

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
    Where-Object { $_.DisplayName -like "Inno Setup*" -and -not [string]::IsNullOrWhiteSpace($_.InstallLocation) } |
    Select-Object -First 1 -ExpandProperty InstallLocation

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

Write-Host "Building Inno Setup package..." -ForegroundColor Cyan
$isccArguments = @(
    "/DMyAppName=$($metadata.AppName)",
    "/DMyAppVersion=$($metadata.Version)",
    "/DMyAppBinaryVersion=$($metadata.AssemblyVersion)",
    "/DMyAppPublisher=$($metadata.BrandName)",
    "/DMyAppExeName=$($metadata.WinUiExecutableName)",
    "/DMyOutputBaseFilename=$($metadata.SetupBaseFilename)",
    "/DMySetupIconFile=$($metadata.AppIconPath)",
    "/DMyLicenseFile=$($metadata.RenderedLicenseFilePath)",
    "/DMyWizardImageFile=$($metadata.WizardImagePath)",
    "/DMyWizardSmallImageFile=$($metadata.WizardSmallImagePath)",
    "/DMyPublishDir=$($metadata.PublishOutputPath)",
    "/O$($repoRoot)\artifacts\installer",
    $innoScriptPath
)
& $isccPath @isccArguments

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup build failed."
}

Write-Host "Installer output is under artifacts\\installer." -ForegroundColor Green
