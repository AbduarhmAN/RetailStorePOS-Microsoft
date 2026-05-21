# Feature Specification: Multi-Language Support (i18n)

**Feature Branch**: `007-multi-language`  
**Created**: 2026-04-28  
**Status**: Draft  
**Input**: User description: "Add multi-language support to the POS app using Approach 1 (x:Uid + ResourceLoader). Support English (default), Arabic, and French."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Language Selection in Settings (Priority: P1)

A store owner or manager navigates to the Settings or Preferences page and selects their preferred language from a dropdown (English, Arabic, or French). After restarting the app, the entire UI displays in the chosen language.

**Why this priority**: The language switcher is the entry point for all localization. Without it, no other language work is accessible to users.

**Independent Test**: Can be tested by selecting "العربية" from the language dropdown, restarting the app, and confirming all visible labels on the login page appear in Arabic.

**Acceptance Scenarios**:

1. **Given** the app is running in English, **When** the user selects "Français" from the language dropdown and restarts, **Then** the login page, navigation, and all static labels display in French.
2. **Given** the app is running in Arabic, **When** the user selects "English" and restarts, **Then** the UI reverts to English.
3. **Given** the user has never changed the language setting, **When** the app launches, **Then** it defaults to English.

---

### User Story 2 - English String Extraction (Priority: P1)

All hardcoded user-facing strings across every page (LoginPage, CheckoutPage, ProductsPage, ReportsPage, SettingsPage, UsersPage, etc.) and all dialogs (OpenRegisterDialog, CloseRegisterDialog, CashInOutDialog, TimeSyncDialog) are extracted into `Strings/en/Resources.resw` and wired via `x:Uid` (XAML) or `ResourceLoader` (code-behind).

**Why this priority**: This is the prerequisite for all translations. If English strings are not extracted, no other language can be added.

**Independent Test**: After extraction, the app running in English looks and behaves identically to the pre-localization version — zero visual or functional regressions.

**Acceptance Scenarios**:

1. **Given** all strings are extracted, **When** the app runs with language set to "en", **Then** every page and dialog displays the same English text as before.
2. **Given** a string key has a typo in `x:Uid`, **When** the app runs, **Then** the element shows blank text (fail-safe: no crash).

---

### User Story 3 - Arabic Translation (Priority: P2)

A `Strings/ar/Resources.resw` file is created containing Arabic translations for all extracted keys. When the user switches to Arabic, the UI displays in Arabic with right-to-left (RTL) layout direction.

**Why this priority**: Arabic is the first target translation language. It also exercises RTL layout, which is the hardest localization challenge.

**Independent Test**: Switch to Arabic, navigate to every page, confirm all labels are in Arabic and the layout flows right-to-left.

**Acceptance Scenarios**:

1. **Given** language is set to Arabic, **When** the login page loads, **Then** all static text appears in Arabic and the layout is right-to-left.
2. **Given** language is Arabic, **When** the user opens the checkout page, **Then** the product search, cart, and payment bar are mirrored to RTL.

---

### User Story 4 - French Translation (Priority: P2)

A `Strings/fr/Resources.resw` file is created containing French translations for all extracted keys. When the user switches to French, the UI displays in French with standard left-to-right layout.

**Why this priority**: French is the second target language. It exercises character length differences (French text is typically ~15-20% longer than English).

**Independent Test**: Switch to French, navigate to every page, confirm all labels are in French and no text truncation occurs.

**Acceptance Scenarios**:

1. **Given** language is set to French, **When** the reports dashboard loads, **Then** all KPI labels, card titles, and tooltips display in French.
2. **Given** French text is longer than English, **When** viewing the checkout bar, **Then** all buttons and labels fit without truncation or overflow.

---

### Edge Cases

