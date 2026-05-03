# Tasks: Multi-Language Support (i18n)

**Input**: Design documents from `/specs/007-multi-language/`
**Prerequisites**: plan.md (✅), spec.md (✅), research.md (✅), data-model.md (✅), contracts/ (✅)

**Tests**: Not requested — no test tasks generated.

**Organization**: Tasks grouped by user story. Page-by-page string extraction per clarification decision.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the localization helper, settings keys, and app startup wiring

- [x] T001 Add `AppLanguageKey` and `StoreLanguageKey` constants to `RetailStorePOS.Data/Modules/Contracts/SettingsPlatformContract.cs`
- [x] T002 Add `GetAppLanguage()`, `SetAppLanguage()`, `GetStoreLanguage()`, `SetStoreLanguage()` methods to `RetailStorePOS.Data/SettingsRepository.cs`
- [x] T003 Create `LocalizationHelper` static class with `GetString()`, `Format()`, `IsRtl`, `SupportedLanguages`, and `DefaultLanguage` in `Nexill.RetailStorePOS/Common/LocalizationHelper.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire language loading at app startup and RTL at window level — MUST complete before any page-level work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Add `PrimaryLanguageOverride` initialization in `App()` constructor before `InitializeComponent()` in `Nexill.RetailStorePOS/App.xaml.cs` — read `app_language` from `SettingsRepository`, set override if not `"en"`
- [x] T005 Add `FlowDirection.RightToLeft` logic to `MainWindow` constructor/Loaded in `Nexill.RetailStorePOS/MainWindow.xaml.cs` — set RTL when `LocalizationHelper.IsRtl` is true

**Checkpoint**: Foundation ready — language loads from DB on startup, RTL applies for Arabic. User story implementation can now begin.

---

## Phase 3: User Story 1 — Language Selection in Store Management (Priority: P1) 🎯 MVP

**Goal**: Admin can select UI language and store language from dropdowns in Store Management. Changing UI language shows "restart required" message. Store language takes effect immediately for receipts.

**Independent Test**: Select "العربية" from dropdown → restart → confirm login page loads in RTL.

### Implementation for User Story 1

- [x] T006 Add "Language & Region" card to `StoreManagementPage.xaml` — include `ComboBox` for App Language and Store Language, plus `InfoBar` for "Restart Required" message
- [x] T007 Add logic to `StoreManagementPage.xaml.cs` (or associated ViewModel) to load current language settings, populate dropdowns from `LocalizationHelper.SupportedLanguages`, and show `InfoBar` when a new app language is selected

**Checkpoint**: Language can be selected and persisted. App restart applies the new language. MVP complete.

---

## Phase 4: User Story 2 — English String Extraction (Priority: P1)

**Goal**: Extract all hardcoded English strings from every page, component, and dialog into `Strings/en/Resources.resw` and wire via `x:Uid` (XAML) + `ResourceLoader` (code-behind). App looks identical after extraction.

**Independent Test**: Run app in English after extraction — zero visual or functional regressions on every page.

### Implementation for User Story 2 — Page by Page

#### Batch 1: Login + Shell (core flow)

- [ ] T008 [US2] Wire `x:Uid` for all static strings and `ResourceLoader` for code-behind strings in `Nexill.RetailStorePOS/Views/LoginPage.xaml` — replace `Text="..."` with `x:Uid="Login_..."`, remove hardcoded text
- [ ] T009 [US2] Replace all hardcoded strings in `Nexill.RetailStorePOS/Views/LoginPage.xaml.cs` with `LocalizationHelper.GetString()` / `LocalizationHelper.Format()` calls
- [ ] T010 [US2] Extract and wire strings from `Nexill.RetailStorePOS/MainWindow.xaml` — sidebar labels ("Retail Store", "Daily store control", "No active register", "Cash In / Out", "Close Register", "Logout"), splash screen text; add corresponding `Shell_*` keys to `Strings/en/Resources.resw`
- [ ] T011 [US2] Replace hardcoded strings in `Nexill.RetailStorePOS/MainWindow.xaml.cs` with `LocalizationHelper.GetString()` calls

#### Batch 2: Checkout flow (3 components + page)

- [ ] T012 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/CheckoutPage.xaml` and wire with `x:Uid`; add `Checkout_*` keys to `Strings/en/Resources.resw`
- [ ] T013 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/Components/CheckoutProductsControl.xaml` and wire with `x:Uid`; add `ProdSearch_*` keys to `Strings/en/Resources.resw`
- [ ] T014 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/Components/CheckoutCartControl.xaml` and wire with `x:Uid`; add `Cart_*` keys to `Strings/en/Resources.resw`
- [ ] T015 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/Components/CheckoutPaymentControl.xaml` and wire with `x:Uid`; add `Payment_*` keys to `Strings/en/Resources.resw`
- [ ] T016 [US2] Replace hardcoded strings in checkout code-behind files (`CheckoutPage.xaml.cs`, `CheckoutProductsControl.xaml.cs`, `CheckoutCartControl.xaml.cs`, `CheckoutPaymentControl.xaml.cs`) with `LocalizationHelper.GetString()` calls

#### Batch 3: Products + Reports

- [ ] T017 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/ProductsPage.xaml` and wire with `x:Uid`; add `Products_*` keys to `Strings/en/Resources.resw`
- [ ] T018 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/ReportsPage.xaml` and wire with `x:Uid`; add `Reports_*` keys to `Strings/en/Resources.resw`
- [ ] T019 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml` and wire with `x:Uid`; add `Dashboard_*` keys to `Strings/en/Resources.resw`
- [ ] T020 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/ReportsReceiptsPage.xaml` and wire with `x:Uid`; add `Receipts_*` keys to `Strings/en/Resources.resw`
- [ ] T021 [US2] Replace hardcoded strings in reports code-behind files (`ReportsDashboardPage.xaml.cs`, `ReportsReceiptsPage.xaml.cs`) with `LocalizationHelper.GetString()` calls

#### Batch 4: Settings area

- [ ] T022 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/SettingsPage.xaml` and wire with `x:Uid`; add `Settings_*` keys to `Strings/en/Resources.resw`
- [ ] T023 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/StoreManagementPage.xaml` and wire with `x:Uid`; add `StoreMgmt_*` keys to `Strings/en/Resources.resw` (including the new language card from T006)
- [ ] T024 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/TaxConfigurationPage.xaml` and wire with `x:Uid`; add `Tax_*` keys to `Strings/en/Resources.resw`
- [ ] T025 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/MyPreferencesPage.xaml` and wire with `x:Uid`; add `Prefs_*` keys to `Strings/en/Resources.resw`

#### Batch 5: Users + About + Placeholder

- [ ] T026 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/UsersPage.xaml` and wire with `x:Uid`; add `Users_*` keys to `Strings/en/Resources.resw`
- [ ] T027 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/AboutPage.xaml` and wire with `x:Uid`; add `About_*` keys to `Strings/en/Resources.resw`
- [ ] T028 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/PlaceholderPage.xaml` and wire with `x:Uid`; add `Placeholder_*` keys to `Strings/en/Resources.resw`
- [ ] T029 [US2] Replace hardcoded strings in code-behind files for Users, About, Placeholder pages with `LocalizationHelper.GetString()` calls

