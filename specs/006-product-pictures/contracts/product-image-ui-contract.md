# Contract: Product Image UI and Persistence Flow

This is a WinUI desktop feature contract, not an HTTP API contract.

## Products Page Contract

### Select Image

**Trigger**: User presses the image button while adding or editing a product and chooses a supported image file.

**Expected behavior**:

1. The app opens a file picker filtered to common image types.
2. The selected source path is stored only in transient draft state.
3. The UI shows the selected image preview.
4. No SQL write happens until the user presses Save.
5. The original source image is not copied, modified, deleted, or persisted as a source reference.

### Remove Existing Image

**Trigger**: User presses the remove/cross affordance on an existing image in edit mode.

**Expected behavior**:

1. Draft state sets `PendingRemoveImage = true`.
2. The image control visually returns to its normal no-image state.
3. The saved `products.thumbnail_path` value is not changed yet.
4. The saved thumbnail file is not cleaned up yet.
5. If the user cancels edit, the original saved image remains attached.
6. If the user saves, the SQL reference is set to null and old thumbnail cleanup runs best-effort after save succeeds.

### Save With New Image

**Trigger**: User presses Save with a valid pending selected image.

**Expected behavior**:

1. Product field validation runs first.
2. Thumbnail generation runs asynchronously/background and creates one app-owned thumbnail.
3. Source image location is forgotten after thumbnail generation.
4. Product save writes the new `thumbnail_path`.
5. If this was a replacement, old app-created thumbnail cleanup runs best-effort after save succeeds.
6. Product search index refreshes through the existing `LoginRuntime.RaiseProductsUpdated()` flow.

### Save With Image Generation Failure

**Trigger**: User presses Save and thumbnail generation fails.

**Expected behavior**:

1. Show an image-save error notification using the existing page status/InfoBar pattern.
2. Clear pending image selection and any failed generated path.
3. Do not show a separate no-image warning.
4. Let the user save the product as a no-image product.
5. Product record must not point to a missing/failed thumbnail.

## Checkout Card Contract

### No-Image Product

**Condition**: `Product.ThumbnailPath` is null/empty or resolved thumbnail file cannot be loaded.

**Expected behavior**:

1. Use the current checkout no-image card visual behavior.
2. Preserve item click, keyboard selection, hover, and selected border behavior.
3. Do not change the outer `ItemsWrapGrid` sizing algorithm.

### Image Product

**Condition**: Product has a usable app-owned thumbnail.

**Expected behavior**:

1. The card uses the same outer width and height assigned by `UpdateSearchResultsGridLayout()`.
2. The top visual area displays the product thumbnail.
3. The image is resized to fill the exact image-section width and height.
4. Larger images shrink; smaller images stretch up.
5. The bottom section shows product name and price.
6. Product name wraps to two lines without reducing font size.
7. If the product name needs two lines, the image section shrinks inside the same fixed card to make room.
8. The existing add-to-cart path remains `SearchResultsListView_ItemClick` -> `ViewModel.HandleSearchResultClick(resultItem)`.

## Storage Contract

### App-Owned Folder

```text
{AppDataPaths root}/ProductMedia/Thumbnails/
```

### SQL Reference

```text
products.thumbnail_path = "ProductMedia/Thumbnails/{guid}.jpg"
```

### Prohibited Values

- Absolute paths to source files
- Original selected file paths
- Image binary data
- User document/downloads/pictures paths as persistent product image references

## Cleanup Contract

Cleanup applies only to app-created thumbnails under the product media folder.

Allowed cleanup moments:

- After successful image replacement save
- After successful image removal save
- After successful product delete

Cleanup failure behavior:

- Log/report if existing project pattern supports it.
- Do not fail product save/delete.
- Do not delete or touch original source images.
