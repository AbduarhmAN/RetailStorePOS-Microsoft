import { createClient } from "npm:@supabase/supabase-js@2";
import {
  computeLicenseKeyHash,
  getBackendSigningKeyId,
  signCanonicalJson,
} from "../utils/crypto.ts";

const PRODUCT_CODE = "RETAILSTOREPOS";
const CERTIFICATE_VERSION = 1;
const PROTOCOL_VERSION = 1;
const CERTIFICATE_TTL_DAYS = 30;

type JsonMap = Record<string, unknown>;

type LicenseRequestBody = {
  payload?: JsonMap;
  [key: string]: unknown;
};

type VerifyLicenseRequest = {
  installId: string;
  licenseKey: string;
  productCode: string;
  protocolVersion: number;
  devicePublicKeyThumbprint: string | null;
  requestNonce: string | null;
  requestSequence: number | null;
  requestTimeUtc: string | null;
};

type InstallationRow = {
  install_id: string;
};

type LicenseRow = {
  id: string;
  product_code: string;
  permission_group: string;
  status: string;
  max_devices: number;
  starts_at: string | null;
  expires_at: string | null;
};

type ActivationRow = {
  id: string;
  status: string;
  last_request_sequence: number;
  activated_at: string;
  revoked_at?: string | null;
  revoke_reason?: string | null;
};

type PermissionGroupRow = {
  permission_group: string;
  status: string;
};

type PermissionFeatureRow = {
  feature_code: string;
};

type SeatClaimRow = {
  activation_id: string | null;
  activation_status: string | null;
  last_request_sequence: number | null;
  activated_at: string | null;
  result_code: string;
};

export async function handleVerifyLicense(body: LicenseRequestBody) {
  const request = parseVerifyLicenseRequest(body);
  const supabaseAdmin = createSupabaseAdminClient();
  const now = new Date();
  const canonicalInstallId = await resolveCanonicalInstallId(
    supabaseAdmin,
    request.installId,
  );

  await ensureInstallationExists(supabaseAdmin, canonicalInstallId);

  const licenseKeyHash = await computeLicenseKeyHash(request.licenseKey);
  const license = await loadLicense(supabaseAdmin, licenseKeyHash, request.productCode);

  validateLicenseState(license, now);

  await ensurePermissionGroupIsActive(
    supabaseAdmin,
    license.product_code,
    license.permission_group,
  );

  const existingActivation = await loadExistingActivation(
    supabaseAdmin,
    license.id,
    canonicalInstallId,
  );

  // Terminal-state check: revoked is final. We refuse to re-issue a certificate
  // or refresh the row even if the user re-presents the same license key.
  if (existingActivation && existingActivation.status === "revoked") {
    throw new Error("activation_revoked");
  }

  validateRequestSequence(existingActivation, request.requestSequence);

  const activation = existingActivation
    ? await refreshExistingActivation(
      supabaseAdmin,
      existingActivation,
      request,
      now,
    )
    : await claimNewSeat(supabaseAdmin, license, canonicalInstallId, request, now);

  const features = await loadFeatures(
    supabaseAdmin,
    license.product_code,
    license.permission_group,
  );

  const certificateExpiresAtUtc = chooseCertificateExpiry(now, license.expires_at);

  const payload = {
    messageType: "license_activation_certificate",
    certificateVersion: CERTIFICATE_VERSION,
    activationCertificateId: crypto.randomUUID(),
    licenseId: license.id,
    activationId: activation.id,
    installId: canonicalInstallId,
    productCode: license.product_code,
    permissionGroup: license.permission_group,
    features,
    licenseStatus: license.status,
    activationStatus: "active",
    issuedAtUtc: now.toISOString(),
    notBeforeUtc: now.toISOString(),
    expiresAtUtc: certificateExpiresAtUtc,
    requestNonce: request.requestNonce,
    requestSequence: request.requestSequence ?? activation.last_request_sequence,
  };

  const backendSignature = await signCanonicalJson(payload);

  return {
    success: true,
    payload,
    backendSignature,
    backendSignatureAlgorithm: "ECDSA-P256-SHA256",
    keyId: getBackendSigningKeyId(),
  };
}

async function resolveCanonicalInstallId(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  installId: string,
) {
  const normalizedInstallId = installId.trim();
  const { data, error } = await supabaseAdmin.rpc("resolve_canonical_install_id", {
    p_install_id: normalizedInstallId,
  });

  if (error) {
    console.error("install id alias lookup failed", {
      installId: normalizedInstallId,
      error,
    });
    throw new Error("installation_lookup_failed");
  }

  return asOptionalString(data) ?? normalizedInstallId;
}

