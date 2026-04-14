param(
    [Parameter(Mandatory)]
    [ValidateSet(
        "test-dotnet-desktop-runtime",
        "test-vc-redist-x64",
        "test-windows-app-runtime",
        "download"
    )]
    [string]$Action,

    [string]$MinimumVersion,
    [string]$PackageNamePattern,
    [string]$Uri,
    [string]$OutFile
)

Set-StrictMode -Version Latest

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Exit-WithState {
    param(
        [Parameter(Mandatory)]
        [bool]$IsAvailable
    )

    if ($IsAvailable) {
        exit 0
    }

    exit 1
}

switch ($Action) {
    "test-dotnet-desktop-runtime" {
        if ([string]::IsNullOrWhiteSpace($MinimumVersion)) {
            throw "MinimumVersion is required for .NET Desktop Runtime detection."
        }

        $minimum = [version]$MinimumVersion
        $runtimeRoot = Join-Path ${env:ProgramFiles} "dotnet\shared\Microsoft.WindowsDesktop.App"

        if (-not (Test-Path $runtimeRoot)) {
            Exit-WithState -IsAvailable $false
        }

        $installed = Get-ChildItem -Path $runtimeRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object {
                try {
                    [version]$_.Name -ge $minimum
                }
                catch {
                    $false
                }
            } |
            Select-Object -First 1

        Exit-WithState -IsAvailable ($null -ne $installed)
    }

    "test-vc-redist-x64" {
        $candidateKeys = @(
            "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
            "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
        )

        $installed = $false

        foreach ($candidateKey in $candidateKeys) {
            $runtimeInfo = Get-ItemProperty -Path $candidateKey -ErrorAction SilentlyContinue
            if ($null -ne $runtimeInfo -and $runtimeInfo.Installed -eq 1) {
                $installed = $true
                break
            }
        }

        Exit-WithState -IsAvailable $installed
    }

    "test-windows-app-runtime" {
        if ([string]::IsNullOrWhiteSpace($MinimumVersion)) {
            throw "MinimumVersion is required for Windows App Runtime detection."
        }

        if ([string]::IsNullOrWhiteSpace($PackageNamePattern)) {
            throw "PackageNamePattern is required for Windows App Runtime detection."
        }

        $minimum = [version]$MinimumVersion
        $packages = @()

        try {
            $packages += Get-AppxPackage -Name $PackageNamePattern -ErrorAction SilentlyContinue
        }
        catch {
        }

        try {
            $packages += Get-AppxPackage -AllUsers -Name $PackageNamePattern -ErrorAction SilentlyContinue
        }
        catch {
        }

        $installed = $packages |
            Where-Object {
                $_ -ne $null -and
                $_.Architecture.ToString() -eq "X64" -and
                ([version]$_.Version) -ge $minimum
            } |
            Sort-Object Version -Descending |
            Select-Object -First 1

        Exit-WithState -IsAvailable ($null -ne $installed)
    }

    "download" {
        if ([string]::IsNullOrWhiteSpace($Uri)) {
            throw "Uri is required for download."
        }

        if ([string]::IsNullOrWhiteSpace($OutFile)) {
            throw "OutFile is required for download."
        }

        $targetDirectory = Split-Path -Parent $OutFile
        if (-not [string]::IsNullOrWhiteSpace($targetDirectory) -and -not (Test-Path $targetDirectory)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }

        Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing
        exit 0
    }
}
