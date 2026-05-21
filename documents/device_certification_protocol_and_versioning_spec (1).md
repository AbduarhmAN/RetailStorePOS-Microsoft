# Legacy Draft Notice

This file is a duplicate older draft and should not be treated as the current implementation reference.

Old assumption:

```text
install_id was treated as UUID-oriented in the activation path.
```

New implemented direction:

```text
install_id is hardware-derived on the client and stored as text on the backend.
legacy saved install IDs are migrated once on the client.
the backend temporarily canonicalizes old and new IDs during rollout.
```

Done status:

```text
The implemented system did not complete the old UUID-only path exactly as written here.
It completed a revised path: text install_id in backend, one-time client migration, and temporary backend canonicalization during rollout.
```

Use [device_certification_protocol_and_versioning_spec.md](E:\Projects\Retail_Store\V\2.0.0\documents\device_certification_protocol_and_versioning_spec.md) as the authoritative version.

# License, Payment, and Activation Architecture Review

## 1. Architecture Verdict

The proposed direction is **good in principle, but incomplete and slightly overcomplicated**.

The strong part is the core idea:

```text
payment provider confirms payment
→ backend updates license/subscription state
→ backend issues signed activation certificate
→ app verifies certificate locally
→ app unlocks allowed features from local snapshot
```

That is the right direction for a desktop POS app. The weak part is trying to make the installed app too involved in payment, provider logic, or protocol routing. The app must not decide whether a payment succeeded, whether a subscription is active, or what features are truly allowed. Those decisions belong to backend functions, verified provider webhooks, database state, and signed activation certificates.

### Keep

Keep these ideas:

```text
Signed local activation certificate
Backend-owned activation decisions
Installation/device binding
Permission groups / feature entitlements
Protocol version fields
Offline-first local feature access
Provider abstraction
```

### Change

Change these parts:

```text
Do not build many app-facing versioned endpoints immediately.
Do not make the desktop app provider-specific.
Do not let the app trust checkout success screens.
Do not call Supabase on every feature click.
Do not store raw license keys if avoidable.
Do not overuse database event logs for every normal activation check.
```

### Reject

Reject these ideas for version 1:

```text
Client decides payment success.
Client talks directly to Stripe/Gumroad/Lemon/PayPal for entitlement truth.
Feature access depends on live backend calls.
Provider-specific logic is hard-coded in the app.
Every checkout or feature event creates heavy database history.
```

### Better alternative

Use this instead:

```text
One stable app licensing API
+ provider-specific webhook handlers
+ provider adapter layer
+ minimal license/subscription database
+ signed local activation certificate
+ local feature snapshot
```

That is simpler, safer, cheaper, and easier to change later.

---

## 2. Better Architecture Recommendation

The best architecture is a **backend-owned licensing gateway**.

```text
Desktop app
  → calls your licensing backend only

Licensing backend
  → creates checkout sessions
  → receives verified payment webhooks
  → updates license/subscription state
  → issues signed activation certificates

Payment providers
  → Stripe, Lemon Squeezy, PayPal, Gumroad, others later
```

The app should not care whether payment came from Stripe, Lemon Squeezy, PayPal, or another provider. The app should only care about one thing:

```text
Do I have a valid signed activation certificate from my backend?
```

### Why this is better

| Area | Why this architecture is better |
|---|---|
| Security | Payment truth stays server-side. Webhooks are verified before license state changes. |
| Simplicity | The app talks to one licensing API instead of many provider APIs. |
| Cost | The app uses local certificates and avoids unnecessary backend calls. |
| Future flexibility | Providers can be swapped behind backend adapters. |
| Provider switching | Stripe today, Lemon later, PayPal later without app rewrite. |
| Offline behavior | After activation, the app works from a local signed certificate. |
| Maintainability | Payment logic, entitlement logic, and app runtime logic are separated. |

