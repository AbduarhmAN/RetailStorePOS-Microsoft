$manifestPath = "e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Package.appxmanifest"
$debugName = "Nexill.RetailStore.Debug"

if (Test-Path $manifestPath) {
    $content = Get-Content $manifestPath -Raw
    if ($content -match '<Identity Name="Nexill.RetailStore"') {
        $content = $content -replace '<Identity Name="Nexill.RetailStore"', "<Identity Name=`"$debugName`""
        Set-Content $manifestPath $content
        Write-Host "SUCCESS: Package identity reset to Debug mode." -ForegroundColor Green
    } else {
        Write-Host "NOTE: Package was already in Debug mode or Identity not found." -ForegroundColor Yellow
    }
} else {
    Write-Host "ERROR: Could not find Package.appxmanifest at $manifestPath" -ForegroundColor Red
}

Pause
