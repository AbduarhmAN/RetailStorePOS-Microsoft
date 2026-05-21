# Backend encryption keypair — operator setup

The license-api Edge Function accepts an **encrypted envelope** form of the
verify_license request. When the app wraps the request in an envelope, the
license key never travels to Supabase in plaintext — only the AES-GCM
ciphertext does, and only the holder of `BACKEND_DECRYPTION_PRIVATE_KEY` can
unwrap it.

This is **opt-in**. Until the keypair is generated and installed, the app
posts plain JSON and the Edge Function accepts it. Both forms work side by
side during the rollout.

## One-time keypair generation

Run these on a trusted offline machine. The private key never leaves disk
encrypted — copy it directly into Supabase secrets.

```bash
# 1. Generate a 2048-bit RSA private key in PKCS8 PEM form.
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 \
    -out backend-encryption-private.pem

# 2. Extract the matching public key in SPKI PEM form.
openssl rsa -pubout \
    -in  backend-encryption-private.pem \
    -out backend-encryption-public.pem
```

## Convert the public key to JWK

The app reads JWK, not PEM. A small Node.js script does the conversion
without bringing a JOSE library into the project:

```bash
node -e "
const fs = require('fs');
const crypto = require('crypto');
const pem = fs.readFileSync('backend-encryption-public.pem', 'utf8');
const key = crypto.createPublicKey(pem);
const jwk = key.export({ format: 'jwk' });
jwk.alg = 'RSA-OAEP-256';
jwk.use = 'enc';
jwk.kid = 'backend-encryption-key-1';
jwk.key_ops = ['wrapKey', 'encrypt'];
jwk.ext = true;
fs.writeFileSync('backend-encryption-public.jwk.json',
    JSON.stringify(jwk, null, 2));
"
```

The output `backend-encryption-public.jwk.json` is a complete JWK ready to
ship into install-side secure storage.

## Install on app side

The WinUI app reads the JWK from DPAPI-protected secure storage. On every
install (per machine), seed the JWK once:

```csharp
// Run this from any code path that has SecureStorageService loaded —
// typically the installer's first-run hook or an admin maintenance script.
var jwkText = File.ReadAllText("backend-encryption-public.jwk.json");
SecureStorageService.StoreSecret(
    PublicEncryptionKey.PublicJwkSecretName,    // "BackendEncryptionPublicJwk"
    jwkText);

// Optional: only needed if the kid differs from the default.
SecureStorageService.StoreSecret(
    PublicEncryptionKey.KeyIdSecretName,        // "BackendEncryptionKeyId"
    "backend-encryption-key-1");
```

Once `PublicEncryptionKey.IsConfigured` returns true, every subsequent call
to `LicenseValidationService.ActivateAsync` posts an envelope.

## Install on backend side

Upload the **private** PEM into Supabase as a secret. The Edge Function reads
it via `Deno.env.get("BACKEND_DECRYPTION_PRIVATE_KEY")`.

```bash
# Read the PEM into a single line so the CLI accepts it.
PRIVATE_KEY="$(cat backend-encryption-private.pem)"
supabase secrets set BACKEND_DECRYPTION_PRIVATE_KEY="$PRIVATE_KEY"

# Set the kid that matches what the JWK says (default is fine).
supabase secrets set BACKEND_ENCRYPTION_KEY_ID="backend-encryption-key-1"

# Re-deploy so the function picks up the new secret on cold start.
supabase functions deploy license-api
```

The Edge Function caches the imported key per cold start, so subsequent
requests pay the importKey cost once.

## Smoke test

```bash
# An app build with PublicEncryptionKey.IsConfigured == true should produce
# requests with messageType="encrypted_license_verification_envelope".
# Capture one in Supabase function logs and confirm the function decrypts
# successfully (you should see the inner action log with no envelope_*
# error codes).
```

## Key rotation

To rotate:

1. Generate a new keypair, kid `backend-encryption-key-2`.
2. Set `BACKEND_DECRYPTION_PRIVATE_KEY` to the new private PEM.
3. Set `BACKEND_ENCRYPTION_KEY_ID` to `backend-encryption-key-2`.
4. Re-deploy the Edge Function. **Old apps that still ship key-1 will start
   getting `envelope_unknown_key_id` 400s.**
5. Push an app build with the new public JWK and bump the rollout.
6. Once telemetry shows zero `envelope_unknown_key_id` errors, retire the
   old private key from any backups.

The backwards-compatible plain-JSON path stays available throughout — apps
without a configured public key keep working.

## Files in this folder

- `backend-signing-public.jwk.json` — backend ECDSA P-256 public key used to
  verify certificate signatures. Embedded in the app at build time via
  `PublicLicenseKey.cs`.
- `backend-encryption-public.jwk.template.json` — placeholder. Do not deploy.
- `SETUP.md` — this file.
