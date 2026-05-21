/**
 * Request envelope decryption.
 *
 * Wire format (matches RetailStorePOS WinUI app's
 * Services/Licensing/EncryptionEnvelope.cs):
 *
 * {
 *   "messageType": "encrypted_license_verification_envelope",
 *   "protocolVersion": 1,
 *   "keyId": "backend-encryption-key-1",
 *   "encryptedContentKey": "<base64 RSA-OAEP-256(AES key)>",
 *   "nonce":               "<base64 12-byte AES-GCM nonce>",
 *   "ciphertext":          "<base64 AES-GCM ciphertext>",
 *   "authenticationTag":   "<base64 16-byte AES-GCM tag>"
 * }
 *
 * The Edge Function decrypts the AES content key with its private RSA-OAEP
 * key, then decrypts the ciphertext with AES-GCM. The recovered plaintext is
 * the JSON of the original verify_license request, which then flows through
 * the existing router.
 *
 * BACKWARD COMPATIBILITY: callers that don't yet wrap requests in envelopes
 * still POST plain JSON. <see cref="tryDecryptEnvelope"/> detects the envelope
 * via its messageType field and passes plain bodies through unchanged so the
 * transition can happen one client at a time.
 */

const ENVELOPE_MESSAGE_TYPE = "encrypted_license_verification_envelope";
const SUPPORTED_PROTOCOL_VERSION = 1;

let cachedDecryptionKey: CryptoKey | null = null;
let cachedDecryptionKeyMaterial: string | null = null;

export type EnvelopeBody = {
  messageType?: unknown;
  protocolVersion?: unknown;
  keyId?: unknown;
  encryptedContentKey?: unknown;
  nonce?: unknown;
  ciphertext?: unknown;
  authenticationTag?: unknown;
  [key: string]: unknown;
};

/**
 * Returns true when the parsed body claims to be an encryption envelope.
 * A weak check is sufficient — full validation happens in decryptEnvelope.
 */
export function isEncryptedEnvelope(body: unknown): body is EnvelopeBody {
  return (
    body !== null &&
    typeof body === "object" &&
    !Array.isArray(body) &&
    (body as EnvelopeBody).messageType === ENVELOPE_MESSAGE_TYPE
  );
}

/**
 * If the body is an envelope, decrypt it and return the recovered inner JSON
 * object. Otherwise return the body unchanged so plain-JSON callers keep
 * working during the transition.
 */
export async function tryDecryptEnvelope(body: unknown): Promise<unknown> {
  if (!isEncryptedEnvelope(body)) {
    return body;
  }
  return await decryptEnvelope(body);
}

async function decryptEnvelope(envelope: EnvelopeBody): Promise<unknown> {
  validateEnvelopeShape(envelope);

  const expectedKeyId = (Deno.env.get("BACKEND_ENCRYPTION_KEY_ID")?.trim()) || "backend-encryption-key-1";
  if (envelope.keyId !== expectedKeyId) {
    // Different key version than what we have private material for. Either the
    // app shipped before a rotation or the operator forgot to update Supabase.
    throw new Error("envelope_unknown_key_id");
  }

  const wrappedKeyBytes = decodeBase64(envelope.encryptedContentKey as string, "encrypted_content_key");
  const nonceBytes = decodeBase64(envelope.nonce as string, "nonce");
  const ciphertextBytes = decodeBase64(envelope.ciphertext as string, "ciphertext");
  const tagBytes = decodeBase64(envelope.authenticationTag as string, "authentication_tag");

  if (nonceBytes.length !== 12) {
    throw new Error("envelope_invalid_nonce_length");
  }
  if (tagBytes.length !== 16) {
    throw new Error("envelope_invalid_tag_length");
  }

  const decryptionKey = await getBackendDecryptionKey();

  let aesKeyBytes: ArrayBuffer;
  try {
    aesKeyBytes = await crypto.subtle.decrypt(
      { name: "RSA-OAEP" },
      decryptionKey,
      wrappedKeyBytes,
    );
  } catch {
    throw new Error("envelope_content_key_unwrap_failed");
  }

  if (aesKeyBytes.byteLength !== 32) {
    throw new Error("envelope_invalid_content_key_length");
  }

  const aesKey = await crypto.subtle.importKey(
    "raw",
    aesKeyBytes,
    { name: "AES-GCM", length: 256 },
    false,
    ["decrypt"],
  );

  // Web Crypto's AES-GCM expects the auth tag concatenated to the ciphertext.
  // The wire format keeps them separate (matching .NET's AesGcm shape), so we
  // join them here.
  const combined = new Uint8Array(ciphertextBytes.length + tagBytes.length);
  combined.set(ciphertextBytes, 0);
  combined.set(tagBytes, ciphertextBytes.length);

  let plaintextBytes: ArrayBuffer;
  try {
    plaintextBytes = await crypto.subtle.decrypt(
      { name: "AES-GCM", iv: nonceBytes },
      aesKey,
      combined,
    );
  } catch {
    throw new Error("envelope_decryption_failed");
  }

  const plaintextJson = new TextDecoder("utf-8").decode(plaintextBytes);

  let inner: unknown;
  try {
    inner = JSON.parse(plaintextJson);
  } catch {
    throw new Error("envelope_inner_payload_invalid_json");
  }

  if (inner === null || typeof inner !== "object" || Array.isArray(inner)) {
    throw new Error("envelope_inner_payload_invalid_shape");
  }

  return inner;
}

