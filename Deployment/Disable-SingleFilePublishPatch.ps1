param(
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    throw "ProjectPath is required."
}

$projectFullPath = [System.IO.Path]::GetFullPath($ProjectPath)

if (-not (Test-Path $projectFullPath)) {
    throw "Project file was not found at '$projectFullPath'."
}

$beginMarker = "<!-- BEGIN CODEX_SINGLEFILE_PATCH -->"
$endMarker = "<!-- END CODEX_SINGLEFILE_PATCH -->"
$projectContent = Get-Content -Path $projectFullPath -Raw
$startIndex = $projectContent.IndexOf($beginMarker, [System.StringComparison]::Ordinal)

if ($startIndex -lt 0) {
    return
}

$endIndex = $projectContent.IndexOf($endMarker, $startIndex, [System.StringComparison]::Ordinal)

if ($endIndex -lt 0) {
    throw "Found BEGIN marker without matching END marker in '$projectFullPath'."
}

$endIndex += $endMarker.Length

while ($endIndex -lt $projectContent.Length -and ($projectContent[$endIndex] -eq "`r" -or $projectContent[$endIndex] -eq "`n")) {
    $endIndex++
}

$updatedContent = $projectContent.Remove($startIndex, $endIndex - $startIndex)
Set-Content -Path $projectFullPath -Value $updatedContent -NoNewline
