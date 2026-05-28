import { computeLicenseKeyHash } from "../utils/crypto.ts";
import {
  LicenseRequestBody,
  PROTOCOL_VERSION,
  PRODUCT_CODE,
  VerifyLicenseRequest,
  asJsonMap,
  asOptionalInteger,
  asOptionalString,
  asRequiredNumber,
  asRequiredString,
  buildSignedCertificateResponse,
  claimNewSeat,
  createSupabaseAdminClient,
  ensureInstallationExists,
  ensurePermissionGroupIsActive,
  loadExistingActivation,
  loadFeatures,
  loadLicense,
  refreshExistingActivation,
  resolveCanonicalInstallId,
  validateLicenseState,
  validateRequestSequence,
} from "./shared.ts";

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

  return await buildSignedCertificateResponse(
    license,
    activation,
    canonicalInstallId,
    features,
    now,
    request.requestNonce,
    request.requestSequence,
  );
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
