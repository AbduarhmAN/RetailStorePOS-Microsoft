# Inventory Module UI

This folder will host inventory-aligned UI workflow code inside the WinUI app.

## Ownership Note

- Inventory UI must reflect Inventory-owned stock rules.
- Product and checkout screens may show stock context, but they should not become the owner of stock movement behavior.
- Any inventory-facing workflow should stay local-first and support offline operation.
