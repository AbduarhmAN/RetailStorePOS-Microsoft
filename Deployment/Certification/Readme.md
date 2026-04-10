# Nexill.RetailStorePOS - Certification Assets

This directory serves as the secure, isolated vault for the application's code signing certificates (`.pfx`), which are strictly required to package and test the WinUI 3 / MSIX application.

## Folder Contents

### 1. `NexillStore_TemporaryKey.pfx`
- **Purpose**: A self-signed development certificate used purely for local debugging, testing of the MSIX package, and internal QA sideloading.
- **Identity**: Matches the AppxManifest Publisher exactly (`CN=9A3EB885-0023-4A35-9C0F-B54D1E9D2824`).
- **Password**: `nexill`

### 2. `NexillStore_StoreKey.pfx`
- **Purpose**: The production code-signing certificate (obtained externally or generated for company signing). This is the key used for enterprise distribution or final pre-store packaging.
- **Note on Microsoft Store**: When packaging the final `.msixupload` bundle for the Microsoft Store (Partner Center), Microsoft strips the local signature and handles the final production signing themselves. 

## Trusting the Certificate for Sideloading
If you get a deployment error in Visual Studio ("The certificate is not trusted"), you must install the temporary key into your local machine's `Trusted People` or `Trusted Root Certification Authorities` store:
1. Double-click `NexillStore_TemporaryKey.pfx`.
2. Select **Local Machine**.
3. Enter the password (`nexill`).
4. Place the certificate in the **Trusted People** store.
5. Finish the wizard and retry deploying the app.