function validateEnvelopeShape(envelope: EnvelopeBody) {
  if (typeof envelope.protocolVersion !== "number" || envelope.protocolVersion !== SUPPORTED_PROTOCOL_VERSION) {
    throw new Error("envelope_unsupported_protocol_version");
  }
  if (typeof envelope.keyId !== "string" || envelope.keyId.trim().length === 0) {
    throw new Error("envelope_missing_key_id");
  }
  for (const field of ["encryptedContentKey", "nonce", "ciphertext", "authenticationTag"] as const) {
    const value = envelope[field];
    if (typeof value !== "string" || value.trim().length === 0) {
      throw new Error(`envelope_missing_${snake(field)}`);
    }
  }
}

async function getBackendDecryptionKey(): Promise<CryptoKey> {
  const keyMaterial = Deno.env.get("BACKEND_DECRYPTION_PRIVATE_KEY");
  if (!keyMaterial || keyMaterial.trim().length === 0) {
    throw new Error("decryption_key_not_configured");
  }

  const trimmed = keyMaterial.trim();

  if (cachedDecryptionKey && cachedDecryptionKeyMaterial === trimmed) {
    return cachedDecryptionKey;
  }

  let imported: CryptoKey;
  if (trimmed.startsWith("{")) {
    imported = await importJwkPrivateKey(trimmed);
  } else {
    imported = await importPkcs8PemPrivateKey(trimmed);
  }

  cachedDecryptionKey = imported;
  cachedDecryptionKeyMaterial = trimmed;
  return imported;
}

async function importJwkPrivateKey(jwkJson: string): Promise<CryptoKey> {
  let jwk: JsonWebKey;
  try {
    jwk = JSON.parse(jwkJson) as JsonWebKey;
  } catch {
    throw new Error("decryption_key_invalid");
  }

  return await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSA-OAEP", hash: "SHA-256" },
    false,
    ["decrypt"],
  );
}

async function importPkcs8PemPrivateKey(pem: string): Promise<CryptoKey> {
  const normalized = pem.replace(/\\n/g, "\n");
  const base64 = normalized
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .filter((line) => !line.startsWith("-----BEGIN"))
    .filter((line) => !line.startsWith("-----END"))
    .join("");

  if (base64.length === 0) {
    throw new Error("decryption_key_invalid");
  }

  const pkcs8 = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));

  return await crypto.subtle.importKey(
    "pkcs8",
    pkcs8,
    { name: "RSA-OAEP", hash: "SHA-256" },
    false,
    ["decrypt"],
  );
}

function decodeBase64(input: string, fieldName: string): Uint8Array {
  try {
    return Uint8Array.from(atob(input), (char) => char.charCodeAt(0));
  } catch {
    throw new Error(`envelope_invalid_${snake(fieldName)}_base64`);
  }
}

function snake(camel: string): string {
  return camel.replace(/[A-Z]/g, (m) => "_" + m.toLowerCase());
}