#### Batch 6: Dialogs

- [ ] T030 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/OpenRegisterDialog.xaml` and wire with `x:Uid`; add `OpenReg_*` keys to `Strings/en/Resources.resw`
- [ ] T031 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml` and wire with `x:Uid`; add `CloseReg_*` keys to `Strings/en/Resources.resw`
- [ ] T032 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/CashInOutDialog.xaml` and wire with `x:Uid`; add `CashIO_*` keys to `Strings/en/Resources.resw`
- [ ] T033 [P] [US2] Extract strings from `Nexill.RetailStorePOS/Views/TimeSyncDialog.xaml` and wire with `x:Uid`; add `TimeSync_*` keys to `Strings/en/Resources.resw`
- [ ] T034 [US2] Replace hardcoded strings in dialog code-behind files (`OpenRegisterDialog.xaml.cs`, `CloseRegisterDialog.xaml.cs`, `CashInOutDialog.xaml.cs`, `TimeSyncDialog.xaml.cs`) with `LocalizationHelper.GetString()` calls

**Checkpoint**: All 20 XAML files + code-behind are fully wired. App running in English is visually identical to pre-localization baseline.

---

## Phase 5: User Story 3 — Arabic Translation Skeleton (Priority: P2)

**Goal**: Create `Strings/ar/Resources.resw` with all keys, empty values, and English comments. When Arabic is selected, layout flips to RTL and shows fallback English text (until user fills translations).

**Independent Test**: Set language to `ar` → restart → confirm RTL layout on all pages, English fallback text visible.

### Implementation for User Story 3

- [ ] T035 [US3] Generate `Nexill.RetailStorePOS/Strings/ar/Resources.resw` — copy all keys from `Strings/en/Resources.resw`, set all `<value>` elements to empty string, add `<comment>` with the English value for translator reference

**Checkpoint**: Arabic skeleton ready. RTL works end-to-end. Translator can begin filling values.

---

## Phase 6: User Story 4 — French Translation Skeleton (Priority: P2)

**Goal**: Create `Strings/fr/Resources.resw` with all keys, empty values, and English comments. When French is selected, text shows fallback English (until user fills translations).

**Independent Test**: Set language to `fr` → restart → confirm LTR layout, English fallback text visible.

### Implementation for User Story 4

- [ ] T036 [P] [US4] Generate `Nexill.RetailStorePOS/Strings/fr/Resources.resw` — copy all keys from `Strings/en/Resources.resw`, set all `<value>` elements to empty string, add `<comment>` with the English value for translator reference

**Checkpoint**: French skeleton ready. Translator can begin filling values.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, edge cases, and documentation

- [ ] T037 Verify fallback behavior: temporarily remove a key from `Strings/ar/Resources.resw`, confirm app shows English fallback without crash
- [ ] T038 Verify invalid `app_language` value in settings (e.g., `"xx"`) defaults to English gracefully in `Nexill.RetailStorePOS/App.xaml.cs`
- [ ] T039 Review all pages in Arabic RTL mode for layout breakage — check donut chart, progress bars, and custom-drawn elements; document any manual adjustments needed
- [ ] T040 Update feature spec status to "Complete" in `specs/007-multi-language/spec.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 Language Selection (Phase 3)**: Depends on Phase 2 — can start immediately after
- **US2 String Extraction (Phase 4)**: Depends on Phase 2 — can run in parallel with Phase 3
- **US3 Arabic Skeleton (Phase 5)**: Depends on Phase 4 (needs all keys extracted first)
- **US4 French Skeleton (Phase 6)**: Depends on Phase 4 — can run in parallel with Phase 5
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Language Selection)**: Phase 2 only — no dependency on other stories
- **US2 (English Extraction)**: Phase 2 only — no dependency on other stories. Can run parallel with US1.
- **US3 (Arabic Skeleton)**: Depends on US2 completion (needs all keys)
- **US4 (French Skeleton)**: Depends on US2 completion (needs all keys). Can run parallel with US3.

