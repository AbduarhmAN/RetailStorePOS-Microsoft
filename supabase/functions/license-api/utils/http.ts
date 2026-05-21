/**
 * Response helpers.
 *
 * The desktop client talks to this Edge Function via .NET HttpClient (not a
 * browser), so CORS is not required for the production caller. We deliberately
 * do NOT emit `Access-Control-Allow-Origin: *` to prevent third-party browser
 * pages from invoking the function on behalf of an unsuspecting user.
 *
 * If a future browser-based admin tool is added, register its origin via the
 * LICENSE_API_ALLOWED_ORIGIN secret (comma-separated list) and we will echo it
 * back when the request `Origin` header matches.
 */

const SECURITY_HEADERS: Record<string, string> = {
  "Content-Type": "application/json; charset=utf-8",
  "Cache-Control": "no-store",
  "Strict-Transport-Security": "max-age=31536000; includeSubDomains",
  "X-Content-Type-Options": "nosniff",
};

const ALLOWED_REQUEST_HEADERS = "authorization, x-client-info, apikey, content-type";

function readAllowedOrigins(): string[] {
  const raw = Deno.env.get("LICENSE_API_ALLOWED_ORIGIN") ?? "";
  return raw
    .split(",")
    .map((value) => value.trim())
    .filter((value) => value.length > 0);
}

export function buildResponseHeaders(requestOrigin?: string | null): Headers {
  const headers = new Headers(SECURITY_HEADERS);

  if (!requestOrigin) {
    return headers;
  }

  const allowed = readAllowedOrigins();
  if (allowed.includes(requestOrigin)) {
    headers.set("Access-Control-Allow-Origin", requestOrigin);
    headers.set("Vary", "Origin");
    headers.set("Access-Control-Allow-Headers", ALLOWED_REQUEST_HEADERS);
    headers.set("Access-Control-Allow-Methods", "POST, OPTIONS");
  }

  return headers;
}

export function jsonResponse(body: unknown, status = 200, requestOrigin?: string | null): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: buildResponseHeaders(requestOrigin),
  });
}

export function jsonError(errorCode: string, status = 400, requestOrigin?: string | null): Response {
  return jsonResponse(
    {
      success: false,
      errorCode,
      timestamp: new Date().toISOString(),
    },
    status,
    requestOrigin,
  );
}

export function preflightResponse(requestOrigin?: string | null): Response {
  // Only respond with CORS headers if the origin is in the allowlist.
  // Otherwise we return 204 with no Access-Control-Allow-Origin so the browser
  // blocks the call.
  return new Response(null, {
    status: 204,
    headers: buildResponseHeaders(requestOrigin),
  });
}