Supabase Edge Functions fit this backend gateway role because they are server-side TypeScript functions, can centralize security checks, integrate with third parties such as Stripe, and access secrets through environment variables.

---

## 3. Correct End-to-End Flow

### Phase A: App startup

```text
1. App starts.
2. App loads local activation certificate.
3. App verifies backend signature.
4. App checks install_id, expiry, license status, activation status, and feature list.
5. If valid:
      ActivationState = Activated
      Build local feature snapshot
      Do not call backend
6. If missing/expired/invalid:
      ActivationState = NotActivated or NeedsRefresh
```

### Phase B: User starts checkout

```text
1. User clicks Activate / Upgrade.
2. App calls backend: create_checkout.
3. App sends:
      install_id
      product_code
      requested_permission_group
      protocol_version
4. Backend creates checkout session through configured provider adapter.
5. Backend stores checkout_session row as pending.
6. Backend returns checkout_url.
7. App opens browser to checkout_url.
```

The app does **not** assume payment succeeded just because the browser opened or returned.

### Phase C: Provider confirms payment

```text
1. Payment provider sends webhook to backend.
2. Backend verifies webhook signature.
3. Backend deduplicates provider event ID.
4. Backend maps event to checkout/subscription/license.
5. Backend updates subscription/license state.
6. Backend marks checkout as paid/completed if valid.
```

Stripe requires raw request body handling for webhook signature verification, and Lemon Squeezy signs webhook requests using a signing secret and `X-Signature` header. PayPal provides webhook signature verification using the transmission headers and webhook event data.

### Phase D: App waits / refreshes status

```text
1. App shows: Waiting for payment confirmation.
2. App polls backend lightly or user clicks Refresh.
3. Backend returns:
      pending
      paid_ready_for_activation
      failed
      expired
      cancelled
4. If paid_ready_for_activation:
      app calls activate_or_refresh.
```

### Phase E: Backend issues activation certificate

```text
1. App sends signed activation request:
      install_id
      device_public_key_thumbprint
      license_id or checkout_session_id
      request_nonce
      request_sequence
2. Backend validates installation/license/subscription state.
3. Backend creates or refreshes license_activation row.
4. Backend builds LicenseActivationCertificate.
5. Backend signs it.
6. Backend returns signed certificate.
```

### Phase F: App unlocks features locally

```text
1. App verifies backend signature.
2. App checks certificate belongs to local install_id.
3. App checks nonce/sequence.
4. App checks expiry.
5. App stores certificate locally.
6. App builds feature snapshot.
7. Premium features are enabled locally.
```

---

## 4. Protocol Versioning Strategy

Use **one stable app-facing endpoint with internal version handling**.

Recommended app-facing function:

```text
license-api
```

Supported actions:

```text
create_checkout
get_checkout_status
activate_or_refresh
get_license_status
```

Each request includes:

```json
{
  "protocolVersion": 1,
  "action": "activate_or_refresh"
}
```

### Why not many app-facing versioned functions?

Avoid this for version 1:

```text
license-v1-activate
license-v2-activate
license-v3-activate
```

That becomes a maze. It makes the app harder to update, harder to test, and easier to leave old vulnerable paths alive.

### Better model

```text
One stable endpoint:
    license-api

Internal handlers:
    handleV1CreateCheckout()
    handleV1Activate()
    handleV2Activate()
```

### Deprecated version behavior

Backend should maintain:

```text
min_supported_protocol_version
max_supported_protocol_version
preferred_protocol_version
blocked_protocol_versions
```

If the app sends an old vulnerable version:

```json
{
  "success": false,
  "errorCode": "protocol_too_old",
  "updateRequired": true,
  "minSupportedProtocolVersion": 2
}
```

If the app sends a newer version than backend supports:

```json
{
  "success": false,
  "errorCode": "protocol_too_new",
  "updateRequired": false
}
```

### Provider webhooks are different

Use provider-specific webhook functions:

