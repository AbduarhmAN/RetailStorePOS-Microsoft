import { routeLicenseRequest } from "./router.ts";
import { jsonError, jsonResponse, preflightResponse } from "./utils/http.ts";
import { enforceIpRateLimit } from "./utils/rate-limit.ts";
import { tryDecryptEnvelope } from "./utils/envelope.ts";

Deno.serve(async (req) => {
  const requestOrigin = req.headers.get("origin");

  if (req.method === "OPTIONS") {
    return preflightResponse(requestOrigin);
  }

  if (req.method !== "POST") {
    return jsonError("method_not_allowed", 405, requestOrigin);
  }

  const rateLimit = await enforceIpRateLimit(req);
  if (!rateLimit.allowed) {
    const response = jsonError("rate_limited", 429, requestOrigin);
    response.headers.set("Retry-After", String(rateLimit.resetSeconds));
    response.headers.set("X-RateLimit-Remaining", "0");
    response.headers.set("X-RateLimit-Reset", String(rateLimit.resetSeconds));
    return response;
  }

  try {
    const rawBody = await req.json();
    // Backward-compatible envelope decryption: encrypted callers wrap the
    // request in an "encrypted_license_verification_envelope". Plain-JSON
    // callers pass through unchanged. The backend keeps accepting both during
    // the rollout so app builds can flip to encryption independently of the
    // Edge Function deploy.
    const decrypted = await tryDecryptEnvelope(rawBody);
    const result = await routeLicenseRequest(decrypted);
    const response = jsonResponse(result, 200, requestOrigin);
    response.headers.set("X-RateLimit-Remaining", String(rateLimit.remaining));
    response.headers.set("X-RateLimit-Reset", String(rateLimit.resetSeconds));
    return response;
  } catch (error) {
    const errorCode = error instanceof Error ? error.message : "unknown_error";

    console.error("license-api failed", {
      errorCode,
      timestamp: new Date().toISOString(),
    });

    return jsonError(errorCode, statusForErrorCode(errorCode), requestOrigin);
  }
});

function statusForErrorCode(errorCode: string) {
  switch (errorCode) {
    case "device_proof_not_enabled":
      return 503;
    case "method_not_allowed":
      return 405;
    case "rate_limited":
      return 429;
    case "backend_not_configured":
    case "license_key_pepper_not_configured":
    case "signing_key_not_configured":
    case "signing_key_invalid":
    case "decryption_key_not_configured":
    case "decryption_key_invalid":
    case "installation_lookup_failed":
    case "license_lookup_failed":
    case "permission_group_lookup_failed":
    case "activation_lookup_failed":
    case "activation_count_failed":
    case "activation_refresh_failed":
    case "activation_create_failed":
    case "activation_seat_claim_failed":
    case "feature_lookup_failed":
    case "device_challenge_creation_failed":
    case "device_challenge_lookup_failed":
    case "device_challenge_completion_failed":
    case "invalid_activation_certificate":
      return 500;
    default:
      // Most envelope-related errors (envelope_*) and validation failures are
      // 400 — the client sent something malformed.
      return 400;
  }
}
