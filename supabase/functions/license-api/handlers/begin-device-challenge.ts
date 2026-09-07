import { computeLicenseKeyHash } from "../utils/crypto.ts";
import {
  asJsonMap,
  asOptionalInteger,
  asOptionalString,
  asRequiredString,
  createSupabaseAdminClient,
  PRODUCT_CODE,
} from "./shared.ts";
import {
  DEVICE_PROOF_AUDIENCE,
  DEVICE_PROOF_PROTOCOL_VERSION,
  importDevicePublicKey,
  parseDeviceChallenge,
} from "../utils/device-proof.ts";
import { getBackendSigningKeyId, signCanonicalJson } from "../utils/crypto.ts";

const MAX_SAFE_SEQUENCE = 9_007_199_254_740_991;

export async function handleBeginDeviceChallenge(body: Record<string, unknown>) {
  const source = asJsonMap(body.payload) ?? body;
  const installId = asRequiredString(source.installId, "missing_install_id");
  if (installId.length > 256 || source.installId !== installId) {
    throw new Error("invalid_install_id");
  }
  const mode = asOptionalString(source.mode);
  if (mode !== "enroll" && mode !== "refresh") {
    throw new Error("invalid_device_challenge_mode");
  }

  const protocolVersion = source.protocolVersion;
  if (protocolVersion !== DEVICE_PROOF_PROTOCOL_VERSION) {
    throw new Error("unsupported_device_protocol_version");
  }

  if (asOptionalString(source.productCode) !== PRODUCT_CODE) {
    throw new Error("invalid_product_code");
  }

  const requestNonce = asOptionalString(source.requestNonce);
  if (!requestNonce || !/^[0-9a-f]{32}$/.test(requestNonce)) {
    throw new Error("invalid_request_nonce");
  }

  const requestSequence = asOptionalInteger(source.requestSequence);
  if (requestSequence === null || requestSequence < 1 || requestSequence > MAX_SAFE_SEQUENCE) {
    throw new Error("invalid_request_sequence");
  }

  const identity = await importDevicePublicKey(source.devicePublicKey);
  const suppliedThumbprint = asOptionalString(source.devicePublicKeyThumbprint);
  if (identity.thumbprint !== suppliedThumbprint) {
    throw new Error("device_key_thumbprint_mismatch");
  }

  let licenseKeyHash = "";
  if (mode === "enroll") {
    const licenseKey = asRequiredString(source.licenseKey, "missing_license_key");
    if (licenseKey.length > 512) throw new Error("invalid_license_key");
    licenseKeyHash = await computeLicenseKeyHash(licenseKey);
  }

  const admin = createSupabaseAdminClient();
  const { data, error } = await admin.rpc("begin_license_device_challenge", {
    p_install_id: installId,
    p_mode: mode,
    p_license_hash: licenseKeyHash,
    p_public_key: identity.jwk,
    p_thumbprint: identity.thumbprint,
    p_request_nonce: requestNonce,
    p_sequence: requestSequence,
  });

  if (error) {
    console.error("device challenge creation failed", { mode, code: error.code });
    throw new Error("device_challenge_creation_failed");
  }

  const payload = parseDeviceChallenge(data, new Date());
  if (payload.audience !== DEVICE_PROOF_AUDIENCE
    || payload.mode !== mode
    || payload.devicePublicKeyThumbprint !== identity.thumbprint
    || payload.requestNonce !== requestNonce
    || payload.requestSequence < requestSequence) {
    throw new Error("device_challenge_creation_failed");
  }

  return {
    success: true,
    payload,
    backendSignature: await signCanonicalJson(payload),
    backendSignatureAlgorithm: "ECDSA-P256-SHA256",
    keyId: getBackendSigningKeyId(),
  };
}
