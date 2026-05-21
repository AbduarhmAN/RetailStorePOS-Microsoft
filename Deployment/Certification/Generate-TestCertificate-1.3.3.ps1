$publisher = "CN=9A3EB885-0023-4A35-9C0F-B54D1E9D2824"
$certDir = "e:\Projects\Retail_Store\V\1.3.3\Deployment\Certification"
$pfxPath = Join-Path $certDir "NexillStore_TestKey.pfx"
$cerPath = Join-Path $certDir "NexillStore_TestKey.cer"

if (-not (Test-Path $certDir)) {
    New-Item -ItemType Directory -Path $certDir -Force
}

# Generate a self-signed key in the user's personal store
$cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature -FriendlyName "Nexill.RetailStorePOS 1.3.3 Test Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export it as a pfx file
$secPassword = ConvertTo-SecureString -String "nexill" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secPassword

# Export it as a cer file (this is what you need to trust)
Export-Certificate -Cert $cert -FilePath $cerPath

Write-Output "--------------------------------------------------"
Write-Output "Certificate successfully generated!"
Write-Output "PFX (for signing): $pfxPath"
Write-Output "CER (for trusting): $cerPath"
Write-Output "Password: nexill"
Write-Output "--------------------------------------------------"
Write-Output "INSTRUCTIONS:"
Write-Output "1. Right-click '$cerPath' and select 'Install Certificate'."
Write-Output "2. Choose 'Local Machine'."
Write-Output "3. Place it in the 'Trusted People' store."
Write-Output "--------------------------------------------------"
