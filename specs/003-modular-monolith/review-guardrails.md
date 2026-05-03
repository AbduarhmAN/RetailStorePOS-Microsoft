# Reviewer Guardrails

Use this checklist when reviewing any module-boundary change.

## Required Checks

- Confirm the change names one owning module for the capability being modified.
- Confirm the change names one owning module for any important table or aggregate it writes.
- Confirm cross-module reads use a published contract, query, command, or event.
- Confirm cross-module writes do not bypass the owning module.
- Confirm offline-critical flows stay local and do not gain a mandatory network dependency.
- Confirm platform modules do not absorb business-rule ownership for convenience.

## Forbidden Patterns

- Direct use of another module's private repository implementation.
- Direct mutation of another module's owned table or aggregate.
- New helper classes that hide cross-module ownership or write paths.
- New view-model or service shortcuts that bypass declared contracts.
- Inline network dependency added to checkout or other offline-critical flows.

## Review Questions

1. Which module owns this change?
2. Which owned data assets are read or written?
3. Does another module's private implementation leak across the boundary?
4. Is the write path visible and owner-approved?
5. Does this create a new legacy exception? If yes, was it documented?
