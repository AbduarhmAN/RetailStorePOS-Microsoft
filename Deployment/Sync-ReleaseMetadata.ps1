param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$metadata = & (Join-Path $PSScriptRoot "Get-ReleaseMetadata.ps1")

try {
    Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop
}
catch {
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
}

function Save-Xml([xml]$Document, [string]$Path) {
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.OmitXmlDeclaration = $false

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Render-PolicyTemplate([string]$TemplatePath, [string]$OutputPath) {
    $rendered = Get-Content -Path $TemplatePath -Raw
    $rendered = $rendered.Replace("{{APP_VERSION}}", $metadata.Version)
    $rendered = $rendered.Replace("{{POLICY_EFFECTIVE_DATE}}", $metadata.PolicyEffectiveDate)
    $rendered = $rendered.Replace("{{POLICY_LAST_UPDATED_DATE}}", $metadata.PolicyLastUpdatedDate)
    $rendered = $rendered.Replace("{{SUPPORT_EMAIL}}", $metadata.SupportEmail)
    $rendered = $rendered.Replace("{{PRIVACY_CONTACT_EMAIL}}", $metadata.PrivacyContactEmail)

    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not (Test-Path $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    Set-Content -Path $OutputPath -Value $rendered -NoNewline
}

function New-CanvasBitmap([int]$Width, [int]$Height) {
    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $bitmap.MakeTransparent()
    return $bitmap
}

function Save-Png([System.Drawing.Bitmap]$Bitmap, [string]$Path) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Write-ScaledSquareIcon([System.Drawing.Bitmap]$SourceBitmap, [string]$Path, [int]$Size) {
    $canvas = New-CanvasBitmap -Width $Size -Height $Size
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($SourceBitmap, 0, 0, $Size, $Size)
        Save-Png -Bitmap $canvas -Path $Path
    }
    finally {
        $graphics.Dispose()
        $canvas.Dispose()
    }
}

function Write-CenteredIconCanvas([System.Drawing.Bitmap]$SourceBitmap, [string]$Path, [int]$CanvasWidth, [int]$CanvasHeight, [double]$Scale = 0.62) {
    $canvas = New-CanvasBitmap -Width $CanvasWidth -Height $CanvasHeight
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        $iconSize = [Math]::Min(
            [int]([Math]::Round($CanvasWidth * $Scale)),
            [int]([Math]::Round($CanvasHeight * $Scale)))
        $x = [int](($CanvasWidth - $iconSize) / 2)
        $y = [int](($CanvasHeight - $iconSize) / 2)

        $graphics.DrawImage($SourceBitmap, $x, $y, $iconSize, $iconSize)
        Save-Png -Bitmap $canvas -Path $Path
    }
    finally {
        $graphics.Dispose()
        $canvas.Dispose()
    }
}

function Sync-IconAssets([string]$SourceIconPath, [string]$AssetsDirectory) {
    if (-not (Test-Path $SourceIconPath)) {
        throw "Shared icon source was not found at '$SourceIconPath'."
    }

    if (-not (Test-Path $AssetsDirectory)) {
        New-Item -ItemType Directory -Path $AssetsDirectory | Out-Null
    }

    $icon = New-Object System.Drawing.Icon($SourceIconPath)
    $sourceBitmap = $icon.ToBitmap()

    try {
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "Logo.png") -Size 128
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "StoreLogo.png") -Size 50
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "Square150x150Logo.scale-200.png") -Size 300
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "Square44x44Logo.scale-200.png") -Size 88
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "Square44x44Logo.targetsize-24_altform-unplated.png") -Size 24
        Write-ScaledSquareIcon -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "LockScreenLogo.scale-200.png") -Size 96
        Write-CenteredIconCanvas -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "Wide310x150Logo.scale-200.png") -CanvasWidth 620 -CanvasHeight 300
        Write-CenteredIconCanvas -SourceBitmap $sourceBitmap -Path (Join-Path $AssetsDirectory "SplashScreen.scale-200.png") -CanvasWidth 620 -CanvasHeight 300
        $destPath = Join-Path $AssetsDirectory "app_icon.ico"
        if ((Resolve-Path $SourceIconPath).Path -ne (Resolve-Path $destPath -ErrorAction SilentlyContinue).Path) {
            Copy-Item -Path $SourceIconPath -Destination $destPath -Force
        }
    }
    finally {
        $sourceBitmap.Dispose()
        $icon.Dispose()
    }
}

$appManifestPath = Join-Path $metadata.RepoRoot "Nexill.RetailStorePOS\app.manifest"
[xml]$appManifest = Get-Content -Path $appManifestPath -Raw
$appNs = New-Object System.Xml.XmlNamespaceManager($appManifest.NameTable)
$appNs.AddNamespace("asmv1", "urn:schemas-microsoft-com:asm.v1")
$assemblyIdentity = $appManifest.SelectSingleNode("/asmv1:assembly/asmv1:assemblyIdentity", $appNs)
if (-not $assemblyIdentity) {
    throw "Could not locate assemblyIdentity in '$appManifestPath'."
}

$assemblyIdentity.SetAttribute("version", $metadata.AssemblyVersion)
Save-Xml -Document $appManifest -Path $appManifestPath

$packageManifestPath = Join-Path $metadata.RepoRoot "Nexill.RetailStorePOS\Package.appxmanifest"
[xml]$packageManifest = Get-Content -Path $packageManifestPath -Raw
$packageNs = New-Object System.Xml.XmlNamespaceManager($packageManifest.NameTable)
$packageNs.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$packageNs.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")

$identity = $packageManifest.SelectSingleNode("/appx:Package/appx:Identity", $packageNs)
if (-not $identity) {
    throw "Could not locate Identity in '$packageManifestPath'."
}

$identity.SetAttribute("Name", $metadata.WinUiPackageIdentityName)
$identity.SetAttribute("Publisher", $metadata.WinUiPackagePublisher)
$identity.SetAttribute("Version", $metadata.AssemblyVersion)

$displayName = $packageManifest.SelectSingleNode("/appx:Package/appx:Properties/appx:DisplayName", $packageNs)
$publisherDisplayName = $packageManifest.SelectSingleNode("/appx:Package/appx:Properties/appx:PublisherDisplayName", $packageNs)
$visualElements = $packageManifest.SelectSingleNode("/appx:Package/appx:Applications/appx:Application/uap:VisualElements", $packageNs)

if (-not $displayName -or -not $publisherDisplayName -or -not $visualElements) {
    throw "Could not locate one or more metadata nodes in '$packageManifestPath'."
}

$displayName.InnerText = $metadata.WinUiPackageDisplayName
$publisherDisplayName.InnerText = $metadata.BrandName
$visualElements.SetAttribute("DisplayName", $metadata.WinUiPackageDisplayName)
$visualElements.SetAttribute("Description", $metadata.WinUiPackageDescription)

Save-Xml -Document $packageManifest -Path $packageManifestPath

Render-PolicyTemplate -TemplatePath $metadata.LicenseFilePath -OutputPath $metadata.RenderedLicenseFilePath
Sync-IconAssets -SourceIconPath $metadata.AppIconPath -AssetsDirectory $metadata.WinUiAssetsPath

if (-not $Quiet) {
    Write-Host "Synchronized WinUI manifest metadata to version $($metadata.Version)." -ForegroundColor Green
}
