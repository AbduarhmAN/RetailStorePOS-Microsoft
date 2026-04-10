$publisher = "CN=9A3EB885-0023-4A35-9C0F-B54D1E9D2824"
$certPath = "e:\Projects\Retail_Store\V\1.2.1\Nexill.RetailStorePOS\Certification\NexillStore_TemporaryKey.pfx"

# Generate a self-signed key in the user's personal store
$cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature -FriendlyName "Nexill.RetailStorePOS Local Test Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export it as a pfx file
$secPassword = ConvertTo-SecureString -String "nexill" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $secPassword

Write-Output "Certificate successfully generated at: $certPath"
Write-Output "Password: nexill"
Write-Output "Remember to install it to your 'Trusted People' store to successfully sideload or debug the app!"
