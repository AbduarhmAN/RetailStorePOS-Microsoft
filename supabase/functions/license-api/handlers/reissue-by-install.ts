import {
  ActivationRow,
  LicenseRequestBody,
  LicenseRow,
  PRODUCT_CODE,
  PROTOCOL_VERSION,
  ReissueByInstallRequest,
  asJsonMap,
  asOptionalInteger,
  asOptionalString,
  asRequiredNumber,
  asRequiredString,
  buildSignedCertificateResponse,
  createSupabaseAdminClient,
  ensureInstallationExists,
  ensurePermissionGroupIsActive,
  loadFeatures,
  refreshExistingActivation,
  resolveCanonicalInstallId,
  validateLicenseState,
} from "./shared.ts";

type ActivationCandidate = ActivationRow & {
  license_id: string;
};

export async function handleReissueByInstall(body: LicenseRequestBody) {
  const request = parseReissueByInstallRequest(body);
  const supabaseAdmin = createSupabaseAdminClient();
  const now = new Date();
  const canonicalInstallId = await resolveCanonicalInstallId(
    supabaseAdmin,
    request.installId,
  );

  await ensureInstallationExists(supabaseAdmin, canonicalInstallId);

  const activations = await loadInstallActivations(
    supabaseAdmin,
    canonicalInstallId,
  );
  const licenses = await loadLicensesForActivations(
    supabaseAdmin,
    activations,
    request.productCode,
  );
  const selected = selectReissuableActivation(activations, licenses, now);

  await ensurePermissionGroupIsActive(
    supabaseAdmin,
    selected.license.product_code,
    selected.license.permission_group,
  );

  const refreshSequence = request.requestSequence === null
    ? selected.activation.last_request_sequence
    : Math.max(request.requestSequence, selected.activation.last_request_sequence);

  const activation = await refreshExistingActivation(
    supabaseAdmin,
    selected.activation,
    {
      devicePublicKeyThumbprint: request.devicePublicKeyThumbprint,
      requestSequence: refreshSequence,
    },
    now,
  );

  const features = await loadFeatures(
    supabaseAdmin,
    selected.license.product_code,
    selected.license.permission_group,
  );

  return await buildSignedCertificateResponse(
    selected.license,
    activation,
    canonicalInstallId,
    features,
    now,
    request.requestNonce,
    activation.last_request_sequence,
  );
}

function parseReissueByInstallRequest(body: LicenseRequestBody): ReissueByInstallRequest {
  const source = asJsonMap(body.payload) ?? body;

  const installId = asRequiredString(source.installId, "missing_install_id");
  const productCode = asOptionalString(source.productCode) ?? PRODUCT_CODE;
  const protocolVersion = asRequiredNumber(
    source.protocolVersion,
    "missing_protocol_version",
  );

  if (protocolVersion !== PROTOCOL_VERSION) {
    throw new Error("unsupported_protocol_version");
  }

  const messageType = asOptionalString(source.messageType);
  if (
    messageType &&
    messageType !== "license_reissue_request" &&
    messageType !== "license_activation_request"
  ) {
    throw new Error("invalid_message_type");
  }

  return {
    installId,
    productCode,
    protocolVersion,
    devicePublicKeyThumbprint: asOptionalString(source.devicePublicKeyThumbprint),
    requestNonce: asOptionalString(source.requestNonce),
    requestSequence: asOptionalInteger(source.requestSequence),
    requestTimeUtc: asOptionalString(source.requestTimeUtc),
  };
}

async function loadInstallActivations(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  installId: string,
) {
  const { data, error } = await supabaseAdmin
    .from("license_activations")
    .select("id, license_id, status, last_request_sequence, activated_at, updated_at, revoked_at, revoke_reason")
    .eq("install_id", installId)
    .order("updated_at", { ascending: false })
    .limit(20);

  if (error) {
    console.error("install activation lookup failed", { installId, error });
    throw new Error("activation_lookup_failed");
  }

  return ((data as ActivationCandidate[] | null) ?? [])
    .filter((item) => typeof item.license_id === "string" && item.license_id.length > 0);
}

async function loadLicensesForActivations(
  supabaseAdmin: ReturnType<typeof createSupabaseAdminClient>,
  activations: ActivationCandidate[],
  productCode: string,
) {
  const licenseIds = [...new Set(activations.map((item) => item.license_id))];
  if (licenseIds.length === 0) {
    return new Map<string, LicenseRow>();
  }

  const { data, error } = await supabaseAdmin
    .from("licenses")
    .select("id, product_code, permission_group, status, max_devices, starts_at, expires_at")
    .in("id", licenseIds)
    .eq("product_code", productCode);

  if (error) {
    console.error("license lookup failed during install reissue", {
      productCode,
      licenseIds,
      error,
    });
    throw new Error("license_lookup_failed");
  }

  const licenses = new Map<string, LicenseRow>();
  for (const license of ((data as LicenseRow[] | null) ?? [])) {
    licenses.set(license.id, license);
  }

  return licenses;
}

function selectReissuableActivation(
  activations: ActivationCandidate[],
  licenses: Map<string, LicenseRow>,
  now: Date,
) {
  let sawRevoked = false;
  let sawInactiveActivation = false;
  let deferredLicenseError: Error | null = null;

  for (const activation of activations) {
    const license = licenses.get(activation.license_id);
    if (!license) {
      continue;
    }

    if (activation.status === "revoked") {
      sawRevoked = true;
      continue;
    }

    if (activation.status !== "active" && activation.status !== "trial") {
      sawInactiveActivation = true;
      continue;
    }

    try {
      validateLicenseState(license, now);
    } catch (error) {
      if (error instanceof Error) {
        deferredLicenseError = error;
      }
      continue;
    }

    return { activation, license };
  }

  if (sawRevoked) {
    throw new Error("activation_revoked");
  }

  if (deferredLicenseError) {
    throw deferredLicenseError;
  }

  if (sawInactiveActivation) {
    throw new Error("activation_not_active");
  }

  throw new Error("activation_not_found");
}
