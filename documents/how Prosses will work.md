## 1. **Direct Answer**

Your uploaded code is a **working V1 plain-JSON license verification function**.

It already does the important base flow:

```text
POST JSON request
→ validate request
→ check installation
→ hash license key
→ check license
→ check/create activation
→ load permission features
→ build certificate
→ sign certificate
→ return signed response
```

But it is **not yet** the final encrypted/decrypted architecture we discussed.

Right now your code does **not** do these yet:

```text
1. It does not decrypt incoming encrypted JSON.
2. It does not verify a device request signature.
3. It does not encrypt the response back to the app.
4. It does not use HMAC-SHA256 for license keys.
5. It does not split handlers into multiple files.
6. It does not validate installId shape before querying Supabase.
7. It does not protect against blank-space secrets.
```

So the correct judgment is:

```text
Keep this code as handleV1VerifyLicense.
Do not throw it away.
Move it into its own file.
Add encryption/decryption wrapper around it.
Add router.ts to choose the right handler.
```

Your current code should become:

```text
handlers/v1/verifyLicense.ts
```

Not stay forever as one giant `index.ts`.

---

## 2. **Reasoning**

### Current code status

Your current file is basically:

```text
license-api/index.ts
```

with everything inside it:

```text
HTTP handler
request validation
database lookup
activation creation
feature loading
certificate payload creation
certificate signing
helper functions
```

That is why it worked and returned:

```json
{
  "success": true,
  "payload": {
    "messageType": "license_activation_certificate",
    "permissionGroup": "PREMIUM"
  }
}
```

So the logic is valid.

But for the architecture you want, it should become modular.

---

# What to change first

## Step 1: Keep `index.ts` small

Final `index.ts` should only do this:

```ts
import { routeLicenseRequest } from "./router.ts";
import { jsonError, jsonResponse } from "./utils/http.ts";

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return jsonResponse({ success: true }, 200);
  }

  if (req.method !== "POST") {
    return jsonError("method_not_allowed", 405);
  }

  try {
    const body = await req.json();

    const result = await routeLicenseRequest(body);

    return jsonResponse(result, 200);
  } catch (error) {
    const errorCode = error instanceof Error ? error.message : "unknown_error";

    console.error("license-api failed", {
      errorCode,
      timestamp: new Date().toISOString(),
    });

    return jsonError(errorCode, 400);
  }
});
```

Later, when encrypted requests are ready, this becomes:

```ts
const decryptedRequest = await decryptRequestEnvelope(body);
const result = await routeLicenseRequest(decryptedRequest);
const encryptedResponse = await encryptResponseEnvelope(result);
return jsonResponse(encryptedResponse, 200);
```

---

## Step 2: Create `router.ts`

This avoids the giant `if if if` problem.

```ts
import { handleV1VerifyLicense } from "./handlers/v1/verifyLicense.ts";

type LicenseHandler = (request: unknown) => Promise<unknown>;

const handlers: Record<string, LicenseHandler> = {
  "1:verify_license": handleV1VerifyLicense,
};

export async function routeLicenseRequest(request: any): Promise<unknown> {
  const protocolVersion = request?.protocolVersion;
  const action = request?.action;

  if (!protocolVersion) {
    throw new Error("missing_protocol_version");
  }

  if (!action) {
    throw new Error("missing_action");
  }

  const key = `${protocolVersion}:${action}`;
  const handler = handlers[key];

  if (!handler) {
    throw new Error("unsupported_action_or_protocol_version");
  }

  return await handler(request);
}
```

Later you add V2 like this:

```ts
import { handleV2VerifyLicense } from "./handlers/v2/verifyLicense.ts";

const handlers: Record<string, LicenseHandler> = {
  "1:verify_license": handleV1VerifyLicense,
  "2:verify_license": handleV2VerifyLicense,
};
```

Clean little switchboard, no spaghetti octopus.

---

## Step 3: Move your current working logic into `handlers/v1/verifyLicense.ts`

The main content of your uploaded code should move here:

```text
handlers/v1/verifyLicense.ts
```

This file should contain:

```text
validateRequestShape
validateProtocolVersion
validateRequestTime
verifyLicenseInDatabase
buildCertificatePayload
signCertificate
handleV1VerifyLicense
```

The handler should look like this:

```ts
export async function handleV1VerifyLicense(body: VerifyLicenseRequest): Promise<unknown> {
  validateRequestShape(body);
  validateProtocolVersion(body.protocolVersion);
  validateRequestTime(body.requestTimeUtc);

  const supabase = createClient(
    mustEnv("SUPABASE_URL"),
    mustEnv("SUPABASE_SERVICE_ROLE_KEY")
  );

  const licenseKeyHash = await licenseKeyHashForLookup(body.licenseKey);

  const result = await verifyLicenseInDatabase(
    supabase,
    body,
    licenseKeyHash
  );

  if (result.resultCode !== "verified") {
    return {
      success: false,
      errorCode: result.resultCode,
      ...result,
    };
  }

  const certificatePayload = buildCertificatePayload(body, result);
  const signedCertificate = await signCertificate(certificatePayload);

  return {
    success: true,
    ...signedCertificate,
  };
}
```

---

# Important fixes in your current code

## Fix 1: `mustEnv` must reject blank spaces

Your current code has:

```ts
function mustEnv(name: string): string {
  const value = Deno.env.get(name);

  if (!value) {
    throw new Error(`missing_env_${name}`);
  }

  return value;
}
```

Replace it with:

```ts
function mustEnv(name: string): string {
  const value = Deno.env.get(name);

  if (!value || value.trim().length === 0) {
    throw new Error(`missing_env_${name}`);
  }

  return value;
}
```

