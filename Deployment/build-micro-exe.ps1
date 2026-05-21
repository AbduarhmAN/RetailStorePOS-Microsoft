param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath
$artifactRoot = Join-Path $metadata.ArtifactsRoot "micro-exe"
$publishOutputPath = Join-Path $artifactRoot "$($metadata.Version)\win-x64"
$runtimeIdentifier = "win-x64"

if (-not (Test-Path $publishOutputPath)) {
    New-Item -ItemType Directory -Path $publishOutputPath -Force | Out-Null
}

Write-Host "Publishing $($metadata.AppName) $($metadata.Version) as an ultra-lightweight, framework-dependent Single-File EXE..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c $Configuration `
    -r $runtimeIdentifier `
    -p:Platform=x64 `
    -p:Version=$($metadata.Version) `
    -p:AssemblyVersion=$($metadata.AssemblyVersion) `
    -p:FileVersion=$($metadata.AssemblyVersion) `
    -p:InformationalVersion=$($metadata.Version) `
    -p:WindowsPackageType=None `
    -p:EnableMsixTooling=true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=true `
    --self-contained false `
    -o $publishOutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Micro EXE publish failed. See the MSBuild error output above for the root cause."
}

$singleFileExePath = Join-Path $publishOutputPath $metadata.WinUiExecutableName
if (-not (Test-Path $singleFileExePath)) {
    throw "Expected single-file executable was not found at '$singleFileExePath'."
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Micro-EXE successfully built!" -ForegroundColor Green
Write-Host "Output path: $singleFileExePath" -ForegroundColor Cyan
Write-Host "NOTE: This executable is FRAMEWORK-DEPENDENT." -ForegroundColor Yellow
Write-Host "It is extremely small, but whoever runs it MUST have the .NET 8 Runtime installed on their computer." -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
