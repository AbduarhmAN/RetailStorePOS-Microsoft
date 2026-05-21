# Research: Register Opening Control

**Date**: 2026-04-22

## R1: Register/Shift Data Storage Pattern

**Decision**: New `register_sessions` SQLite table in local DB, owned by Sales module.

**Rationale**: No existing shift/register table. Sales module already owns `sales` and `receipt_sequence`. Register session is a logical parent of sales during a shift. Follows Two-Layer Architecture (constitution §IV) — model + repository in RetailStorePOS.Data.

**Alternatives**:
- Settings KV store → Rejected: structured data with history, not a simple preference.
- Separate "CashManagement" module → Rejected: Overkill for v1. Constitution says "keep it simple." Can extract later.

## R2: Modal Trigger Point

**Decision**: Show `ContentDialog` after login, before navigating to checkout. If register already open, skip.

**Rationale**: Existing login flow ends at `LoginPage.xaml.cs` → navigates to main shell. Insert check between authentication and checkout access. WinUI 3 `ContentDialog` matches existing modal pattern (used in ProductsPage, ReportsReceiptsPage, StoreManagementPage).

**Alternatives**:
- Full Page → Rejected: Spec says "modal window." Consistency with existing codebase.
- Inline panel → Rejected: Doesn't block checkout access cleanly.

## R3: Opening Cash Input Validation

**Decision**: Reuse same currency conversion pattern from `SettingsViewModel.ConvertToQuickCashAmount`. NumberBox with `Minimum="0"`, no spin buttons.

**Rationale**: Already proven pattern in codebase. Handles NaN, overflow, negative. Region-aware display via existing `CurrencyDisplayHelper`.

**Alternatives**:
- TextBox with manual validation → Rejected: NumberBox is native WinUI, gives free numeric enforcement.

## R4: Shift Lifecycle

**Decision**: Single-row "active session" pattern. One register session open at a time. Opening creates row; closing (future feature) marks `closed_at`.

**Rationale**: Single-terminal POS. No multi-register support needed per spec. Simplest model. Future close-of-day can update same row.

**Alternatives**:
- Multi-register tracking → Rejected: Out of scope, no spec requirement.
