param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$runtimeIdentifier = "win-x64"
$vtBuildScriptPath = Join-Path $PSScriptRoot "build-vt-singlefile.ps1"
$innoScriptPath = Join-Path $PSScriptRoot "setup_singlefile.iss"
$singleFileOutputPath = Join-Path (Join-Path $metadata.ArtifactsRoot "vt-singlefile") "$($metadata.Version)\$runtimeIdentifier"
$singleFileExePath = Join-Path $singleFileOutputPath $metadata.WinUiExecutableName
$singleFileSetupBaseFilename = "$($metadata.SetupBaseFilename)-SingleFile"

Write-Host "Building raw single-file artifact for installer packaging..." -ForegroundColor Cyan
& $vtBuildScriptPath -Configuration $Configuration

if (-not (Test-Path $singleFileExePath)) {
    throw "Expected single-file executable was not found at '$singleFileExePath'."
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

Write-Host "Building single-file installer..." -ForegroundColor Cyan
$isccArguments = @(
    "/DMyAppName=$($metadata.AppName)",
    "/DMyAppVersion=$($metadata.Version)",
    "/DMyAppPublisher=$($metadata.BrandName)",
    "/DMyAppExeName=$($metadata.WinUiExecutableName)",
    "/DMyAppExeSourcePath=$singleFileExePath",
    "/DMyOutputBaseFilename=$singleFileSetupBaseFilename",
    "/DMySetupIconFile=$($metadata.AppIconPath)",
    "/DMyLicenseFile=$($metadata.RenderedLicenseFilePath)",
    "/DMyWizardImageFile=$($metadata.WizardImagePath)",
    "/DMyWizardSmallImageFile=$($metadata.WizardSmallImagePath)",
    "/O$($metadata.ArtifactsRoot)\installer-singlefile",
    $innoScriptPath
)
& $isccPath @isccArguments

if ($LASTEXITCODE -ne 0) {
    throw "Single-file installer build failed."
}

Write-Host "Single-file installer output is under artifacts\\installer-singlefile." -ForegroundColor Green
