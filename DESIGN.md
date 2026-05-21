---
name: Retail Store POS
description: Windows-native point-of-sale for small retail — operational, calm, trustworthy.
colors:
  operational-blue: "#1976D2"
  accent-blue: "#3B82F6"
  accent-blue-deep: "#1565C0"
  accent-blue-pressed: "#0D47A1"
  nexill-purple: "#2D1B69"
  paper-white: "#F9FAFB"
  card-white: "#FFFFFF"
  soft-fill: "#FAFBFC"
  ink: "#1A1D23"
  graphite: "#5F6775"
  secondary-gray: "#8F96A3"
  ruled-line: "#E8EAF0"
  row-divider: "#F1F4F9"
  success-bg: "#ECFDF5"
  success-border: "#A7F3D0"
  success-text: "#065F46"
  warning-bg: "#FFFBEB"
  warning-border: "#FDE68A"
  warning-text: "#92400E"
  danger-bg: "#FEF2F2"
  danger-border: "#FCA5A5"
  danger-text: "#DC2626"
  info-bg: "#EFF6FF"
  info-border: "#BFDBFE"
  info-text: "#1D4ED8"
  pos-footer: "#1E293B"
typography:
  display:
    fontFamily: "Segoe UI Variable, Segoe UI, sans-serif"
    fontSize: "36px"
    fontWeight: 700
    lineHeight: 1.1
  headline:
    fontFamily: "Segoe UI Variable, Segoe UI, sans-serif"
    fontSize: "28px"
    fontWeight: 600
    lineHeight: 1.2
  title:
    fontFamily: "Segoe UI Variable, Segoe UI, sans-serif"
    fontSize: "22px"
    fontWeight: 600
    lineHeight: 1.3
  body:
    fontFamily: "Segoe UI Variable, Segoe UI, sans-serif"
    fontSize: "15px"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Segoe UI Variable, Segoe UI, sans-serif"
    fontSize: "11px"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "0.04em"
rounded:
  none: "0px"
  sm: "3px"
  md: "6px"
  lg: "8px"
  xl: "12px"
  dialog: "14px"
  login: "20px"
spacing:
  xs: "6px"
  sm: "8px"
  md: "14px"
  lg: "16px"
  xl: "20px"
  xxl: "22px"
components:
  button-primary:
    backgroundColor: "{colors.operational-blue}"
    textColor: "{colors.card-white}"
    rounded: "{rounded.sm}"
    padding: "14px 6px"
  button-primary-hover:
    backgroundColor: "{colors.accent-blue-deep}"
    textColor: "{colors.card-white}"
    rounded: "{rounded.sm}"
    padding: "14px 6px"
  button-ghost:
    backgroundColor: "{colors.card-white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "14px 7px"
  button-danger:
    backgroundColor: "{colors.card-white}"
    textColor: "{colors.danger-text}"
    rounded: "{rounded.md}"
    padding: "14px 7px"
  button-flat:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "8px 4px"
  card-standard:
    backgroundColor: "{colors.card-white}"
    rounded: "{rounded.xl}"
    padding: "22px"
  input-default:
    backgroundColor: "{colors.card-white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    height: "36px"
---

# Design System: Retail Store POS

## 1. Overview

**Creative North Star: "The Quiet Register"**

This is a system that disappears into the task. The cashier sees totals, tax breakdowns, and change due. The manager sees today's revenue, top movers, and register differences. Neither notices the interface. The design exists to make numbers legible, actions immediate, and errors impossible to miss.

The aesthetic is Notion-meets-Fluent: warm neutrals, a single blue accent used sparingly for primary actions, flat surfaces separated by thin ruled lines rather than shadows, and a typographic hierarchy built on weight and tracking rather than color or decoration. The system is dense where density serves the operator (tables, dashboards, tax configuration) and spacious where focus matters (checkout, login).

What this system explicitly rejects: generic "AI SaaS" landing aesthetics with purple-to-blue gradients and glassmorphism on every surface; cards-inside-cards-inside-cards nesting; bouncy or elastic easing; gray text on colored backgrounds; consumer-mobile shopping-app energy; anything that reads "demo" or "concept." Receipts, totals, and stock counts look like they belong in a real shop.

**Key Characteristics:**
- Flat surfaces, 1px borders, no shadows (except ambient Translation on checkout cart cards)
- Single accent color (Operational Blue) reserved for primary actions and current selection only
- Tracked uppercase labels (`CharacterSpacing="40"`) as the metadata signal
- Platform-native typography (Segoe UI Variable) at a tight fixed scale
- Square global CornerRadius (0) with intentional radius on interactive elements (6px inputs, 12px cards)
- Semantic color vocabulary for states (success/warning/danger/info) always paired with icons and text

## 2. Colors: The Quiet Register Palette

A restrained palette. Tinted neutrals carry the surface; one blue accent at less than 10% of any screen. Brand purple appears only on splash and login hero.