- What happens when a string key exists in `en` but is missing from `ar`? (Expected: falls back to English)
- What happens when the saved language preference references a locale that no longer exists?
- How does RTL affect custom-drawn elements (donut chart, progress bars)?
- What happens to receipt printing when the UI language is Arabic?
- How are dynamically-generated strings handled (e.g., "3 orders: $450.00")?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST use `.resw` resource files under `Strings/{locale}/Resources.resw` with the WinUI 3 MRT Core resource pipeline.
- **FR-002**: System MUST support three locales at launch: `en` (English, default), `ar` (Arabic), `fr` (French).
- **FR-003**: XAML static strings MUST use `x:Uid` attribute for automatic resource resolution.
- **FR-004**: Code-behind dynamic strings MUST use `ResourceLoader.GetString()`.
- **FR-005**: System MUST persist the selected language in the settings database (`app_language` key).
- **FR-006**: System MUST apply `ApplicationLanguages.PrimaryLanguageOverride` before `InitializeComponent()` in `App.xaml.cs` on startup.
- **FR-007**: System MUST set `FlowDirection.RightToLeft` on the root layout when the active language is Arabic. This applies universally to every page, dialog, and component — no exceptions.
- **FR-008**: System MUST fall back to English when a resource key is missing in the active locale.
- **FR-009**: Language change MUST require an app restart to take effect.
- **FR-010**: System MUST display a user-facing message indicating restart is required after language change.
- **FR-011**: Printed receipts and exported PDF reports MUST use a separate "store language" setting (key `store_language`), independent of the UI language. Default: English.
- **FR-012**: Numbers on receipts and reports MUST always use Western digits (0-9), regardless of the store language or UI language.
- **FR-013**: The language selection UI (dropdown for both UI language and store language) MUST be placed in the Store Management page, accessible to admins/managers only.

### Key Entities

- **Resource File** (`Resources.resw`): XML file containing Name/Value pairs for all UI strings in one locale.
- **Language Preference**: A setting stored in the `settings` table with key `app_language` and value being a BCP 47 tag (e.g., `en`, `ar`, `fr`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of user-facing static strings across all 13 XAML pages and 4 dialogs are extracted into `.resw` files.
- **SC-002**: App running in English after extraction is visually identical to pre-localization baseline.
- **SC-003**: Switching to Arabic results in RTL layout on all pages with zero layout breakage.
- **SC-004**: Switching to French results in correct French text on all pages with no truncation.
- **SC-005**: Missing translation keys fall back to English without crash or blank UI.

## Assumptions

- The app already uses `SettingsRepository` for persisting key-value settings.
- `App.xaml.cs` is the correct startup entry point where `PrimaryLanguageOverride` can be set before UI initialization.
- Arabic and French translations will be provided manually by the user/translator. Implementation delivers empty `.resw` skeletons with all keys and English-language comments for context.
- Receipt content language is independent of UI language (receipts use `store_language` setting, default English, changeable to any supported locale).
- Receipt numbers (prices, quantities, totals) always render in Western digits (0-9), never Arabic-Indic digits.
- Currency formatting, number formatting, and date formatting already respect `CultureInfo` and are unaffected by this feature.
- String extraction and `x:Uid` wiring will be done page by page (one page converted and tested before moving to the next). Mixed converted/unconverted pages are expected during development.

## Clarifications

### Session 2026-04-28

- Q: Which pages and elements should receive RTL layout flipping when Arabic is active? → A: Full RTL — every page, dialog, and component flips to RTL with no exceptions.
- Q: Should printed receipts and exported PDF reports follow the UI language or stay fixed? → A: Separate store language setting (default English), numbers always Western digits (0-9), store language changeable only to supported UI languages.
- Q: Where should the language selection UI be placed? → A: In the Store Management page, alongside other store-wide configuration (admin/manager access only).
- Q: How will Arabic and French translations be produced? → A: Manual only. Empty `.resw` skeletons with all keys are created; user provides translations.
- Q: Should we convert all pages at once or page by page? → A: Page by page. Convert one page, test it, then move to next. No runtime performance difference.
