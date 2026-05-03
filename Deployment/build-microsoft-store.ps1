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
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxBundle=Always `
    -p:AppxPackageSigningEnabled=false `
    -p:GenerateAppxPackageOnBuild=true

if ($LASTEXITCODE -ne 0) {
    throw "Microsoft Store (.msixupload) publisher crashed."
}

# --- CUSTOM MSIXUPLOAD GENERATOR ---
# Since mspdbcmf.exe is missing, we zip the .msix into an .msixupload manually
$appPackagesDir = Join-Path (Split-Path $projectPath) "AppPackages"
$latestTestDir = Get-ChildItem -Path $appPackagesDir -Directory -Filter "*_Test" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestTestDir) {
    $msixFile = Get-ChildItem -Path $latestTestDir.FullName -Filter "*.msixbundle" | Select-Object -First 1
    if ($msixFile) {
        $uploadName = $msixFile.Name.Replace(".msixbundle", ".msixupload")
        $uploadPath = Join-Path $appPackagesDir $uploadName
        $tempZipPath = Join-Path $appPackagesDir "temp_upload.zip"
        
        Write-Host "Bypassing missing C++ Build Tools. Generating custom .msixupload archive..." -ForegroundColor Yellow
        if (Test-Path $tempZipPath) { Remove-Item $tempZipPath -Force }
        if (Test-Path $uploadPath) { Remove-Item $uploadPath -Force }
        
        Compress-Archive -Path $msixFile.FullName -DestinationPath $tempZipPath -Force
        Rename-Item -Path $tempZipPath -NewName $uploadName -Force
        
        Write-Host "Microsoft Store package successfully built!" -ForegroundColor Green
        Write-Host "Your .msixupload file has been securely output: $uploadPath" -ForegroundColor Cyan
        exit 0
    }
}

throw "Failed to locate generated .msix file to package into an .msixupload container."
