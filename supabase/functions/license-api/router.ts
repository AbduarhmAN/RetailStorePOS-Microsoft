import { handleVerifyLicense } from "./handlers/verify-license.ts";
import { handleReissueByInstall } from "./handlers/reissue-by-install.ts";
import { handleBeginDeviceChallenge } from "./handlers/begin-device-challenge.ts";
import { handleCompleteDeviceProof } from "./handlers/complete-device-proof.ts";

type JsonMap = Record<string, unknown>;

type LicenseRequestBody = {
  action?: string;
  payload?: JsonMap;
  [key: string]: unknown;
};

export async function routeLicenseRequest(input: unknown) {
  const body = asJsonMap(input) as LicenseRequestBody | null;
  if (!body) throw new Error("invalid_request_body");
  const action = readAction(body);

  switch (action) {
    case "verify_license":
      return await handleVerifyLicense(body);
    case "reissue_by_install":
      return await handleReissueByInstall(body);
    case "begin_device_challenge":
      requireDeviceProofEnabled();
      return await handleBeginDeviceChallenge(body);
    case "complete_device_proof":
      requireDeviceProofEnabled();
      return await handleCompleteDeviceProof(readDeviceProof(body));
    default:
      throw new Error("unsupported_action");
  }
}

function requireDeviceProofEnabled() {
  // The transaction draft and client enrollment are not production-ready.
  // Keep v1 behavior unchanged unless a staging operator explicitly opts in.
  if (Deno.env.get("LICENSE_DEVICE_PROOF_V2_ENABLED") !== "true") {
    throw new Error("device_proof_not_enabled");
  }
}

function readDeviceProof(body: LicenseRequestBody): JsonMap {
  const source = body.payload === undefined ? body : asJsonMap(body.payload);
  if (!source) throw new Error("invalid_request_body");
  const { action: nestedAction, ...proof } = source;
  if (nestedAction !== undefined && nestedAction !== "complete_device_proof") {
    throw new Error("invalid_device_proof_fields");
  }
  return proof;
}

function readAction(body: LicenseRequestBody): string {
  if (typeof body?.action === "string" && body.action.trim().length > 0) {
    return body.action.trim();
  }

  const payload = asJsonMap(body?.payload);
  if (typeof payload?.action === "string" && payload.action.trim().length > 0) {
    return payload.action.trim();
  }

  return "";
}

function asJsonMap(value: unknown): JsonMap | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as JsonMap)
    : null;
}
