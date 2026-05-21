import { handleVerifyLicense } from "./handlers/verify-license.ts";

type JsonMap = Record<string, unknown>;

type LicenseRequestBody = {
  action?: string;
  payload?: JsonMap;
  [key: string]: unknown;
};

export async function routeLicenseRequest(body: LicenseRequestBody) {
  const action = readAction(body);

  switch (action) {
    case "verify_license":
      return await handleVerifyLicense(body);
    default:
      throw new Error("unsupported_action");
  }
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
