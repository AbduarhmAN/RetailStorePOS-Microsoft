# Register Session Contract

## Purpose

Cross-module coordination for register session lifecycle. Owned by Sales module.

## Contract: RegisterSessionQuery

```
ContractKey: "checkout.register.session-status"
OwnerModuleKey: "Sales"
ContractType: Query
Purpose: "Check if register has an active (open) session."
Consumers: ["Checkout UI", "Reporting"]
OfflineAllowed: true
```

## Contract: RegisterOpenCommand

```
ContractKey: "checkout.register.open"
OwnerModuleKey: "Sales"
ContractType: Command
Purpose: "Create a new register session with opening balance and note."
Consumers: ["Checkout UI"]
OfflineAllowed: true
```

## Workflow Boundary: RegisterOpenBoundary

```
WorkflowKey: "checkout.register-open"
CoordinatorModuleKey: "Sales"
ParticipatingModules: ["Users/Auth"]
OfflineCritical: true
WriteSequence:
  1. "UI validates opening amount and note (or processes Discard as auto-zero)."
  2. "Sales persists register_sessions row via checkout.register.open."
  3. "UI unlocks checkout navigation."
```

## Interface Methods (Repository)

```csharp
// RegisterSessionRepository (Sales module)
RegisterSession? GetActiveSession();
long OpenRegister(long userId, long openingAmountCents, string? openingNote);
void CloseRegister(long sessionId, long closingAmountCents, string? closingNote); // Future
```