```text
webhook-stripe
webhook-lemonsqueezy
webhook-paypal
webhook-gumroad  // only after confirming official webhook/signature behavior
```

The reason is that webhook signature verification differs by provider. Stripe requires the raw body and `Stripe-Signature`, Lemon Squeezy uses `X-Signature` HMAC, and PayPal has its own verify-webhook-signature API.

---

## 5. Backend Function Strategy

Recommended function layout:

```text
license-api
webhook-stripe
webhook-lemonsqueezy
webhook-paypal
```

Optional later:

```text
admin-license-api
```

### `license-api`

App-facing. Handles:

```text
create_checkout
get_checkout_status
activate_or_refresh
get_license_status
```

It should:

```text
validate protocolVersion
validate install_id
validate request shape
never trust client payment claims
call provider adapter only when creating checkout
issue signed activation certificates only after backend license state is valid
```

### `webhook-stripe`

Handles only Stripe webhooks.

```text
read raw body
verify Stripe-Signature
deduplicate event ID
normalize event into internal payment event
update subscription/license state
```

Stripe documents that raw body manipulation breaks signature verification.

### `webhook-lemonsqueezy`

Handles only Lemon Squeezy webhooks.

```text
read raw body
verify X-Signature using signing secret
deduplicate event
normalize event
update subscription/license state
```

Lemon Squeezy documents signing secrets and `X-Signature` verification.

### `webhook-paypal`

Handles only PayPal webhooks.

```text
read webhook headers
call PayPal verify webhook signature or perform supported verification
deduplicate transmission/event ID
normalize event
update subscription/license state
```

PayPal documents a verify-webhook-signature endpoint that returns `SUCCESS` or `FAILURE`.

### Gumroad

Use only after confirmation. Gumroad’s public product pages confirm subscriptions/memberships and software license-key features, but webhook signature and event behavior require confirmation before using it for automatic entitlement changes.

---

## 6. Payment Provider Abstraction

Use a provider adapter pattern.

### Interface

```text
PaymentProviderAdapter
    createCheckoutSession(input)
    verifyWebhook(rawBody, headers)
    normalizeWebhookEvent(providerEvent)
    getProviderName()
```

### Provider implementations

```text
StripeProviderAdapter
LemonSqueezyProviderAdapter
PayPalProviderAdapter
GumroadProviderAdapter  // later, after confirmation
```

### What belongs in configuration

```text
enabled provider
price/product IDs
permission group mapping
success URL
cancel URL
webhook secret names
provider mode: test/live
checkout expiration policy
```

### What belongs in code

```text
signature verification
provider API calls
event normalization
error handling
idempotency handling
mapping provider event → internal event
```

### Internal normalized event

Every provider should become the same internal shape:

```json
{
  "provider": "stripe",
  "providerEventId": "evt_...",
  "eventType": "subscription_active",
  "providerCustomerId": "cus_...",
  "providerSubscriptionId": "sub_...",
  "checkoutSessionId": "cs_...",
  "licenseId": "uuid",
  "status": "active",
  "occurredAtUtc": "..."
}
```

The app never sees provider-specific complexity.

---

## 7. Database Design

Use your existing tables as the foundation:

```text
public.installations
public.installation_events
public.error_logs
public.app_versions
```

Add the minimum license/payment tables.

### Existing table usage

| Table | Use |
|---|---|
| `public.installations` | Installation/device anchor. |
| `public.app_versions` | Mandatory update and release status. |
| `public.installation_events` | Important license events only. |
| `public.error_logs` | Activation and crypto errors. |

### New minimum tables

```text
public.customers
public.licenses
public.permission_groups
public.permission_group_features
public.subscriptions
public.payment_providers
public.checkout_sessions
public.payment_events
public.license_activations
```

### `public.customers`

Needed if license is customer/account-owned.

```text
id
email
display_name
created_at
```

Requires confirmation:

