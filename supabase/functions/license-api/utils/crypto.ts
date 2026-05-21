const encoder = new TextEncoder();

let cachedHmacKey: CryptoKey | null = null;
let cachedHmacKeyPepper: string | null = null;

/**
 * Hashes a license key for lookup against the `licenses.license_key_hash`
 * column. Uses HMAC-SHA256 with the LICENSE_KEY_PEPPER secret as the key.
 *
 * Why HMAC and not just SHA-256(key + pepper)?
 *   - HMAC has formal proofs of resistance to length-extension and to attacks
 *     that exploit the `hash(secret || message)` construction.
 *   - The pepper is treated as a key, which is the right type for it.
 *
 * MIGRATION NOTE:
 *   Existing license_key_hash values produced by the previous SHA-256(key + pepper)
 *   construction must be regenerated using HMAC-SHA256 before this code is deployed.
 *   See `supabase/seeds/regenerate_license_hashes.template.sql` for the backfill
 *   procedure. Until the backfill is complete, calls to verify_license against
 *   old hashes will return `license_not_found`.
 */
export async function computeLicenseKeyHash(licenseKey: string) {
  const pepper = Deno.env.get("LICENSE_KEY_PEPPER");
  if (!pepper || pepper.trim().length === 0) {
    throw new Error("license_key_pepper_not_configured");
  }

  const key = await getHmacKey(pepper);
  const normalized = normalizeLicenseKey(licenseKey);
  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    encoder.encode(normalized),
  );

  return toHex(new Uint8Array(signature));
}

async function getHmacKey(pepper: string): Promise<CryptoKey> {
  if (cachedHmacKey && cachedHmacKeyPepper === pepper) {
    return cachedHmacKey;
  }

  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(pepper),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );

  cachedHmacKey = key;
  cachedHmacKeyPepper = pepper;
  return key;
}

export async function signCanonicalJson(value: unknown) {
  const key = await importBackendSigningKey();
  const canonicalJson = canonicalizeJson(value);
  const signature = await crypto.subtle.sign(
    { name: "ECDSA", hash: "SHA-256" },
    key,
    encoder.encode(canonicalJson),
  );

  return toBase64(new Uint8Array(signature));
}

export function getBackendSigningKeyId() {
  return Deno.env.get("BACKEND_SIGNING_KEY_ID")?.trim() || "backend-signing-key-1";
}

async function importBackendSigningKey() {
  const keyMaterial = Deno.env.get("BACKEND_SIGNING_PRIVATE_KEY");
  if (!keyMaterial || keyMaterial.trim().length === 0) {
    throw new Error("signing_key_not_configured");
  }

  const trimmed = keyMaterial.trim();

  if (trimmed.startsWith("{")) {
    return await importJwkSigningKey(trimmed);
  }

  const pkcs8Bytes = pemToBytes(trimmed);

  return await crypto.subtle.importKey(
    "pkcs8",
    pkcs8Bytes,
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["sign"],
  );
}

async function importJwkSigningKey(jwkJson: string) {
  let jwk: JsonWebKey;

  try {
    jwk = JSON.parse(jwkJson) as JsonWebKey;
  } catch {
    throw new Error("signing_key_invalid");
  }

  const sanitizedJwk: JsonWebKey = {
    kty: jwk.kty,
    crv: jwk.crv,
    x: jwk.x,
    y: jwk.y,
    d: jwk.d,
    alg: jwk.alg,
    kid: jwk.kid,
  };

  return await crypto.subtle.importKey(
    "jwk",
    sanitizedJwk,
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["sign"],
  );
}

function normalizeLicenseKey(value: string) {
  return value.trim().toUpperCase().replace(/\s+/g, "");
}

function pemToBytes(pem: string) {
  const normalizedPem = pem.trim().replace(/\\n/g, "\n");
  const base64 = normalizedPem
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .filter((line) => !line.startsWith("-----BEGIN"))
    .filter((line) => !line.startsWith("-----END"))
    .join("");

  if (base64.length === 0) {
    throw new Error("signing_key_invalid");
  }

  return Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
}

function canonicalizeJson(value: unknown): string {
  if (value === null || typeof value !== "object") {
    return JSON.stringify(value);
  }

  if (Array.isArray(value)) {
    return `[${value.map((item) => canonicalizeJson(item)).join(",")}]`;
  }

  const entries = Object.entries(value as Record<string, unknown>)
    .filter(([, item]) => item !== undefined)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, item]) => `${JSON.stringify(key)}:${canonicalizeJson(item)}`);

  return `{${entries.join(",")}}`;
}

function toBase64(bytes: Uint8Array) {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary);
}

function toHex(bytes: Uint8Array) {
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
}
