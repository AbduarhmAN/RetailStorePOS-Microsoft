param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
# Read all properties from Directory.Build.props
$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")

if (-not $metadata.StorePackageVersionIsValid) {
    throw $metadata.StorePackageVersionValidationMessage
}

# Push Publisher & Identity tags directly into the Microsoft Package.appxmanifest
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath

# 1. Clean the project to ensure fresh resource indexing
Write-Host "Cleaning project to prevent PRI indexing conflicts..." -ForegroundColor Cyan
dotnet clean $projectPath --configuration $Configuration

# 2. Publish and Package
Write-Host "Building and Packaging MSIX (Microsoft Way)..." -ForegroundColor Cyan
dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=x64 `
    -p:Version=$($metadata.Version) `
    -p:AssemblyVersion=$($metadata.AssemblyVersion) `
    -p:FileVersion=$($metadata.AssemblyVersion) `
    -p:InformationalVersion=$($metadata.Version) `
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
        
        # Auto-sign the bundle before zipping
        # Auto-sign the bundle before zipping
        Write-Host "Auto-signing package..." -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot "AutoSign-MSIX.ps1") -FilePath $msixFile.FullName -NoPrompt

        Compress-Archive -Path $msixFile.FullName -DestinationPath $tempZipPath -Force
        Rename-Item -Path $tempZipPath -NewName $uploadName -Force
        
        Write-Host "Microsoft Store package successfully built!" -ForegroundColor Green
        Write-Host "Your .msixupload file has been securely output: $uploadPath" -ForegroundColor Cyan
        exit 0
    }
}

throw "Failed to locate generated .msix file to package into an .msixupload container."
