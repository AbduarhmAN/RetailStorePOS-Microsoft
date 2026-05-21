Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$nodeExe = "C:\Program Files\nodejs\node.exe"
$npmCli = "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js"
$cacheDir = Join-Path $repoRoot ".tmp\npm-cache"

if (-not (Test-Path $nodeExe)) {
    throw "Node.js was not found at '$nodeExe'."
}

if (-not (Test-Path $npmCli)) {
    throw "npm-cli.js was not found at '$npmCli'."
}

New-Item -ItemType Directory -Force $cacheDir | Out-Null
$env:npm_config_cache = $cacheDir

Push-Location $repoRoot
try {
    & $nodeExe $npmCli install supabase --save-dev
} finally {
    Pop-Location
}

$shimPath = Join-Path $repoRoot "node_modules\.bin\supabase.ps1"
$exePath = Join-Path $repoRoot "node_modules\supabase\bin\supabase.exe"

if ((Test-Path $shimPath) -or (Test-Path $exePath)) {
    Write-Host "Local Supabase CLI installed successfully."
    exit 0
}

throw "Supabase package install finished but no runnable CLI was found."