### Primary
- **Operational Blue** (#1976D2): The single action color. Save buttons, confirm actions, primary CTAs. Darkens to #1565C0 on hover, #0D47A1 on press.
- **Accent Blue** (#3B82F6): Section icons, status highlights, lighter accent contexts. Never on buttons directly (use Operational Blue for interactive elements).

### Neutral
- **Paper White** (#F9FAFB): Page background on checkout. Other pages inherit the system theme background.
- **Card White** (#FFFFFF): All section cards, form surfaces, dialog backgrounds.
- **Soft Fill** (#FAFBFC): Table header rows, search box backgrounds, subtle differentiation.
- **Ink** (#1A1D23): Primary text, section titles, KPI values, table cell data. The default reading color.
- **Graphite** (#5F6775): Field labels, table headers, muted metadata. Always paired with `CharacterSpacing="40"`.
- **Secondary Gray** (#8F96A3): Subtitles, helper text, placeholder content. Lower hierarchy than Graphite.
- **Ruled Line** (#E8EAF0): Card borders, input borders, table header separators. The primary structural divider.
- **Row Divider** (#F1F4F9): Soft bottom borders on table rows. Barely visible; just enough to separate.

### Semantic
- **Success**: #ECFDF5 background, #A7F3D0 border, #065F46 text. Change Due chip, receipt confirmation.
- **Warning**: #FFFBEB background, #FDE68A border, #92400E text. Read-only states, unlock reasons.
- **Danger**: #FEF2F2 background, #FCA5A5 border, #DC2626 text. Delete buttons, danger zones, error banners.
- **Info**: #EFF6FF background, #BFDBFE border, #1D4ED8 text. Status messages, quantity pills.

### Brand (login and splash only)
- **Nexill Purple** (#2D1B69): Splash screen base, login hero background image overlay. Never used in operational UI.

### Named Rules

**The Numbers-First Rule.** Totals, taxes, change due, stock levels, and register differences always win the visual hierarchy contest. Numerals get the largest size, the heaviest weight, and the highest contrast on any surface. Decorative chrome never competes with them.

**The One-Accent Rule.** Operational Blue appears on primary actions and current selection only. Its rarity is the point. If more than 10% of a screen is blue, something is wrong.

## 3. Typography

**Primary Font:** Segoe UI Variable (with Segoe UI fallback)

**Character:** Platform-native, variable-weight, optimized for Windows ClearType rendering. No web fonts. The system feels at home on Windows 11 because it uses the same typeface the OS does. Hierarchy is built through size, weight, and tracking rather than font pairing.

### Hierarchy
- **Display** (Bold, 36px, line-height 1.1): Page titles on the login hero. Rare in operational UI.
- **Headline** (SemiBold, 28px, line-height 1.2): KPI values on dashboards. The largest numeral in the system.
- **Title** (SemiBold, 22px, line-height 1.3): Page titles at the top of settings/management pages.
- **Section** (SemiBold, 15px, line-height 1.5): Card section headers. The workhorse heading level.
- **Body** (Regular, 15px, line-height 1.6): Form input text, table cells, dialog content. Max line length 65-75ch for prose.
- **Label** (SemiBold, 11px, line-height 1.4, CharacterSpacing="40"): Field labels, table headers, KPI labels. The tracked-uppercase metadata signal that distinguishes "what this is" from "what it says."
- **Helper** (Regular, 11-12px, line-height 1.4): Below inputs, page subtitles, secondary context. Always in Secondary Gray.

### Named Rules

**The Tracking Rule.** Every label that describes a data field (above inputs, above KPI values, in table headers) uses `CharacterSpacing="40"`. This is the system's single strongest visual signal for "this is metadata, not content." Never apply tracking to body text or values.

## 4. Elevation

This system is flat. Surfaces are separated by 1px Ruled Line borders, not shadows. Depth is conveyed through background color differentiation (Paper White page → Card White surface → Soft Fill inset) rather than vertical layering.

The single exception: checkout cart item cards use `Translation="0,0,4"` for an ambient lift that separates interactive product cards from the static cart list. This is barely perceptible and intentional. It signals "this is tappable/draggable" without introducing a shadow vocabulary.

### Named Rules

**The Flat-By-Default Rule.** No `box-shadow`, no `ThemeShadow`, no elevation tokens. If a new surface needs to feel distinct, use a 1px border or a background color step. Shadows are prohibited outside the checkout cart card exception. If it looks like a Material Design card with a drop shadow, it's wrong.

## 5. Components

Restrained and confident. Every component has a clear default, hover, pressed, and disabled state. No decoration beyond what communicates state.

### Buttons
- **Shape:** Square globally (CornerRadius 0 at the system level), but action buttons use tight radius (3px on primary, 6px on ghost/danger/settings buttons, 8px on top-action-bar buttons).
- **Primary (Operational Blue):** #1976D2 background, white text, 14px horizontal / 6px vertical padding. Hover darkens to #1565C0. Press darkens to #0D47A1. Disabled at 40% opacity. Transition: 80ms.
- **Ghost:** White background, 1px Ruled Line border, Ink text. Hover lightens border. Used for Cancel, Refresh, Import, Export.
- **Danger:** White background, 1px #FCA5A5 border, #DC2626 text, SemiBold weight. Used for Delete, Deactivate, destructive actions.
- **Flat (Notion-style):** Transparent background, no border. Hover shows #EFEFED fill. Used in sidebar navigation items.
- **Row Icon:** 32×32px, transparent, 6px radius. Edit glyph in Graphite, Delete glyph in Danger Red.

### Cards / Containers
- **Corner Style:** Gently curved (12px radius). Dialogs use 14px. Login card uses 20px.
- **Background:** Card White (#FFFFFF), always.
- **Border:** 1px Ruled Line (#E8EAF0). No exceptions.
- **Internal Padding:** 22px standard. 20px on dashboard cards. 0px on cards containing edge-to-edge tables.
- **Danger Variant:** #FFF5F5 background, #FECACA border. Same radius and padding.
- **Spacing between cards:** 16px vertical (StackPanel Spacing="16").

### Inputs / Fields
- **Style:** White background, 1px Ruled Line border, 6px radius, 34-36px height, 15px font size.
- **Focus:** System focus visual (never stripped). No custom glow or border-color shift.
- **Disabled:** Reduced opacity.
- **Layout:** Two-column grids with 14-20px column spacing, 14px row spacing. Each cell is a StackPanel(Spacing=6) of Label + Input.

### Navigation
- **Sidebar:** Notion-style flat items. Transparent background, Graphite text. Hover shows #EFEFED. Selected shows #E8E8E6 with Ink text. 12px horizontal / 8px vertical padding. 4px radius. 4px horizontal margin, 2px vertical margin.
- **Title bar:** Light (#F9FBFD) with 1px bottom border (#E2E8F0). Logo + brand name + context subtitle. User badge dropdown on the right.

### Status Banner
- **Shape:** 8px radius, 1px border, 12px horizontal / 10px vertical padding.
- **Info (default):** #EFF6FF background, #BFDBFE border, #1D4ED8 text.
- **Error:** #FEF2F2 background, #FCA5A5 border, #991B1B text.
- **Visibility:** Collapsed when empty. Never auto-dismisses on destructive confirmations.

### Empty States
- Centered StackPanel, max-width 380px, 32px top padding.
- Title: 15px SemiBold in Ink. Subtitle: 12px Regular in Secondary Gray.
- No decorative icons. No bordered containers. One sentence says what to do next.

## 6. Do's and Don'ts

### Do:
- **Do** use Operational Blue (#1976D2) exclusively for primary actions and current selection. Its scarcity is the signal.
- **Do** apply `CharacterSpacing="40"` to every field label, table header, and KPI label. This is the system's metadata signature.
- **Do** use 1px Ruled Line (#E8EAF0) borders to separate surfaces. Borders are the elevation system.
- **Do** give numerals (totals, taxes, change due, stock counts) the largest size and heaviest weight on any surface. Numbers are the product.
- **Do** use semantic color triples (background + border + text) for all state communication. Color is never the only signal; always pair with an icon and a text label.
- **Do** keep transitions at 80ms or instant. Users are in flow; don't make them wait.
- **Do** honor the OS reduced-motion setting. All non-essential animation (skeleton shimmer, chart entry) drops to static.
- **Do** maintain full keyboard operation for every cashier task. Visible focus rings everywhere.

### Don't:
- **Don't** use shadows or ThemeShadow on any surface except the checkout cart card Translation exception. Flat is the rule.
- **Don't** nest cards inside cards. A card contains content, never another card. This was an audited problem in the codebase; it's now prohibited.
- **Don't** use purple-to-blue gradients, glassmorphism, or oversized hero cards in operational UI. Nexill Purple lives on splash/login only.
- **Don't** use bouncy, elastic, or playful easing. This is a cash-handling tool, not a consumer app.
- **Don't** use gray text on colored backgrounds or neon accents. Every text-background pair must pass WCAG AA.
- **Don't** use Inter, custom web fonts, or display typefaces. Segoe UI Variable is the only font. The app is Windows-native and must feel it.
- **Don't** use `border-left` or `border-right` greater than 1px as a colored accent stripe on cards, list items, or alerts.
- **Don't** use gradient text (`background-clip: text` with a gradient).
- **Don't** use decorative emoji as icons, badge sparkles, or inspirational empty-state copy. Empty states say what to do next in one sentence.
- **Don't** animate numbers that the user is actively reading. KPI values, totals, and change due appear instantly.
- **Don't** auto-dismiss confirmations of destructive actions. The user closes them manually.
- **Don't** make anything look like a "demo" or "concept." Receipts, totals, and stock counts must look like they belong in a real shop.
