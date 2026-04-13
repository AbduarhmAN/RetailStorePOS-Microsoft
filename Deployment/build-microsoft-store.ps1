param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
# Read all properties from Directory.Build.props
$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
# Push Publisher & Identity tags directly into the Microsoft Package.appxmanifest
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath

Write-Host "Publishing $($metadata.AppName) $($metadata.Version) for the Microsoft Store..." -ForegroundColor Cyan

# Unlike the UpToDown script, we do NOT use -p:WindowsPackageType=None here!
# This allows MSBuild to natively engage the MakeAppx engine and package the app for Partner Center.
dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=x64 `
    -p:UapAppxPackageBuildMode=StoreUpload `
    -p:AppxBundle=Always `
    -p:AppxSymbolPackageEnabled=false

if ($LASTEXITCODE -ne 0) {
    throw "Microsoft Store (.msixupload) publisher crashed."
}

Write-Host "Microsoft Store package successfully built!" -ForegroundColor Green
Write-Host "Your .msixupload file has been securely output into the MSBuild 'AppPackages' folder." -ForegroundColor Cyan
