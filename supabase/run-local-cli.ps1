param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $CliArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$shimPath = Join-Path $repoRoot "node_modules\.bin\supabase.ps1"
$exePath = Join-Path $repoRoot "node_modules\supabase\bin\supabase.exe"

if (Test-Path $shimPath) {
    & $shimPath @CliArgs
    exit $LASTEXITCODE
}

if (Test-Path $exePath) {
    & $exePath @CliArgs
    exit $LASTEXITCODE
}

throw "Local Supabase CLI was not found. Run '.\supabase\setup-local-cli.ps1' first."
