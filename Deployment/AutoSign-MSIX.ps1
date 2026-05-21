param(
    [string]$FilePath,
    [switch]$AutoInstall,
    [switch]$NoPrompt
)

# 1. Check for Admin Rights and Self-Elevate
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($AutoInstall) { $arguments += " -AutoInstall" }
    if ($NoPrompt) { $arguments += " -NoPrompt" }
    Start-Process powershell.exe $arguments -Verb RunAs
    exit
}

# 2. Configuration
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$pfx = "e:\Projects\Retail_Store\V\1.3.3\Deployment\Certification\NexillTest.pfx"
$password = "nexill"
$packageDir = "E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\AppPackages"

# 3. Find the package to sign
if ($FilePath) {
    $bundle = Get-Item -Path $FilePath
} else {
    Write-Host "Searching for latest MSIX package..." -ForegroundColor Cyan
    $bundle = Get-ChildItem -Path $packageDir -Include "*.msixbundle", "*.msix" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if ($null -eq $bundle -or -not $bundle.Exists) {
    Write-Host "ERROR: Package not found." -ForegroundColor Red
    if (-not $NoPrompt) { pause }
    exit
}

Write-Host "Found: $($bundle.FullName)" -ForegroundColor Green

# 4. Sign the file
Write-Host "Signing package..." -ForegroundColor Cyan
& $signtool sign /f $pfx /p $password /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $bundle.FullName

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Package is signed and ready!" -ForegroundColor Green
    
    $choice = "N"
    if ($AutoInstall) {
        $choice = "Y"
    } elseif (-not $NoPrompt) {
        $choice = Read-Host "Do you want to install this package now? (Y/N)"
    }

    if ($choice -eq 'Y' -or $choice -eq 'y') {
        $oldPkg = Get-AppxPackage *RetailStore*
        if ($oldPkg) {
             Write-Host "Removing old version..." -ForegroundColor Yellow
             Remove-AppxPackage $oldPkg.PackageFullName
        }
        
        Write-Host "Installing new version..." -ForegroundColor Cyan
        Add-AppxPackage -Path $bundle.FullName
        Write-Host "Installation complete!" -ForegroundColor Green
    }
} else {
    Write-Host "ERROR: Signing failed." -ForegroundColor Red
}

if (-not $NoPrompt) {
    pause
}