### Within User Story 2 (String Extraction)

- Batches 1-6 are sequential (page by page, per clarification decision)
- Within each batch, tasks marked [P] can run in parallel (different XAML files)
- Code-behind tasks depend on their XAML counterpart completing first

### Parallel Opportunities

- T001, T002, T003 can all run in parallel (different files)
- T004 and T005 can run in parallel (different files)
- Within each batch of US2: all [P] XAML extraction tasks can run in parallel
- US1 (Phase 3) and US2 (Phase 4) can run in parallel
- US3 (Phase 5) and US4 (Phase 6) can run in parallel

---

## Parallel Example: User Story 2, Batch 2

```
# Launch all checkout XAML extractions together:
Task T012: Extract strings from CheckoutPage.xaml
Task T013: Extract strings from CheckoutProductsControl.xaml
Task T014: Extract strings from CheckoutCartControl.xaml
Task T015: Extract strings from CheckoutPaymentControl.xaml

# Then after all complete:
Task T016: Wire code-behind for all checkout files
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T005)
3. Complete Phase 3: US1 Language Selection (T006-T007)
4. **STOP and VALIDATE**: Language dropdown works, saves to DB, restart loads new language
5. Ship as MVP — language infrastructure is in place

### Incremental Delivery

1. Setup + Foundational → Language system boots ✅
2. US1 Language Selection → Admin can pick language → MVP! ✅
3. US2 English Extraction → All strings externalized (page by page) ✅
4. US3 Arabic Skeleton → RTL layout works, translator can start ✅
5. US4 French Skeleton → French translator can start ✅
6. Polish → Edge case validation ✅

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Page-by-page extraction order: Login → MainWindow → Checkout → Products → Reports → Settings → Users → Dialogs
- Each batch within US2 should be visually verified before moving to next
- Commit after each batch completion for safe rollback points
- LoginPage already has keys in `Strings/en/Resources.resw` — T008/T009 wire the existing keys, not re-extract them