```text
Whether customers are required before Google/third-party sign-in is added.
```

### `public.payment_providers`

```text
id
provider_code       // stripe, lemon, paypal
status
config_json         // non-secret config only
created_at
```

Do not store provider secrets here.

### `public.checkout_sessions`

```text
id
provider_code
provider_checkout_id
install_id
license_id nullable
requested_permission_group
status: pending, completed, expired, cancelled, failed
created_at
expires_at
```

### `public.payment_events`

```text
id
provider_code
provider_event_id unique
event_type
processed_status
raw_event_hash
created_at
processed_at
```

Store raw event only if necessary. A hash plus key fields is often enough for cost and privacy.

### `public.licenses`

Use hashed license keys:

```text
id
license_key_hash
license_key_prefix
product_code
permission_group
status
max_devices
starts_at
expires_at
customer_id nullable
subscription_id nullable
```

### `public.subscriptions`

```text
id
provider_code
provider_customer_id
provider_subscription_id
status
current_period_start
current_period_end
cancel_at_period_end
```

Provider status values should be normalized internally.

### `public.permission_groups`

```text
product_code
permission_group
display_name
status
```

### `public.permission_group_features`

```text
product_code
permission_group
feature_code
enabled
```

### `public.license_activations`

```text
id
license_id
install_id
status
device_public_key_thumbprint
last_request_sequence
activated_at
last_refreshed_at
revoked_at
```

### Protocol versions

For version 1, do **not** create a table unless you need admin control.

Use environment/config first:

```text
MIN_PROTOCOL_VERSION
MAX_PROTOCOL_VERSION
PREFERRED_PROTOCOL_VERSION
```

Create `public.protocol_versions` later only if you need dashboard-controlled protocol blocking.

### RLS

RLS should be enabled for new tables in `public`, and the desktop app should not directly query license/payment tables. Supabase states that RLS should always be enabled on tables in exposed schemas, with `public` being exposed by default; service keys bypass RLS and must not be exposed to customers.

---

## 8. Webhook and Payment Confirmation

The client must not confirm payment success because:

```text
browser redirect can be faked
success URL can be opened manually
client can be patched
local app traffic can be manipulated
provider session IDs can be replayed if not validated
```

Only backend-verified provider webhooks should update payment/license state.

### Webhook verification

| Provider | Verification |
|---|---|
| Stripe | Verify `Stripe-Signature` using raw body and endpoint secret. |
| Lemon Squeezy | Verify `X-Signature` HMAC using signing secret. |
| PayPal | Use PayPal webhook signature verification flow or supported signature validation. |
| Gumroad | Requires confirmation before automatic entitlement. |

Stripe specifically requires the unmodified raw request body for signature verification. Lemon Squeezy signs payloads with a signing secret and sends the hash in `X-Signature`. PayPal provides a verify-webhook-signature API.

### Duplicate events

Every webhook handler must be idempotent:

```text
if provider_event_id already processed:
    return 200 OK
else:
    process event
    store provider_event_id
```

### Retries

Providers retry failed webhooks. Your backend must accept duplicates safely.

PayPal’s documentation says non-2xx webhook responses can cause retries over several days.

### Failed provider callbacks

If webhook verification fails:

```text
do not update license
log security event
return 400 or provider-appropriate failure
```

If provider webhook is valid but database update fails:

```text
return non-2xx if retry is safe
or store failure state and retry internally
```

---

## 9. App Waiting and Status Refresh

For version 1, use a **hybrid polling + manual refresh** approach.

### Recommended v1

```text
After opening checkout:
    app shows Waiting for payment confirmation
    app polls every 5-10 seconds for a short window
    user can click Refresh
    app can also show “I completed payment” button that triggers status check
```

The app must still only trust backend status.

### Why not Realtime first?

Supabase Realtime exists, and Supabase’s docs describe Broadcast and Postgres Changes. Supabase recommends Broadcast for scalability and security, while Postgres Changes is simpler but does not scale as well.

