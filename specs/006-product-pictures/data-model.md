# Data Model: Product Pictures

## Product

Existing catalog entity owned by `RetailStorePOS.Data.Modules.Products`.

### Added Field

| Field | Type | Required | Owner | Notes |
|-------|------|----------|-------|-------|
| `ThumbnailPath` | `string?` | No | Products | App-owned relative thumbnail reference. Null means no product image. |

### SQLite Column

| Table | Column | Type | Default | Migration |
|-------|--------|------|---------|-----------|
| `products` | `thumbnail_path` | `TEXT` | `NULL` | Additive, idempotent migration after current version 17. |

### Rules

- Existing products must keep `thumbnail_path = NULL` after migration.
- `thumbnail_path` must never contain the original selected source image location.
- `thumbnail_path` must point only to an app-created thumbnail under the app-owned product media folder.
- A product with `thumbnail_path = NULL` is a valid no-image product.
- Product identity remains `Product.Id`; replacing an image changes only `ThumbnailPath`.

## Product Thumbnail File

Optimized app-created file used by checkout cards.

### Fields / Metadata

| Field | Source | Notes |
|-------|--------|-------|
| Relative path | Stored in `Product.ThumbnailPath` | Example: `ProductMedia/Thumbnails/{guid}.jpg`. |
| Absolute path | Resolved at runtime | Combine app data root with relative path. Do not store absolute path in SQL. |
| Format | Generated thumbnail image | Prefer JPEG for product photos unless implementation finds source constraints requiring another supported format. |

### Lifecycle

```text
No Image
  -> user selects source image in Products page
Pending Source Selection
  -> save starts thumbnail generation
Generated Thumbnail Pending Save
  -> product save succeeds
Saved Thumbnail Reference
  -> user replaces image and save succeeds
Saved Thumbnail Reference (new path), old thumbnail cleanup best-effort
  -> user presses remove/cross and save succeeds
No Image, old thumbnail cleanup best-effort
```

### Validation Rules

- Missing/unreadable thumbnail files must fall back to no-image checkout card behavior.
- Cleanup failure for old app-created thumbnails must not fail product save/delete.
- If thumbnail generation fails, pending image state is cleared and product save can proceed as no-image.
- Source image file is read only for thumbnail generation and must not be copied, modified, tracked, deleted, or persisted.

## Source Image Selection

Temporary UI-only state in `ProductsPage` / draft model.

### Fields

| Field | Type | Persistence | Notes |
|-------|------|-------------|-------|
| `PendingSourceImagePath` | `string?` | UI memory only | Used only until thumbnail generation completes or fails. |
| `PendingRemoveImage` | `bool` | UI memory only | Set when user presses remove/cross on existing image. |
| `PreviewImagePath` | `string?` | UI memory only | May show selected source or saved thumbnail preview while editing. |

### Rules

- Pressing remove/cross while editing marks removal and visually returns the image control to the no-image state.
- The saved SQL reference and saved thumbnail are not removed until product save succeeds.
- Canceling edit must restore the previously saved product image state.

## Search Result Item

UI model used by checkout product cards.

### Added/Derived Fields

| Field | Type | Notes |
|-------|------|-------|
| `ThumbnailPath` | `string?` | Forwarded from `Product.ThumbnailPath`. |
| `HasThumbnail` | `bool` | True only when `ThumbnailPath` is non-empty and the resolved file exists/read succeeds. |
| `ThumbnailImageSource` | UI image source or path binding | Used by checkout XAML image section. |
| `PriceText` | `string` | Formatted price text for bottom card section. |

### Rules

- If `HasThumbnail` is false, checkout must use the existing no-image card layout.
- If `HasThumbnail` is true, checkout uses the image-backed layout:
  - top image section fills remaining card height,
  - bottom section shows product name and price,
  - product name wraps to two lines without font shrink,
  - image section may shrink inside the fixed card when two-line name is needed.

## Repository Changes

### ProductRepository

Affected methods must include `thumbnail_path` consistently:

- `Search`
- `GetByBarcode`
- `GetByIds`
- `GetById`
- `Create`
- `Update`
- `MapProduct`

### Delete Flow

`ProductRepository.Delete(long id)` removes only the database row. UI/service code that knows the thumbnail path should resolve and clean up the old app-created thumbnail best-effort after the delete succeeds.

## Migration

Add a new migration step after version 17:

```sql
ALTER TABLE products ADD COLUMN thumbnail_path TEXT;
```

The migration must check `ColumnExists(conn, "products", "thumbnail_path")` before altering the table.