This prevents `" "` from being treated as valid.

---

## Fix 2: Validate `installId` shape before Supabase query

Historical draft assumption:

```ts
// Earlier idea:
// function isValidUuid(value: string): boolean { ... }
// if (!isValidUuid(body.installId)) throw new Error("invalid_install_id_format");
```

Implemented variant:

Add:

```ts
function isValidInstallId(value: string): boolean {
  return /^[A-Za-z0-9_-]{16,128}$/.test(value);
}
```

Then inside `validateRequestShape`:

```ts
if (!isValidInstallId(body.installId)) {
  throw new Error("invalid_install_id");
}
```

This allows both the legacy GUID-style ID and the newer hardware-hash ID while still rejecting blank, malformed, or pathologically large values.

Status note:

```text
This was not implemented as UUID-only validation.
It was implemented as bounded text-shape validation because the current client canonical install_id is hardware-derived text.
```

---

## Fix 3: Use HMAC-SHA256 for license key hashing

Your current code uses:

```ts
sha256Hex(normalizeLicenseKey(body.licenseKey) + mustEnv("LICENSE_KEY_PEPPER"))
```

That works for testing, but the better final version is:

```text
HMAC-SHA256(secret pepper, normalized license key)
```

Add:

```ts
async function hmacSha256Hex(secret: string, message: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    new TextEncoder().encode(message)
  );

  return bytesToHex(new Uint8Array(signature));
}

async function licenseKeyHashForLookup(licenseKey: string): Promise<string> {
  return await hmacSha256Hex(
    mustEnv("LICENSE_KEY_PEPPER"),
    normalizeLicenseKey(licenseKey)
  );
}
```

Then replace:

```ts
const licenseKeyHash = await sha256Hex(
  normalizeLicenseKey(body.licenseKey) + mustEnv("LICENSE_KEY_PEPPER")
);
```

with:

```ts
const licenseKeyHash = await licenseKeyHashForLookup(body.licenseKey);
```

Important: if you change hashing to HMAC, your existing test license hash in Supabase must be regenerated using HMAC too.

For now, you may keep the current SHA-256 test method until the full app flow works.

---

## Fix 4: Add `notBeforeUtc` to the certificate

Your current payload has:

```ts
issuedAtUtc: now.toISOString(),
expiresAtUtc,
```

Add:

```ts
notBeforeUtc: now.toISOString(),
```

So:

```ts
return {
  messageType: "license_activation_certificate",
  certificateVersion: 1,
  activationCertificateId: crypto.randomUUID(),
  licenseId: result.licenseId,
  activationId: result.activationId,
  installId: result.installId,
  productCode: result.productCode,
  permissionGroup: result.permissionGroup,
  features: result.features,
  licenseStatus: result.licenseStatus,
  activationStatus: result.activationStatus,
  issuedAtUtc: now.toISOString(),
  notBeforeUtc: now.toISOString(),
  expiresAtUtc,
  requestNonce: request.requestNonce,
  requestSequence: request.requestSequence,
};
```

---

## Fix 5: Add better Supabase error logging

Your current code hides database error details.

For example:

```ts
if (installationError) {
  throw new Error("installation_lookup_failed");
}
```

Use this during development:

```ts
if (installationError) {
  console.error("installation lookup failed", {
    message: installationError.message,
    code: installationError.code,
    details: installationError.details,
    hint: installationError.hint,
    installId: request.installId,
  });

  throw new Error("installation_lookup_failed");
}
```

Do the same for:

```text
licenseError
permissionGroupError
activationLookupError
countError
createError
updateError
featuresError
```

Do not log the raw license key.

---

# Final folder structure

Use this:

```text
supabase/
  functions/
    license-api/
      index.ts
      router.ts

      handlers/
        v1/
          verifyLicense.ts
          validateV1VerifyLicenseRequest.ts

        v2/
          verifyLicense.ts

      crypto/
        canonicalJson.ts
        signCertificate.ts
        hmac.ts
        keyImport.ts
        base64.ts

      db/
        verifyLicenseInDatabase.ts

      utils/
        env.ts
        http.ts
        time.ts
        validation.ts

      types/
        licensing.ts
```

This gives you separate files without creating many public URLs.

---

# Where encryption goes later

Your current code receives plain JSON.

Later, add this before routing:

```ts
const innerRequest = await decryptRequestEnvelope(body);
const result = await routeLicenseRequest(innerRequest);
```

And after routing:

```ts
const encryptedResponse = await encryptResponseEnvelope(result);
return jsonResponse(encryptedResponse, 200);
```

So your final `index.ts` becomes:

```text
HTTP
→ decrypt request envelope
→ route to handler
→ encrypt response envelope
→ return
```

Your current `verifyLicenseInDatabase` stays mostly the same.

That is the correct layering.

---

## 3. **Uncertainty or Limits**

Your uploaded code is good as a proof that Supabase, the tables, the license key, and signing are working.

But it is not the final secure design yet.

Current status:

```text
Works:
  plain JSON verification
  license lookup
  activation create/refresh
  feature loading
  backend-signed certificate

Missing:
  encrypted request envelope
  device request signature verification
  encrypted response envelope
  HMAC license-key hashing
  modular file split
  stronger validation/logging
```

Next best move:

```text
1. Refactor this exact code into handlers/v1/verifyLicense.ts.
2. Create router.ts.
3. Keep index.ts tiny.
4. After that, add crypto/decryptRequestEnvelope.ts.
```

Do not rewrite the whole thing. Your code already proved the engine works. Now split the engine into compartments and add the armored doors.