For version 1, Realtime adds complexity:

```text
auth policies
channels
disconnect handling
desktop networking edge cases
more testing
```

So choose:

```text
v1: polling + manual refresh
v2: optional Realtime Broadcast
v3: optional deep link callback
```

### Deep link

Deep link is convenient but optional:

```text
retailstorepos://checkout-complete?session=...
```

It should only trigger a status refresh. It must not activate the app by itself.

---

## 10. Local Activation and Feature Access

The app stores a signed certificate locally:

```text
license_activation_certificate.json
```

Certificate contains:

```text
installId
licenseId
permissionGroup
features
licenseStatus
activationStatus
issuedAtUtc
expiresAtUtc
backendSignature
keyId
```

### App validation

```text
verify backend signature
check installId == local install_id
check productCode
check expiry
check licenseStatus
check activationStatus
build local feature snapshot
```

### Feature snapshot

```json
{
  "permissionGroup": "PREMIUM",
  "features": [
    "AdvancedReports",
    "InventoryAnalytics",
    "ExportReports"
  ],
  "expiresAtUtc": "..."
}
```

### Runtime checks

```text
FeatureAccessService.CanUse("AdvancedReports")
```

Do not call the backend for every feature click because:

```text
offline POS workflow would break
feature access would become slow
backend costs increase
network failures would block the store
client can still patch around online calls
```

This matches your local-first verification notes: validate signed artifacts during load/refresh and use an in-memory runtime snapshot for hot-path checks.

---

## 11. Security Threat Model

| Risk | Impact | Mitigation |
|---|---|---|
| Fake checkout success | User claims paid without payment | Ignore client success; require verified webhook. |
| Replayed activation request | Old request reused | requestNonce + requestSequence + install_id binding. |
| Old vulnerable protocol | Weak app keeps activating | min protocol version, blocked version list, mandatory update. |
| Leaked service key | Full backend access | Never ship service_role key; Edge Function only. |
| Provider spoofing | Fake webhook changes license | Verify provider webhook signatures. |
| Webhook forgery | Fake payment event | Raw-body signature verification or provider verification API. |
| Local certificate tampering | User edits features | Backend signature verification locally. |
| Excessive polling | Cost and abuse | Backoff, short polling window, manual refresh. |
| Client-side feature bypass | Patched desktop app unlocks UI | Service/workflow guards, obfuscation later, signed cert, telemetry. |
| Raw license key leak | Anyone can activate if DB leaks | Store hash + prefix, not raw key. |
| App version text sorting | Wrong update decisions | Add numeric version_code later. |
| SECURITY DEFINER in exposed schema | Privilege exposure | Use non-exposed schema or Edge Function logic. |
| Provider lock-in | Hard to change providers | Provider adapter layer. |

Supabase docs explicitly warn that service role keys bypass RLS and must not be exposed to customers, and RLS should be enabled for exposed `public` tables.

---

## 12. Simplification Check

Do not build these in version 1:

```text
multiple payment providers at once
Realtime waiting
deep links
admin dashboard
customer portal
complex provider-routing UI
full protocol compatibility table
certificate history table
per-feature backend checks
per-request nonce table
complex app-side payment verification
```

Build only:

```text
one provider first, preferably Stripe or Lemon Squeezy
one checkout creation function
one webhook handler
one activation/refresh path
one signed local activation certificate
one local feature snapshot
```

Stripe has the strongest official documentation and tooling for webhooks and Checkout, so it is the safest first provider if you want the most standard SaaS path. Lemon Squeezy may be simpler as merchant-of-record-style tooling, but confirm your exact licensing/subscription needs first. PayPal can be added later. Gumroad requires confirmation before treating it as an automated entitlement provider.

---

## 13. Best Practical Implementation Plan

### Backend tasks

