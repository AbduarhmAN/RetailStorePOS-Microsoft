# Inventory Module

This module owns stock position, stock movement rules, and availability constraints.

## Stock Ownership Note

- Inventory owns shelf and warehouse stock rules.
- Sales may trigger stock changes only through Inventory-owned paths.
- Products may display stock information, but product master data does not own stock movement rules.
- Offline-critical stock updates must remain local.