function parseVerifyLicenseRequest(body: LicenseRequestBody): VerifyLicenseRequest {
  const source = asJsonMap(body.payload) ?? body;

  const installId = asRequiredString(source.installId, "missing_install_id");
  const licenseKey = asRequiredString(source.licenseKey, "missing_license_key");
  const productCode = asOptionalString(source.productCode) ?? PRODUCT_CODE;
  const protocolVersion = asRequiredNumber(
    source.protocolVersion,
    "missing_protocol_version",
  );

  if (protocolVersion !== PROTOCOL_VERSION) {
    throw new Error("unsupported_protocol_version");
  }

  const messageType = asOptionalString(source.messageType);
  if (messageType && messageType !== "license_activation_request") {
    throw new Error("invalid_message_type");
  }

  return {
    installId,
    licenseKey,
    productCode,
    protocolVersion,
    devicePublicKeyThumbprint: asOptionalString(source.devicePublicKeyThumbprint),
    requestNonce: asOptionalString(source.requestNonce),
    requestSequence: asOptionalInteger(source.requestSequence),
    requestTimeUtc: asOptionalString(source.requestTimeUtc),
  };
}

function createSupabaseAdminClient() {
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

async function ensureInstallationExists(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  installId: string,
) {
  const { data, error } = await supabaseAdmin
    .from("installations")
    .select("install_id")
    .eq("install_id", installId)
    .maybeSingle();

  if (error) {
    console.error("installation lookup failed", { installId, error });
    throw new Error("installation_lookup_failed");
  }

  const installation = data as InstallationRow | null;

  if (!installation) {
    throw new Error("installation_not_found");
  }
}

async function loadLicense(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  licenseKeyHash: string,
  productCode: string,
) {
  const { data, error } = await supabaseAdmin
    .from("licenses")
    .select(
      "id, product_code, permission_group, status, max_devices, starts_at, expires_at",
    )
    .eq("license_key_hash", licenseKeyHash)
    .eq("product_code", productCode)
    .maybeSingle();

  if (error) {
    console.error("license lookup failed", { productCode, error });
    throw new Error("license_lookup_failed");
  }

  const license = data as LicenseRow | null;

  if (!license) {
    throw new Error("license_not_found");
  }

  return license;
}

function validateLicenseState(license: LicenseRow, now: Date) {
  switch (license.status) {
    case "active":
    case "trial":
      break;
    case "revoked":
      throw new Error("license_revoked");
    case "suspended":
      throw new Error("license_suspended");
    case "expired":
      throw new Error("license_expired");
    default:
      throw new Error("license_status_invalid");
  }

  if (license.starts_at && new Date(license.starts_at).getTime() > now.getTime()) {
    throw new Error("license_not_started");
  }

  if (license.expires_at && new Date(license.expires_at).getTime() <= now.getTime()) {
    throw new Error("license_expired");
  }
}

async function ensurePermissionGroupIsActive(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  productCode: string,
  permissionGroup: string,
) {
  const { data, error } = await supabaseAdmin
    .from("permission_groups")
    .select("permission_group, status")
    .eq("product_code", productCode)
    .eq("permission_group", permissionGroup)
    .maybeSingle();

  if (error) {
    console.error("permission group lookup failed", {
      productCode,
      permissionGroup,
      error,
    });
    throw new Error("permission_group_lookup_failed");
  }

  const permissionGroupRow = data as PermissionGroupRow | null;

  if (!permissionGroupRow || permissionGroupRow.status !== "active") {
    throw new Error("permission_group_not_active");
  }
}

async function loadExistingActivation(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  licenseId: string,
  installId: string,
) {
  const { data, error } = await supabaseAdmin
    .from("license_activations")
    .select("id, status, last_request_sequence, activated_at, revoked_at, revoke_reason")
    .eq("license_id", licenseId)
    .eq("install_id", installId)
    .maybeSingle();

  if (error) {
    console.error("activation lookup failed", { licenseId, installId, error });
    throw new Error("activation_lookup_failed");
  }

  return data as ActivationRow | null;
}

function validateRequestSequence(
  existingActivation: ActivationRow | null,
  requestSequence: number | null,
) {
  if (!existingActivation || requestSequence === null) {
    return;
  }

  if (requestSequence <= existingActivation.last_request_sequence) {
    throw new Error("request_sequence_replayed");
  }
}

async function claimNewSeat(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  license: LicenseRow,
  installId: string,
  request: VerifyLicenseRequest,
  now: Date,
): Promise<ActivationRow> {
  const { data, error } = await supabaseAdmin.rpc("claim_license_seat", {
    p_license_id: license.id,
    p_install_id: installId,
    p_max_devices: license.max_devices,
    p_device_public_key_thumbprint: request.devicePublicKeyThumbprint,
    p_request_sequence: request.requestSequence ?? 0,
    p_now: now.toISOString(),
  });

  if (error) {
    console.error("activation seat claim failed", {
      licenseId: license.id,
      installId,
      error,
    });
    throw new Error("activation_seat_claim_failed");
  }

  const rows = (data as SeatClaimRow[] | null) ?? [];
  const row = rows[0];

  if (!row) {
    throw new Error("activation_seat_claim_failed");
  }

  switch (row.result_code) {
    case "max_devices_reached":
      throw new Error("max_devices_reached");
    case "created":
    case "existing":
      if (!row.activation_id) {
        throw new Error("activation_seat_claim_failed");
      }
      return {
        id: row.activation_id,
        status: row.activation_status ?? "active",
        last_request_sequence: row.last_request_sequence ?? 0,
        activated_at: row.activated_at ?? now.toISOString(),
      };
    default:
      console.error("activation seat claim returned unexpected result", { row });
      throw new Error("activation_seat_claim_failed");
  }
}

async function refreshExistingActivation(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  existingActivation: ActivationRow,
  request: VerifyLicenseRequest,
  now: Date,
): Promise<ActivationRow> {
  const { data, error } = await supabaseAdmin.rpc("refresh_license_activation", {
    p_activation_id: existingActivation.id,
    p_device_public_key_thumbprint: request.devicePublicKeyThumbprint,
    p_request_sequence: request.requestSequence ?? existingActivation.last_request_sequence,
    p_now: now.toISOString(),
  });

  if (error) {
    console.error("activation refresh failed", {
      activationId: existingActivation.id,
      error,
    });
    throw new Error("activation_refresh_failed");
  }

  const rows = (data as SeatClaimRow[] | null) ?? [];
  const row = rows[0];
  if (!row) {
    throw new Error("activation_refresh_failed");
  }

  switch (row.result_code) {
    case "activation_revoked":
      throw new Error("activation_revoked");
    case "not_found":
      throw new Error("activation_lookup_failed");
    case "refreshed":
      if (!row.activation_id) {
        throw new Error("activation_refresh_failed");
      }
      return {
        id: row.activation_id,
        status: row.activation_status ?? "active",
        last_request_sequence: row.last_request_sequence ?? existingActivation.last_request_sequence,
        activated_at: row.activated_at ?? existingActivation.activated_at,
      };
    default:
      console.error("activation refresh returned unexpected result", { row });
      throw new Error("activation_refresh_failed");
  }
}

async function loadFeatures(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  productCode: string,
  permissionGroup: string,
) {
  const { data, error } = await supabaseAdmin
    .from("permission_group_features")
    .select("feature_code")
    .eq("product_code", productCode)
    .eq("permission_group", permissionGroup)
    .eq("enabled", true)
    .order("feature_code", { ascending: true });

  if (error) {
    console.error("feature lookup failed", { productCode, permissionGroup, error });
    throw new Error("feature_lookup_failed");
  }

  const featureRows = (data as PermissionFeatureRow[] | null) ?? [];
  return [...new Set(featureRows.map((item) => item.feature_code))];
}

function chooseCertificateExpiry(now: Date, licenseExpiresAt: string | null) {
  const certificateExpiry = new Date(
    now.getTime() + CERTIFICATE_TTL_DAYS * 24 * 60 * 60 * 1000,
  );

  if (!licenseExpiresAt) {
    return certificateExpiry.toISOString();
  }

  const licenseExpiry = new Date(licenseExpiresAt);
  return new Date(
    Math.min(certificateExpiry.getTime(), licenseExpiry.getTime()),
  ).toISOString();
}

function asJsonMap(value: unknown): JsonMap | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as JsonMap)
    : null;
}

function asRequiredString(value: unknown, errorCode: string): string {
  const text = asOptionalString(value);
  if (!text) {
    throw new Error(errorCode);
  }

  return text;
}

function asOptionalString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function asRequiredNumber(value: unknown, errorCode: string): number {
  const numberValue = asOptionalNumber(value);
  if (numberValue === null) {
    throw new Error(errorCode);
  }

  return numberValue;
}

function asOptionalNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function asOptionalInteger(value: unknown): number | null {
  return typeof value === "number" && Number.isInteger(value) ? value : null;
}
