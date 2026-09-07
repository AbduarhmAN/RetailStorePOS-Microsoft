// Protocol v2 primitives. Existing v1 routes do not call these yet.
export const DEVICE_PROOF_ALGORITHM = "ECDSA-P256-SHA256";
export const DEVICE_PROOF_AUDIENCE = "nexill-license-device-v2";
export const DEVICE_PROOF_PROTOCOL_VERSION = 2;
const encoder = new TextEncoder();
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const base64url32 = /^[A-Za-z0-9_-]{43}$/;

export class DeviceProofError extends Error {
  constructor(code: string) {
    super(code);
    this.name = "DeviceProofError";
  }
}

export type DeviceJwk = { kty: "EC"; crv: "P-256"; x: string; y: string };
export type DeviceChallenge = {
  audience: string;
  challengeId: string;
  devicePublicKeyThumbprint: string;
  expiresAtUtc: string;
  installId: string;
  issuedAtUtc: string;
  messageType: string;
  mode: "enroll" | "refresh";
  nonce: string;
  productCode: string;
  protocolVersion: number;
  requestNonce: string;
  requestSequence: number;
};

export type StoredDeviceChallenge = {
  id: string;
  install_id: string;
  thumbprint: string;
  public_key: unknown;
  payload: unknown;
  expires_at: string;
  consumed_at: string | null;
};

function object(value: unknown): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new DeviceProofError("invalid_device_proof");
  }
  return value as Record<string, unknown>;
}

function exactKeys(value: Record<string, unknown>, keys: readonly string[]) {
  const actual = Object.keys(value).sort();
  const expected = [...keys].sort();
  if (actual.length !== expected.length || actual.some((key, i) => key !== expected[i])) {
    throw new DeviceProofError("invalid_device_proof_fields");
  }
}

