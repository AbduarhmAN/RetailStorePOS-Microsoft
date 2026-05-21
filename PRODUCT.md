# Product

## Register

product

## Users

Three audiences share a single Windows desktop install:

- **Cashier (primary).** Stationed at a register in a small or medium retail
  store. High-throughput, often standing, often with a customer waiting.
  Logs in via fast PIN — picks their badge card, types a 4-digit PIN, no
  username typing. Spends most of the day in Checkout: scanning barcodes,
  picking products by name, applying discounts, taking cash or card,
  printing or PDF-ing receipts, watching low-stock alerts. Opens the
  register at the start of shift with a counted opening-cash amount and
  closes it at end of shift with a counted-cash reconciliation.
- **Manager / Owner (secondary).** Same machine, separate account, full
  username + password login. Lives in Dashboard, Reports, Products,
  Settings, Users. Reviews sales vs. target, gross profit, top movers,
  low stock, register differences. Imports product catalogs from CSV
  with explicit column mapping, exports for backup, edits product master
  data, configures tax authorities / rules / groups, manages cashier
  accounts and roles, maintains store metadata.
- **Administrator (tertiary).** Bootstraps the system, performs schema
  migrations, audits security and licensing, handles backup and restore.

## Product Purpose

Retail Store POS by Nexill is a Windows-native, local-first point-of-sale
and store-operations app for small and medium retail stores on Windows 10
and Windows 11. It exists so that staff can ring up sales without making
the customer wait, owners can understand what the store did today and
what to do tomorrow, and the data the business runs on stays on the
machine and stays correct.

The full operational loop — log in, open register, scan or search, ring
up a sale, take payment, print a receipt, view dashboard, close register
— completes with zero network dependency. Online services exist only as
an enhancement layer (telemetry outbox, optional sync, future paid-feature
verification). No cardholder data is stored.

Success looks like: a new cashier trained in under ten minutes; a manager
reading the dashboard in under ten seconds and knowing whether the day
went well; an auditor reconstructing a closed register exactly from
saved register-session, sales, and audit-log records; and a customer at
the counter who sees a calm, professional screen, never a flashy demo.

## Brand Personality

Three words: **operational, calm, trustworthy**.

Confident without flashy. Dense but legible. Fast. Professional. A tool
for adults at work. Voice in UX writing is short, factual, present-tense,
and second-person where it helps ("Pick a CSV column for Name before you
can import"). Never cute. Never marketing. Never an exclamation point
unless something actually failed.

Reference products that capture the right feel:
- Square for Retail and Lightspeed Retail — operational POS density,
  large primary action, clean cart-and-total hierarchy.
- Stripe Dashboard — number-heavy reporting that stays readable; calm,
  restrained color.
- Linear — keyboard-first speed, restrained motion, sharp typographic
  hierarchy, no decoration for decoration's sake.
- Microsoft 365 / Fluent productivity surfaces — fits because we are a
  native WinUI 3 app and should feel at home on Windows 11, not like a
  web app pretending to be desktop.

## Anti-references

Things this product must not look like:

- Generic "AI SaaS" landing aesthetic: purple-to-blue gradients,
  oversized hero cards, glassmorphism, marketing pages on every screen.
- Cards-inside-cards-inside-cards. (A real, audited problem in the
  current codebase.)
- "Inter for everything" generic webfont typography. We run on Windows;
  the platform-native default belongs here.
- Bouncy, elastic, or playful easing — feels dated and unprofessional
  for a cash-handling tool.
- Gray text on colored backgrounds, neon accents, decorative
  emoji-as-icons.
- Consumer-mobile shopping-app vibe. This is a staff tool, not a
  customer-facing storefront.
- Anything that reads "demo" or "concept". Receipts, totals, and stock
  counts must look like they belong in a real shop.

## Design Principles

1. **Speed of operation beats visual delight.** The checkout primary
   action is hit thousands of times a day. Affordances, target sizes,
   and keyboard paths come before animation or color flourishes.
2. **Numbers are the product.** Totals, taxes, change due, stock levels,
   register differences are what people are actually reading. Hierarchy,
   alignment, and contrast for numerals are first-class concerns;
   decorative chrome must never compete with them.
3. **Local-first is a UX principle, not just an architecture.** The UI
   never implies "checking with the server." Offline is the normal
   state. Loading patterns, optimistic writes, and recovery messaging
   all assume the network is optional.
4. **Two-tier audience, one app.** Cashier surfaces stay extremely
   focused — ring, take payment, close register. Manager surfaces
   accept density — tables, filters, multi-step config. Don't dumb down
   manager screens to fit cashier patterns, and don't burden cashier
   screens with manager controls.
5. **Quiet confidence, no marketing.** No badges, no sparkle emoji, no
   inspirational empty states. Empty states say what to do next in one
   sentence.
6. **Trust before novelty.** When the choice is between "looks more
   modern" and "behaves predictably," behave predictably. Keep keyboard
   focus visible. Never auto-dismiss confirmations of destructive
   actions. Never animate numbers that the user is actively reading.

## Accessibility & Inclusion

- **WCAG 2.1 AA** target across all production surfaces, with elevated
  contrast and target-size on Checkout (operator under load, sometimes
  on a touch screen).
- **Full keyboard operation.** Every cashier task — login, ring item,
  apply discount, take payment, complete sale, print — is reachable
  without a mouse. Visible focus rings everywhere; the system focus
  visual is never stripped.
- **Theme-aware and high-contrast safe.** All text-and-background pairs
  resolve through theme-aware brushes; no hard-coded hex pairs.
  Production surfaces survive Windows high-contrast themes.
- **Reduced motion.** Honors the OS setting; all non-essential motion
  (skeleton shimmer, chart entry animations) drops to a static state
  when reduced motion is on.
- **Color is never the only signal.** Status (mapped, skipped,
  required-missing, success, warning, error) always carries an icon and
  a text label as well.
- **Internationalization.** Full localization in English (en-US),
  Arabic (ar-SA) with RTL flow direction, and French (fr-FR) in
  progress. Layouts mirror correctly under RTL. Numerals and currency
  use locale-aware formatting. Strings tolerate roughly 30% expansion
  for translation without truncation.
- **Currency flexibility.** Country and currency are decoupled — a
  store in the US may transact in GBP. Currency symbol placement
  respects locale.
