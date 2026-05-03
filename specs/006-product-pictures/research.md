# Research: Product Pictures

## Local Thumbnail Storage

**Decision**: Store generated thumbnails under `AppDataPaths.Combine("ProductMedia", "Thumbnails")` and store only a relative thumbnail reference in SQLite.

**Rationale**: The project already centralizes app-owned local storage through `RetailStorePOS.Data/AppDataPaths.cs`, which resolves `%LocalAppData%\RetailStorePOS` with safe fallbacks. This keeps product media outside the installed app package, avoids write-permission problems under MSIX/Program Files locations, and keeps the database lightweight. A relative app-owned path remains stable if the app data root changes across environments.

**Alternatives considered**:
- Store image binaries in SQLite: rejected because it increases database size, backup/copy cost, and checkout memory pressure.
- Store original selected file paths: rejected by requirement; the original source image must be forgotten after thumbnail generation.
- Store files beside the executable: rejected because packaged/installed app directories may not be writable and are worse for user data backup.

## SQL Reference Shape

**Decision**: Add one nullable `products.thumbnail_path TEXT` column through a versioned additive migration.

**Rationale**: A single nullable field is enough to identify an app-created thumbnail and lets old products default to no image without backfill. It fits the existing `Product` model/repository style and avoids adding a separate table before there is any multi-image requirement.

**Alternatives considered**:
- Separate `product_images` table: rejected as unnecessary complexity for one optional thumbnail per product.
- Store absolute paths: rejected because app data root may differ by machine/user and absolute paths expose implementation details.
- Store product ID only and derive path: rejected because replacement images need unique filenames and safe cleanup without filename collisions.

## Thumbnail Generation API

**Decision**: Generate thumbnails in the WinUI app layer with Windows imaging APIs (`Windows.Graphics.Imaging`) from a temporary selected source path, then forget the source path after save processing.

**Rationale**: Image picking and decoding are UI/platform concerns, not Data-layer concerns. Windows imaging APIs avoid adding a new package, support common image inputs, and fit the WinUI/Windows-only target. Running the work asynchronously prevents the Products page from freezing on large source images.

**Alternatives considered**:
- Add ImageSharp or another third-party image library: rejected because no new package is necessary for this Windows-only WinUI app.
- Put thumbnail generation in `RetailStorePOS.Data`: rejected because the Data project should stay pure data/repository logic and avoid UI/platform file-picker concerns.
- Use the existing `System.Drawing.Common` package: possible on Windows, but Windows imaging APIs align better with WinUI and avoid expanding reliance on GDI+ for new UI media work.

## Thumbnail File Naming

**Decision**: Generate filenames with a GUID, for example `{guid}.jpg`, under the thumbnails folder. The product identity stays in SQL, not in the filename.

**Rationale**: GUID filenames avoid collision and do not require a product ID before thumbnail generation. Product edits can replace the thumbnail reference without changing the product ID. Old app-created thumbnails can be cleaned up after the product save succeeds.

**Alternatives considered**:
- Use `product-{id}.jpg`: rejected because new products do not have an ID before create and replacements can conflict with cached image handles.
- Use original filenames: rejected because they can collide, leak source names, and are unrelated after thumbnail generation.

## Checkout Card Rendering

**Decision**: Preserve `CheckoutPage.xaml.cs` card sizing constants and `UpdateSearchResultsGridLayout()` unchanged; update only the `SearchResultItem` data and `CheckoutPage.xaml` item template internals.

**Rationale**: The current algorithm controls cards per row by `SearchResultMinTileWidth`, `SearchResultTileHeight`, `SearchResultTileGap`, and `SearchResultMaxColumns`. Changing those would violate the feature requirement. Internal card layout can use an image row plus bottom product/price row while keeping the fixed outer item size.

**Alternatives considered**:
- Increase `SearchResultTileHeight`: rejected because it changes the card sizing system.
- Use a separate image-only checkout list: rejected because it risks breaking add-to-cart and keyboard flow.

## Long Product Names

**Decision**: Image-backed cards show product name and price in the bottom section. Product name wraps to two lines without shrinking the font; the image section gives up vertical space inside the same fixed card when needed.

**Rationale**: This matches the clarified requirement and keeps the existing card height. The bottom content remains readable while the image remains bounded by the same card.

**Alternatives considered**:
- Shrink font size for long names: rejected by clarification.
- Truncate long names with ellipsis: rejected because full name should be readable.
- Change card height: rejected because the checkout card sizing algorithm must remain unchanged.

## Failure Handling

**Decision**: If thumbnail generation fails, show an image-save error notification, clear the pending image state, and allow product save as no-image without an additional no-image warning.

**Rationale**: The feature must not block product maintenance. A failed image should not corrupt the product record or keep a stale pending path.

**Alternatives considered**:
- Block product save on image failure: rejected by clarification.
- Retain failed pending selection: rejected because it can create repeated save failures and confusing UI.
