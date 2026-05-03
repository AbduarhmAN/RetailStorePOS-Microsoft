$ErrorActionPreference = 'SilentlyContinue'

function Ensure-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isAdmin) {
        if (-not $PSCommandPath) {
            throw "Save this script to a .ps1 file before running it."
        }

        Write-Host "Requesting administrator permission..." -ForegroundColor Yellow
        Start-Process powershell.exe -Verb RunAs -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', "`"$PSCommandPath`""
        )
        exit
    }
}

function Add-Section {
    param(
        [System.Collections.Generic.List[string]]$Report,
        [string]$Title
    )
    $Report.Add("")
    $Report.Add("=== $Title ===")
}

function Add-Lines {
    param(
        [System.Collections.Generic.List[string]]$Report,
        [object[]]$Lines
    )
    foreach ($line in $Lines) {
        $Report.Add([string]$line)
    }
}

Ensure-Admin

$report = [System.Collections.Generic.List[string]]::new()
$outFile = Join-Path $env:USERPROFILE 'Desktop\discord-diagnostic.txt'

Add-Lines -Report $report -Lines @(
    "Discord diagnostic",
    "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "Computer: $env:COMPUTERNAME",
    "User: $env:USERNAME"
)

Add-Section -Report $report -Title "Current User Appx Packages"
$currentUserPackages = Get-AppxPackage -Name '*Discord*'
if ($currentUserPackages) {
    foreach ($pkg in $currentUserPackages) {
        Add-Lines -Report $report -Lines @(
            "Name: $($pkg.Name)",
            "PackageFullName: $($pkg.PackageFullName)",
            "InstallLocation: $($pkg.InstallLocation)",
            "Status: $($pkg.Status)",
            "---"
        )
    }
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "All Users Appx Packages"
$allUserPackages = Get-AppxPackage -AllUsers -Name '*Discord*'
if ($allUserPackages) {
    foreach ($pkg in $allUserPackages) {
        Add-Lines -Report $report -Lines @(
            "Name: $($pkg.Name)",
            "PackageFullName: $($pkg.PackageFullName)",
            "InstallLocation: $($pkg.InstallLocation)",
            "Status: $($pkg.Status)",
            "---"
        )
    }
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "Current User Package Repository"
$hkcuRepo = Get-ChildItem 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages' |
    Where-Object { $_.PSChildName -like '*Discord*' }

if ($hkcuRepo) {
    Add-Lines -Report $report -Lines ($hkcuRepo | Select-Object -ExpandProperty PSChildName)
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "Machine Package Repository"
$hklmRepo = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications' |
    Where-Object { $_.PSChildName -like '*Discord*' }

if ($hklmRepo) {
    Add-Lines -Report $report -Lines ($hklmRepo | Select-Object -ExpandProperty PSChildName)
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "Start Menu Apps"
$startApps = Get-StartApps | Where-Object { $_.Name -like '*Discord*' }
if ($startApps) {
    foreach ($app in $startApps) {
        Add-Lines -Report $report -Lines @(
            "Name: $($app.Name)",
            "AppID: $($app.AppID)",
            "---"
        )
    }
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "Installed Apps Registry Entries"
$uninstallPaths = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
)

$installedApps = Get-ItemProperty $uninstallPaths |
    Where-Object { $_.DisplayName -like '*Discord*' }

if ($installedApps) {
    foreach ($app in $installedApps) {
        Add-Lines -Report $report -Lines @(
            "DisplayName: $($app.DisplayName)",
            "DisplayVersion: $($app.DisplayVersion)",
            "Publisher: $($app.Publisher)",
            "UninstallString: $($app.UninstallString)",
            "---"
        )
    }
} else {
    Add-Lines -Report $report -Lines @("None found")
}

Add-Section -Report $report -Title "User Discord Folders"
$userPaths = @(
    "$env:LOCALAPPDATA\Discord",
    "$env:APPDATA\discord",
    "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Discord Inc"
)

foreach ($path in $userPaths) {
    if (Test-Path $path) {
        Add-Lines -Report $report -Lines @("$path : EXISTS")
    } else {
        Add-Lines -Report $report -Lines @("$path : MISSING")
    }
}

$report | Set-Content -LiteralPath $outFile -Encoding UTF8
$report | ForEach-Object { Write-Host $_ }

Write-Host ""
Write-Host "Saved report to: $outFile" -ForegroundColor Green
