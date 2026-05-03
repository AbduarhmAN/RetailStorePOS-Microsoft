# Register Close Contract

## Purpose

Define the Sales-owned boundaries for the close-register workflow introduced by feature
`005-close-register-menu`.

## Owning Module

- **Owner**: Sales
- **Reason**: The feature reads and writes `register_sessions` and derives payment totals from
  `sales`, both of which fall under Sales-owned workflow coordination for checkout/register
  operations.

## Contracts

### `RegisterSessionQuery`

```text
ContractKey: "checkout.register.session-query"
OwnerModuleKey: "Sales"
ContractType: Query
Purpose: "Resolve the currently active register session before shell actions are enabled."
Consumers: ["Checkout UI", "Main shell UI"]
OfflineAllowed: true
```

### `RegisterCloseSummaryQuery`

```text
ContractKey: "checkout.register.close-summary"
OwnerModuleKey: "Sales"
ContractType: Query
Purpose: "Return the active-session Cash and Card totals needed by the close-register dialog."
Consumers: ["Main shell UI"]
OfflineAllowed: true
```

### `RegisterCloseCommand`

```text
ContractKey: "checkout.register.close"
OwnerModuleKey: "Sales"
ContractType: Command
Purpose: "Persist the close timestamp, counted cash, and closing note for the active register session."
Consumers: ["Main shell UI"]
OfflineAllowed: true
```

## Workflow Boundary: `RegisterCloseBoundary`

```text
WorkflowKey: "checkout.register-close"
CoordinatorModuleKey: "Sales"
ParticipatingModules: ["Users/Auth"]
OfflineCritical: true
WriteSequence:
  1. "Legacy upgraded databases with zero register-session rows resolve as no active session."
  2. "Shell UI resolves the active session through checkout.register.session-query."
  3. "Shell UI loads close totals through checkout.register.close-summary."
  4. "User enters counted cash and optional note; difference is calculated locally in the dialog."
  5. "Sales persists the register close through checkout.register.close."
  6. "UI triggers local logout through Users/Auth only after the Sales write succeeds."
```

## Interface Methods

```csharp
// RegisterSessionRepository (Sales module)
RegisterSession? GetActiveSession();
RegisterCloseSummary? GetActiveCloseSummary();
void CloseRegister(long sessionId, long closingAmountCents, string? closingNote);
```

## Boundary Rules

- UI may not write directly to `register_sessions` or `sales`.
- Logout remains a UI-triggered Users/Auth concern after a successful Sales write.
- Zero `register_sessions` rows after legacy upgrade is valid and must not be treated as an
  implicit active session.
- No network dependency may be introduced into the close-register flow.
