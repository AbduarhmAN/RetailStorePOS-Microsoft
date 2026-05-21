param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")
& (Join-Path $PSScriptRoot "Sync-ReleaseMetadata.ps1") -Quiet

$projectPath = $metadata.WinUiProjectPath
$artifactRoot = Join-Path $metadata.ArtifactsRoot "vt-singlefile"
$publishOutputPath = Join-Path $artifactRoot "$($metadata.Version)\win-x64"
$runtimeIdentifier = "win-x64"

# Configuration for Identity Switching
$manifestPath = Join-Path (Split-Path $projectPath) "Package.appxmanifest"

$prodName = "Nexill.RetailStore"
$debugName = "Nexill.RetailStore.Debug"

function Set-PackageIdentity {
    param([string]$Name)
    if (Test-Path $manifestPath) {
        $content = Get-Content $manifestPath -Raw
        $content = $content -replace '<Identity Name="[^"]+"', "<Identity Name=`"$Name`""
        Set-Content $manifestPath $content
        Write-Host "Package identity set to: $Name" -ForegroundColor Gray
    }
}

try {
    # 1. Force Production Identity for Build (Ensure it is Real)
    Write-Host "Verifying Identity: Forcing Production Name ($prodName)..." -ForegroundColor Yellow
    Set-PackageIdentity -Name $prodName

    # 2. Existing Build Logic
    Write-Host "Publishing $($metadata.AppName) $($metadata.Version) as a single-file VT artifact..." -ForegroundColor Cyan

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
        -p:PublishTrimmed=false `
        --self-contained true `
        -o $publishOutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "Single-file VT publish failed. See the MSBuild error output above for the root cause."
    }

    $singleFileExePath = Join-Path $publishOutputPath $metadata.WinUiExecutableName
    if (-not (Test-Path $singleFileExePath)) {
        throw "Expected single-file executable was not found at '$singleFileExePath'."
    }

    $hash = Get-FileHash -Algorithm SHA256 -Path $singleFileExePath
    $hashFilePath = "$singleFileExePath.sha256.txt"
    $hashFileContent = @(
        "File: $singleFileExePath"
        "SHA256: $($hash.Hash)"
    ) -join [Environment]::NewLine

    Set-Content -Path $hashFilePath -Value $hashFileContent -NoNewline

    Write-Host "VT single-file artifact is ready:" -ForegroundColor Green
    Write-Host "  EXE : $singleFileExePath" -ForegroundColor Cyan
    Write-Host "  HASH: $hashFilePath" -ForegroundColor Cyan
    Write-Host "Upload the raw EXE to VirusTotal, not an installer wrapper or extracted temp file." -ForegroundColor Yellow
}
catch {
    Write-Host "BUILD ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    # 3. Always Switch Back to Debug Identity
    Write-Host "Reverting to Debug Identity..." -ForegroundColor Yellow
    Set-PackageIdentity -Name $debugName
}
