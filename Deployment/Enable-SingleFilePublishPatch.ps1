param(
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    throw "ProjectPath is required."
}

$projectFullPath = [System.IO.Path]::GetFullPath($ProjectPath)
$fragmentPath = Join-Path $PSScriptRoot "singlefile-msbuild-fragment.xml"

if (-not (Test-Path $projectFullPath)) {
    throw "Project file was not found at '$projectFullPath'."
}

if (-not (Test-Path $fragmentPath)) {
    throw "Single-file fragment was not found at '$fragmentPath'."
}

$beginMarker = "<!-- BEGIN CODEX_SINGLEFILE_PATCH -->"
$endMarker = "<!-- END CODEX_SINGLEFILE_PATCH -->"
$projectContent = Get-Content -Path $projectFullPath -Raw

if ($projectContent.Contains($beginMarker)) {
    return
}

$fragmentContent = Get-Content -Path $fragmentPath -Raw
$closingTag = "</Project>"
$closingIndex = $projectContent.LastIndexOf($closingTag, [System.StringComparison]::Ordinal)

if ($closingIndex -lt 0) {
    throw "Could not locate closing </Project> tag in '$projectFullPath'."
}

$updatedContent = $projectContent.Insert($closingIndex, [Environment]::NewLine + $fragmentContent + [Environment]::NewLine)
Set-Content -Path $projectFullPath -Value $updatedContent -NoNewline