```text
1. Create license-api Edge Function.
2. Create webhook-stripe or webhook-lemonsqueezy for first provider.
3. Store provider secrets in Supabase secrets.
4. Store backend signing key in Supabase secrets or external KMS.
5. Implement signed activation certificate generation.
6. Add protocolVersion handling.
```

### Database tasks

```text
1. Keep public.installations as install/device anchor.
2. Add public.licenses.
3. Add public.permission_groups.
4. Add public.permission_group_features.
5. Add public.subscriptions if using recurring subscriptions.
6. Add public.checkout_sessions.
7. Add public.payment_events.
8. Add public.license_activations.
9. Enable RLS on new public tables.
10. Add indexes for license hash, install_id, provider event ID.
```

### App-side tasks

```text
1. Add InstallationIdentityProvider.
2. Add DeviceKeyService.
3. Add LicenseActivationClient.
4. Add LicenseCertificateValidator.
5. Add LicenseCertificateStore.
6. Add FeatureAccessService.
7. Add Settings → License screen.
8. Add local startup validation.
9. Add background refresh near expiry.
```

### Payment-provider tasks

```text
1. Pick first provider.
2. Create product/price in provider dashboard.
3. Configure webhook endpoint.
4. Store webhook secret.
5. Test paid, failed, cancelled, refunded, subscription expired events.
6. Map provider events to internal subscription/license states.
```

### Testing tasks

```text
1. Valid activation.
2. Invalid license key.
3. Expired license.
4. Revoked license.
5. Device limit reached.
6. Webhook duplicate event.
7. Webhook bad signature.
8. Local certificate tampering.
9. Supabase offline after valid activation.
10. Mandatory update required.
11. Old protocol blocked.
12. Feature denied when not in certificate.
```

### Security tasks

```text
1. Verify RLS on new tables.
2. Verify app contains no service_role key.
3. Verify app contains no backend private signing key.
4. Verify raw webhook body is used for provider signature checks.
5. Verify license keys are stored hashed.
6. Verify local cert replacement is atomic.
7. Verify no feature depends only on UI button visibility.
```

---

## 14. Final Recommended Architecture

Use this clean design:

```text
Existing Supabase database
    public.installations
    public.app_versions
    public.installation_events
    public.error_logs
    + compact license/payment tables

App-facing backend
    license-api
        create_checkout
        get_checkout_status
        activate_or_refresh

Provider webhooks
    webhook-stripe
    webhook-lemonsqueezy
    webhook-paypal later
    Gumroad only after confirmation

Payment abstraction
    ProviderAdapter interface
    Provider-specific adapters behind backend only

App runtime
    signed local activation certificate
    local feature snapshot
    no backend call per feature
```

The simplest secure first production version is:

```text
One payment provider
One license key or subscription
One install_id
One permission group
One signed activation certificate
Local feature access
Webhook-confirmed payment only
```

This preserves future flexibility because the desktop app does not know or care which payment provider exists behind the backend. The provider can change later; the app still speaks the same `license-api` protocol.

That is the architecture I recommend.

---

## Reference Links

- Supabase Edge Functions: https://supabase.com/docs/guides/functions
- Stripe webhook signature verification: https://docs.stripe.com/webhooks/signature?lang=node
- Stripe webhooks: https://docs.stripe.com/webhooks?lang=node
- Lemon Squeezy webhooks: https://docs.lemonsqueezy.com/help/webhooks
- PayPal webhooks API: https://developer.paypal.com/docs/api/webhooks/v1/
- Gumroad features: https://gumroad.com/features
- Supabase Row Level Security: https://supabase.com/docs/guides/database/postgres/row-level-security
- PayPal webhook overview: https://developer.paypal.com/api/rest/webhooks/
- Supabase Realtime database changes: https://supabase.com/docs/guides/realtime/subscribing-to-database-changes
- Supabase Edge Function secrets: https://supabase.com/docs/guides/functions/secrets