function toBase64Url(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function decodeCoordinate(value: unknown): string {
  if (typeof value !== "string" || !base64url32.test(value)) {
    throw new DeviceProofError("invalid_device_public_key");
  }
  const bytes = Uint8Array.from(atob(value.replace(/-/g, "+").replace(/_/g, "/") + "="), c => c.charCodeAt(0));
  if (bytes.length !== 32 || toBase64Url(bytes) !== value) {
    throw new DeviceProofError("invalid_device_public_key");
  }
  return value;
}

export async function importDevicePublicKey(value: unknown) {
  const input = object(value);
  // Never accept private key material or a caller-selected algorithm/key URL.
  exactKeys(input, ["kty", "crv", "x", "y"]);
  if (input.kty !== "EC" || input.crv !== "P-256") {
    throw new DeviceProofError("invalid_device_public_key");
  }
  const jwk: DeviceJwk = { kty: "EC", crv: "P-256", x: decodeCoordinate(input.x), y: decodeCoordinate(input.y) };
  let key: CryptoKey;
  try {
    key = await crypto.subtle.importKey("jwk", jwk, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
  } catch {
    throw new DeviceProofError("invalid_device_public_key");
  }
  // RFC 7638: only required JWK members, in lexicographic order.
  const canonical = JSON.stringify({ crv: jwk.crv, kty: jwk.kty, x: jwk.x, y: jwk.y });
  const thumbprint = toBase64Url(new Uint8Array(await crypto.subtle.digest("SHA-256", encoder.encode(canonical))));
  return { key, jwk, thumbprint };
}

export function parseDeviceChallenge(value: unknown, now: Date): DeviceChallenge {
  const payload = object(value);
  exactKeys(payload, ["audience", "challengeId", "devicePublicKeyThumbprint", "expiresAtUtc", "installId",
    "issuedAtUtc", "messageType", "mode", "nonce", "productCode", "protocolVersion", "requestNonce", "requestSequence"]);
  if (payload.audience !== DEVICE_PROOF_AUDIENCE || payload.messageType !== "license_device_challenge"
    || payload.protocolVersion !== DEVICE_PROOF_PROTOCOL_VERSION || payload.productCode !== "RETAILSTOREPOS"
    || (payload.mode !== "enroll" && payload.mode !== "refresh")
    || typeof payload.challengeId !== "string" || payload.challengeId.length !== 36 || !uuid.test(payload.challengeId)
    || typeof payload.installId !== "string" || payload.installId.length < 1 || payload.installId.length > 256
    || payload.installId.trim() !== payload.installId
    || typeof payload.devicePublicKeyThumbprint !== "string" || payload.devicePublicKeyThumbprint.length !== 43 || !base64url32.test(payload.devicePublicKeyThumbprint)
    || typeof payload.nonce !== "string" || payload.nonce.length !== 64 || !/^[0-9a-f]{64}$/.test(payload.nonce)
    || typeof payload.requestNonce !== "string" || payload.requestNonce.length !== 32 || !/^[0-9a-f]{32}$/.test(payload.requestNonce)
    || typeof payload.requestSequence !== "number" || !Number.isSafeInteger(payload.requestSequence)
    || payload.requestSequence < 1) {
    throw new DeviceProofError("invalid_device_challenge");
  }
  const issued = parseTimestamp(payload.issuedAtUtc);
  const expires = parseTimestamp(payload.expiresAtUtc);
  if (!Number.isFinite(now.getTime()) || issued > now.getTime() + 30_000 || expires <= now.getTime()
    || expires <= issued || expires - issued > 120_000) {
    throw new DeviceProofError("device_challenge_expired_or_invalid");
  }
  return payload as DeviceChallenge;
}

function parseTimestamp(value: unknown): number {
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(value)) {
    throw new DeviceProofError("invalid_device_challenge_time");
  }
  const time = Date.parse(value);
  if (!Number.isFinite(time) || new Date(time).toISOString() !== value) {
    throw new DeviceProofError("invalid_device_challenge_time");
  }
  return time;
}

export function canonicalDeviceChallenge(payload: DeviceChallenge): string {
  // The validated schema contains only strings and safe integers. Use ordinal
  // key ordering, independent of the host's locale and legacy v1 canonicalizer.
  const values = payload as unknown as Record<string, string | number>;
  return `{${Object.keys(values).sort().map(key => `${JSON.stringify(key)}:${JSON.stringify(values[key])}`).join(",")}}`;
}

export function parseDeviceProof(value: unknown) {
  const proof = object(value);
  exactKeys(proof, ["challengeId", "signature", "signatureAlgorithm"]);
  if (typeof proof.challengeId !== "string" || proof.challengeId.length !== 36 || !uuid.test(proof.challengeId)
    || proof.signatureAlgorithm !== DEVICE_PROOF_ALGORITHM || typeof proof.signature !== "string"
    || proof.signature.length !== 88 || !/^[A-Za-z0-9+/]{86}==$/.test(proof.signature)) {
    throw new DeviceProofError("invalid_device_proof");
  }
  const signature = Uint8Array.from(atob(proof.signature), c => c.charCodeAt(0));
  if (signature.length !== 64 || btoa(String.fromCharCode(...signature)) !== proof.signature) {
    throw new DeviceProofError("invalid_device_proof");
  }
  return { challengeId: proof.challengeId, signature };
}

export async function verifyStoredDeviceProof(stored: StoredDeviceChallenge, proofBody: unknown, now: Date) {
  const proof = parseDeviceProof(proofBody);
  if (stored.id !== proof.challengeId || stored.consumed_at !== null) {
    throw new DeviceProofError("device_challenge_expired_or_used");
  }
  const payload = parseDeviceChallenge(stored.payload, now);
  if (payload.challengeId !== stored.id || payload.installId !== stored.install_id
    || payload.devicePublicKeyThumbprint !== stored.thumbprint
    || Date.parse(stored.expires_at) !== Date.parse(payload.expiresAtUtc)) {
    throw new DeviceProofError("device_challenge_binding_mismatch");
  }
  const identity = await importDevicePublicKey(stored.public_key);
  if (identity.thumbprint !== stored.thumbprint) {
    throw new DeviceProofError("device_key_mismatch");
  }
  const valid = await crypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, identity.key,
    proof.signature, encoder.encode(canonicalDeviceChallenge(payload)));
  if (!valid) throw new DeviceProofError("device_signature_invalid");
  return payload;
}

export interface DeviceChallengeStore {
  load(id: string): Promise<StoredDeviceChallenge | null>;
  // Must atomically recheck expiry, consumption, current binding, license state,
  // seat count and sequence, then consume the challenge and commit the activation.
  consume(id: string, thumbprint: string, sequence: number): Promise<unknown>;
}

export async function verifyAndConsumeDeviceProof(store: DeviceChallengeStore, proof: unknown, now = new Date()) {
  const parsed = parseDeviceProof(proof);
  const stored = await store.load(parsed.challengeId);
  if (!stored) throw new DeviceProofError("device_challenge_not_found");
  const payload = await verifyStoredDeviceProof(stored, proof, now);
  const result = await store.consume(stored.id, payload.devicePublicKeyThumbprint, payload.requestSequence);
  return { challenge: payload, result };
}
