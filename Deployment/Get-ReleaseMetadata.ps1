param(
    [string]$PropsPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "Directory.Build.props")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PropsPath)) {
    throw "Directory.Build.props was not found at '$PropsPath'."
}

[xml]$props = Get-Content -Path $PropsPath -Raw
$propertyGroup = $props.Project.PropertyGroup | Select-Object -First 1
if (-not $propertyGroup) {
    throw "No PropertyGroup was found in '$PropsPath'."
}

$repoRoot = Split-Path -Parent $PropsPath

function Get-PropertyValue([string]$Name) {
    $value = $propertyGroup.$Name
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }
    return $value.Trim()
}

function Resolve-PropertyValue([string]$Value, [System.Collections.Generic.HashSet[string]]$Stack = $null) {
    if ($null -eq $Stack) {
        $Stack = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    }

    return ([regex]::Replace($Value, '\$\(([^)]+)\)', {
        param($match)
        $propertyName = $match.Groups[1].Value
        if (-not $Stack.Add($propertyName)) {
            throw "Circular property reference detected while resolving '$propertyName'."
        }
        try {
            $propertyValue = Get-PropertyValue $propertyName
            return Resolve-PropertyValue $propertyValue $Stack
        }
        finally {
            [void]$Stack.Remove($propertyName)
        }
    }))
}

function Get-AbsoluteRepoPath([string]$RelativePathPropertyName) {
    $relativePath = Resolve-PropertyValue (Get-PropertyValue $RelativePathPropertyName)
    if (-not $relativePath) { return "" }
    return Join-Path $repoRoot $relativePath
}

function Get-NormalizedVersionInfo([string]$Value, [string]$PropertyName) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$PropertyName is required. Use 1 to 4 numeric parts such as 1, 1.2, 1.2.3, or 1.2.3.4."
    }

    $trimmed = $Value.Trim()
    $segments = $trimmed.Split('.')

    if ($segments.Count -lt 1 -or $segments.Count -gt 4) {
        throw "$PropertyName value '$trimmed' is invalid. Use 1 to 4 numeric parts such as 1, 1.2, 1.2.3, or 1.2.3.4."
    }

    $parts = New-Object System.Collections.Generic.List[int]
    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -notmatch '^\d+$') {
            throw "$PropertyName value '$trimmed' is invalid. Every version segment must be a whole number."
        }

        $number = [int]$segment
        if ($number -lt 0 -or $number -gt 65535) {
            throw "$PropertyName value '$trimmed' is invalid. Each version segment must be between 0 and 65535."
        }

        [void]$parts.Add($number)
    }

    if ($parts[0] -eq 0) {
        throw "$PropertyName value '$trimmed' is invalid. The first version segment must be greater than 0."
    }

    $binaryParts = @($parts)
    while ($binaryParts.Count -lt 4) {
        $binaryParts += 0
    }

    [pscustomobject]@{
        Raw = $trimmed
        Canonical = ($parts -join '.')
        Binary = ($binaryParts -join '.')
        SegmentCount = $parts.Count
        Revision = $binaryParts[3]
    }
}

$rawVersion = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSVersion")
$rawAssemblyVersion = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSAssemblyVersion")
if ([string]::IsNullOrWhiteSpace($rawAssemblyVersion)) {
    $rawAssemblyVersion = $rawVersion
}

$versionInfo = Get-NormalizedVersionInfo $rawVersion "RetailStorePOSVersion"
$assemblyVersionInfo = Get-NormalizedVersionInfo $rawAssemblyVersion "RetailStorePOSAssemblyVersion"
$storePackageVersionIsValid = $versionInfo.Revision -eq 0
$storePackageVersionValidationMessage = if ($storePackageVersionIsValid) {
    ""
}
else {
    "Microsoft Store package version '$($versionInfo.Binary)' is invalid. The fourth version segment is reserved for Store use and must be 0. Use 1, 1.2, 1.2.3, or 1.2.3.0 for Store builds."
}

[pscustomobject]@{
    RepoRoot = $repoRoot
    ArtifactsRoot = Join-Path $repoRoot "artifacts"
    BrandName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSBrandName")
    AppName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSProductName")
    ProductName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSProductName")
    ExecutableName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSExecutableName")
    Version = $versionInfo.Canonical
    AssemblyVersion = $assemblyVersionInfo.Binary
    PackageVersion = $versionInfo.Binary
    StorePackageVersionIsValid = $storePackageVersionIsValid
    StorePackageVersionValidationMessage = $storePackageVersionValidationMessage
    Description = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSDescription")
    PolicyEffectiveDate = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSPolicyEffectiveDate")
    PolicyLastUpdatedDate = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSPolicyLastUpdatedDate")
    SupportEmail = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSSupportEmail")
    PrivacyContactEmail = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSPrivacyContactEmail")
    WinUiAssemblyName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiAssemblyName")
    WinUiTargetName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiTargetName")
    WinUiTitle = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiTitle")
    WinUiProductName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiProductName")
    WinUiPackageIdentityName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiPackageIdentityName")
    WinUiPackagePublisher = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiPackagePublisher")
    WinUiPackageDisplayName = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiPackageDisplayName")
    WinUiPackageDescription = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiPackageDescription")
    WinUiExecutableName = "$(Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSWinUiTargetName")).exe"
    WinUiProjectDirectory = Join-Path $repoRoot "Nexill.RetailStorePOS"
    WinUiProjectPath = Join-Path $repoRoot "Nexill.RetailStorePOS\Nexill.RetailStorePOS.csproj"
    SetupBaseFilename = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSSetupBaseFilename")
    StorePublishProfile = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSStorePublishProfile")
    UptodownPublishProfile = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSUptodownPublishProfile")
    PublishRuntimeIdentifier = Resolve-PropertyValue (Get-PropertyValue "RetailStorePOSPublishRuntimeIdentifier")
    AppIconPath = Get-AbsoluteRepoPath "RetailStorePOSAppIcon"
    WinUiAssetsPath = Join-Path $repoRoot "Nexill.RetailStorePOS\Assets"
    LicenseFilePath = Get-AbsoluteRepoPath "RetailStorePOSLicenseRelativePath"
    RenderedLicenseFilePath = Join-Path $repoRoot "artifacts\release-metadata\retail_store_pos_terms_and_privacy_policy.txt"
    WizardImagePath = Get-AbsoluteRepoPath "RetailStorePOSWizardImageRelativePath"
    WizardSmallImagePath = Get-AbsoluteRepoPath "RetailStorePOSWizardSmallImageRelativePath"
    PublishOutputPath = Join-Path $repoRoot "Nexill.RetailStorePOS\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
}
