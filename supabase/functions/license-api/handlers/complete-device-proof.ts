import { asJsonMap, createSupabaseAdminClient } from "./shared.ts";
import { getBackendSigningKeyId, signCanonicalJson } from "../utils/crypto.ts";
import { DeviceProofError, verifyAndConsumeDeviceProof } from "../utils/device-proof.ts";
import type { StoredDeviceChallenge } from "../utils/device-proof.ts";

// Source-wired but not deployed until enrollment, the transaction migration,
// recovery and v2 client cache/downgrade rules are implemented together.
export async function handleCompleteDeviceProof(proof: unknown) {
  const admin = createSupabaseAdminClient();
  const verified = await verifyAndConsumeDeviceProof({
    async load(id) {
      const { data, error } = await admin.from("license_device_challenges")
        .select("id,install_id,thumbprint,public_key,payload,expires_at,consumed_at")
        .eq("id", id).maybeSingle();
      if (error) throw new DeviceProofError("device_challenge_lookup_failed");
      return data as StoredDeviceChallenge | null;
    },
    async consume(id, thumbprint, sequence) {
      const { data, error } = await admin.rpc("complete_license_device_challenge", {
        p_challenge_id: id,
        p_expected_thumbprint: thumbprint,
        p_expected_sequence: sequence,
      });
      if (error || data === null) throw new DeviceProofError("device_challenge_completion_failed");
      return data;
    },
  }, proof);
  const challenge = verified.challenge;
  const certificate = asJsonMap(verified.result);
  if (!certificate
    || certificate.messageType !== "license_activation_certificate"
    || certificate.certificateVersion !== 2
    || certificate.productCode !== "RETAILSTOREPOS"
    || certificate.installId !== challenge.installId
    || certificate.devicePublicKeyThumbprint !== challenge.devicePublicKeyThumbprint
    || certificate.requestNonce !== challenge.requestNonce
    || certificate.requestSequence !== challenge.requestSequence) {
    throw new DeviceProofError("invalid_activation_certificate");
  }

  return {
    success: true,
    payload: certificate,
    backendSignature: await signCanonicalJson(certificate),
    backendSignatureAlgorithm: "ECDSA-P256-SHA256",
    keyId: getBackendSigningKeyId(),
  };
}
