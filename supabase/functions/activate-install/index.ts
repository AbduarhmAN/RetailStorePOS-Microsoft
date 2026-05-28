import { handleReissueByInstall } from "../license-api/handlers/reissue-by-install.ts";
import { jsonError, jsonResponse, preflightResponse } from "../license-api/utils/http.ts";
import { enforceIpRateLimit } from "../license-api/utils/rate-limit.ts";
import { tryDecryptEnvelope } from "../license-api/utils/envelope.ts";

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
    const decrypted = await tryDecryptEnvelope(rawBody);
    const result = await handleReissueByInstall(decrypted as Record<string, unknown>);
    const response = jsonResponse(result, 200, requestOrigin);
    response.headers.set("X-RateLimit-Remaining", String(rateLimit.remaining));
    response.headers.set("X-RateLimit-Reset", String(rateLimit.resetSeconds));
    return response;
  } catch (error) {
    const errorCode = error instanceof Error ? error.message : "unknown_error";

    console.error("activate-install failed", {
      errorCode,
      timestamp: new Date().toISOString(),
    });

    return jsonError(errorCode, statusForErrorCode(errorCode), requestOrigin);
  }
});

function statusForErrorCode(errorCode: string) {
  switch (errorCode) {
    case "method_not_allowed":
      return 405;
    case "rate_limited":
      return 429;
    case "backend_not_configured":
    case "decryption_key_not_configured":
    case "decryption_key_invalid":
    case "installation_lookup_failed":
    case "license_lookup_failed":
    case "permission_group_lookup_failed":
    case "activation_lookup_failed":
    case "activation_refresh_failed":
    case "feature_lookup_failed":
      return 500;
    default:
      return 400;
  }
}
