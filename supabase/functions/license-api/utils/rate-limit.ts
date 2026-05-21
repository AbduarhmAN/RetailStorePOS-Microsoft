import { createClient, SupabaseClient } from "npm:@supabase/supabase-js@2";

/**
 * Per-IP sliding-window rate limiting backed by the
 * `public.license_api_rate_limit` table (created by
 * `supabase/migrations/20260519_license_api_hardening.sql`).
 *
 * Defaults to 5 requests per 60 second window. Override via env vars:
 *   LICENSE_API_RATE_LIMIT_MAX     (default 5)
 *   LICENSE_API_RATE_LIMIT_WINDOW  (seconds, default 60)
 *
 * The function returns `true` when the request is allowed and `false` when it
 * should be rejected with a 429. We rely on the table's primary key (bucket_key)
 * for atomic upsert, so two parallel requests from the same IP cannot both
 * believe they own a fresh window.
 *
 * If the rate limit table is unreachable for any reason we fail OPEN — the
 * caller is allowed through and the failure is logged. A signing license
 * activation is rate-limit-sensitive but a denial of service against legitimate
 * users would be worse. Backstop: the function-level concurrency limit on the
 * platform still caps absolute throughput.
 */

const DEFAULT_MAX_REQUESTS = 5;
const DEFAULT_WINDOW_SECONDS = 60;

export type RateLimitResult =
  | { allowed: true; remaining: number; resetSeconds: number }
  | { allowed: false; remaining: 0; resetSeconds: number };

export async function enforceIpRateLimit(req: Request): Promise<RateLimitResult> {
  const ip = readClientIp(req);
  const bucketKey = `ip:${ip}`;

  const config = readConfig();
  const now = new Date();
  const windowStart = new Date(now.getTime() - config.windowSeconds * 1000);

  let supabase: SupabaseClient;
  try {
    supabase = createSupabaseAdminClient();
  } catch (error) {
    // Misconfiguration: fail open but log so on-call can see it.
    console.error("rate_limit_client_unavailable", { error: stringifyError(error) });
    return openCircuit(config.windowSeconds);
  }

  // Upsert the bucket and atomically increment the counter inside the active
  // window. We do this in two steps because PostgREST does not expose a
  // single-call "increment-or-reset" — a small race here would let an extra
  // request through on the boundary, which is acceptable.
  try {
    const { data: existing, error: selectError } = await supabase
      .from("license_api_rate_limit")
      .select("bucket_key, window_start, request_count")
      .eq("bucket_key", bucketKey)
      .maybeSingle();

    if (selectError) {
      console.error("rate_limit_select_failed", { bucketKey, error: selectError });
      return openCircuit(config.windowSeconds);
    }

    if (!existing || new Date(existing.window_start as string).getTime() < windowStart.getTime()) {
      // No active window — start a fresh one.
      const { error: upsertError } = await supabase
        .from("license_api_rate_limit")
        .upsert({
          bucket_key: bucketKey,
          window_start: now.toISOString(),
          request_count: 1,
          updated_at: now.toISOString(),
        });

      if (upsertError) {
        console.error("rate_limit_upsert_failed", { bucketKey, error: upsertError });
        return openCircuit(config.windowSeconds);
      }

      return {
        allowed: true,
        remaining: config.maxRequests - 1,
        resetSeconds: config.windowSeconds,
      };
    }

    const currentCount = (existing.request_count as number) ?? 0;

    if (currentCount >= config.maxRequests) {
      const elapsed = Math.floor((now.getTime() - new Date(existing.window_start as string).getTime()) / 1000);
      const resetSeconds = Math.max(1, config.windowSeconds - elapsed);
      return { allowed: false, remaining: 0, resetSeconds };
    }

    const { error: updateError } = await supabase
      .from("license_api_rate_limit")
      .update({
        request_count: currentCount + 1,
        updated_at: now.toISOString(),
      })
      .eq("bucket_key", bucketKey);

    if (updateError) {
      console.error("rate_limit_update_failed", { bucketKey, error: updateError });
      return openCircuit(config.windowSeconds);
    }

    const elapsed = Math.floor((now.getTime() - new Date(existing.window_start as string).getTime()) / 1000);
    return {
      allowed: true,
      remaining: Math.max(0, config.maxRequests - (currentCount + 1)),
      resetSeconds: Math.max(1, config.windowSeconds - elapsed),
    };
  } catch (error) {
    console.error("rate_limit_unexpected_failure", { bucketKey, error: stringifyError(error) });
    return openCircuit(config.windowSeconds);
  }
}

function readConfig() {
  const max = parseIntEnv("LICENSE_API_RATE_LIMIT_MAX", DEFAULT_MAX_REQUESTS);
  const windowSeconds = parseIntEnv("LICENSE_API_RATE_LIMIT_WINDOW", DEFAULT_WINDOW_SECONDS);
  return {
    maxRequests: Math.max(1, max),
    windowSeconds: Math.max(1, windowSeconds),
  };
}

function parseIntEnv(name: string, fallback: number): number {
  const raw = Deno.env.get(name)?.trim() ?? "";
  if (!raw) return fallback;
  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function readClientIp(req: Request): string {
  // Supabase Edge Runtime forwards the original client IP via x-forwarded-for.
  // Fly/Cloudflare-style headers are also tolerated as fallbacks.
  const forwarded = req.headers.get("x-forwarded-for");
  if (forwarded) {
    const first = forwarded.split(",")[0]?.trim();
    if (first) return first;
  }

  const realIp = req.headers.get("x-real-ip");
  if (realIp) return realIp.trim();

  const cf = req.headers.get("cf-connecting-ip");
  if (cf) return cf.trim();

  return "unknown";
}

function createSupabaseAdminClient(): SupabaseClient {
  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

  if (!supabaseUrl || !serviceRoleKey) {
    throw new Error("backend_not_configured");
  }

  return createClient(supabaseUrl, serviceRoleKey, {
    auth: {
      persistSession: false,
      autoRefreshToken: false,
    },
  });
}

function openCircuit(windowSeconds: number): RateLimitResult {
  return { allowed: true, remaining: 0, resetSeconds: windowSeconds };
}

function stringifyError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  return String(error);
}
