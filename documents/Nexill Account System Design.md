# Nexill Account System Design

**Date:** 2026-05-14  
**Status:** Planned (P3 — Build with paid features)  
**Depends on:** P1-9 (forced password change), Supabase Auth configuration, License system

---

## Overview

Add an optional online account system ("Nexill Account") that allows the store owner/admin to:
1. Sign up for a Nexill account inside the app
2. Sign in with that account
3. Use it as their local login credential
4. Manage their subscription/license through it

This does NOT replace the current local login system. It is an additive upgrade path.

---

## Current State

- App uses local-only authentication (PIN for cashiers, password for admin)
- First-run creates `admin/1234` (will be forced to change via P1-9)
- No online identity exists
- License verification uses license keys, not user accounts

---

## Target State

```
Local login (unchanged):
  Cashiers → PIN (fast, offline, no change)
  Admin → password (local, offline)

+ Nexill Account (new, optional, admin-only):
  Admin → "Manage Account" button → Sign up / Sign in
  After verified → option to "Use as local login"
  Ties to subscription/license management
```

---

## User Flow

### First-Time Sign-Up

1. Admin logs in locally (with their changed password from P1-9)
2. Admin sees "Manage Account" button (visible only to first admin)
3. Admin presses it → opens in-app account page
4. Admin chooses "Create Nexill Account"
5. Enters: email, password, store name
6. App calls Supabase Auth `signUp(email, password)`
7. Supabase sends verification email automatically
8. User verifies email (clicks link)
9. App confirms verification status
10. Shows: "Account created! You can now use this to log in."
11. Option: "Use as local login" toggle

### Sign-In (Returning User)

1. Admin presses "Manage Account"
2. Chooses "Sign In"
3. Enters email + password
4. App calls Supabase Auth `signInWithPassword(email, password)`
5. On success → session token stored locally (DPAPI)
6. If "Use as local login" is enabled → this becomes their app login credential

### Using as Local Login

When enabled:
- Logout screen shows email/password option alongside PIN cards
- On login attempt with email/password:
  1. First: verify against locally cached credential hash (works offline)
  2. If online: also verify against Supabase Auth (refresh token)
  3. If offline + local cache valid: allow login
- The local cache is updated every time an online login succeeds

### Subscription Management

After sign-in, the "Manage Account" page shows:
- Account email
- Subscription status (Free / Premium / etc.)
- License key entry (if manual activation)
- Device info (install ID, device name)
- "Unlink account" option

---

## Architecture

### Backend (Supabase)

- **Supabase Auth** — handles sign-up, sign-in, email verification, password reset
- **`users_profiles` table** — stores additional user metadata (store name, subscription tier)
- **Existing `licenses` table** — links license to user's auth ID
- **Existing `installations` table** — links device to user's auth ID

### App-Side

- **`NexillAccountService`** — handles sign-up, sign-in, token management
- **`SecureStorageService`** (existing) — stores auth tokens in DPAPI
- **`LoginPage.xaml`** — adds email/password option when "Use as local login" is enabled
- **Settings/Account page** — new page for account management

### Data Flow

```
Sign-up:
  App → Supabase Auth (signUp) → email sent → user verifies → confirmed

Sign-in:
  App → Supabase Auth (signInWithPassword) → JWT token → stored locally

Use as local login:
  On successful online sign-in → hash email+password locally → store in user record
  On next app login → verify against local hash (offline) OR Supabase (online)

License link:
  After sign-in → app sends (auth_user_id + install_id) to license-api
  Backend links the license to the authenticated user
```

---

## Security Considerations

| Concern | Mitigation |
|---------|-----------|
| Supabase down during sign-up | Show "try again later" — one-time setup, not daily use |
| Forgot online password | Supabase Auth has built-in password reset via email |
| Token theft | Short-lived JWT (1 hour) + refresh token in DPAPI |
| Offline login after online setup | Local credential hash cached; works without internet |
| Man-in-the-middle | HTTPS + certificate pinning (P2 roadmap) |
| Someone steals the device | Online account can be deactivated remotely; local PIN still requires knowledge |
| Admin/1234 still exists before account link | P1-9 forces password change immediately; Nexill account is upgrade, not replacement |

---

## Email Verification (Free)

Supabase Auth handles email verification automatically on the free tier:
- Configure custom SMTP in Supabase Dashboard (use your domain)
- Or use Supabase's built-in email (limited but free)
- Verification link sent on sign-up
- App checks `user.email_confirmed_at` to confirm verification

---

## UI Placement

- **"Manage Account" button** — top-right user card area, visible only to first admin
- **Account page** — full page with sign-up/sign-in forms, subscription status, device info
- **Login page** — if "Use as local login" enabled, show email/password field below cashier cards

---

## Subscription Tiers (Naming TBD)

| Tier | Name Options | What It Includes |
|------|-------------|-----------------|
| Free | Free / Basic | Current app features, local-only |
| Tier 1 | Premium / Advanced Analytics / Pro | Advanced reports, cloud backup, priority support |
| Tier 2 | (Future) | Multi-device sync, team management, API access |

The Nexill Account is required for any paid tier. Free tier works without it.

---

## Implementation Order

1. **P1-9 first** — Force bootstrap password change (immediate security fix, no online dependency)
2. **Supabase Auth setup** — Configure auth, email templates, custom domain
3. **`NexillAccountService`** — Sign-up, sign-in, token storage
4. **Account management page** — UI for sign-up/sign-in/status
5. **"Use as local login" feature** — Cache credentials locally, add to login page
6. **License linking** — Connect auth user ID to license/installation
7. **Subscription status display** — Show tier in account page

---

## Relationship to Existing Systems

| System | How Nexill Account Connects |
|--------|---------------------------|
| Local auth (AuthService) | Nexill account is an alternative credential source for admin; cashier PIN unchanged |
| License verification (license-api) | License can be tied to auth user ID instead of just install_id |
| Device certification | Device key + Nexill account = stronger identity proof |
| Telemetry | Can associate telemetry with authenticated user (optional) |
| Future cloud sync | Nexill account = identity for sync authorization |

---

## What This Does NOT Change

- Cashier PIN login (unchanged)
- Offline checkout (unchanged)
- Local database as source of truth (unchanged)
- App works without internet (unchanged)
- Current local admin password login (still works, Nexill account is optional upgrade)
