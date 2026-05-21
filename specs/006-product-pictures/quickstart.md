# Quickstart: Product Pictures

Codex must not compile, run `dotnet build`, or delete files/directories for this repository. Verification below is manual/source-level unless a human chooses to build/run the app.

## Implementation Order

1. Add additive migration in `RetailStorePOS.Data/Class1.cs`.
2. Add `ThumbnailPath` to `RetailStorePOS.Data/Modules/Products/Product.cs`.
3. Update all `ProductRepository` select/insert/update/map paths for `thumbnail_path`.
4. Add `Nexill.RetailStorePOS/Services/ProductImageService.cs`.
5. Extend `ProductEditDraft` and `ProductsPage` with pending image selection/removal state.
6. Add Products page image picker/preview/remove UI.
7. Extend `ProductListItem` and `SearchResultItem` with thumbnail and formatted price data.
8. Update checkout `SearchResultItem` template internals only; do not change card sizing constants or `UpdateSearchResultsGridLayout()`.
9. Keep CSV import/export no-image behavior unchanged unless a later feature explicitly extends it.

## Source Checks

Use these checks after implementation:

```powershell
Select-String -Path "E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Class1.cs" -Pattern "thumbnail_path"
Select-String -Path "E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Products\ProductRepository.cs" -Pattern "thumbnail_path|ThumbnailPath"
Select-String -Path "E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs" -Pattern "SearchResultMinTileWidth|SearchResultTileHeight|SearchResultTileGap|SearchResultMaxColumns|UpdateSearchResultsGridLayout"
```

Expected:

- `thumbnail_path` appears in migration and all product repository read/write paths.
- `CheckoutPage.xaml.cs` sizing constants remain unchanged.
- `UpdateSearchResultsGridLayout()` remains behaviorally unchanged.

## Manual App Verification

If a human runs the app:

1. Open Products page.
2. Add a new product without image and save.
3. Confirm it appears in checkout using the existing no-image card style.
4. Edit the product, select an image, and save.
5. Confirm checkout shows an image-backed card with image, product name, and price.
6. Use a long product name and confirm it wraps to two lines without shrinking font.
7. Confirm the image section shrinks inside the same card when the name needs two lines.
8. Edit the product, press the remove/cross image control, then cancel.
9. Confirm the image is still attached.
10. Edit again, press remove/cross, then save.
11. Confirm checkout returns to the no-image card style.
12. Replace an image and confirm the product keeps the same product identity.
13. Try an invalid/unsupported image and confirm the page shows an image-save error and can still save product data without an image.

## Existing Database Verification

Expected behavior for old databases:

- Migration adds nullable `products.thumbnail_path`.
- Existing products have null image reference.
- No synthetic image or source path is created.
- Checkout still displays all old products using the no-image behavior.

## Performance Verification

For a large catalog:

- Checkout search should still render only lightweight thumbnail references.
- No full original images should be loaded by checkout.
- Products page should not visibly freeze while generating a thumbnail from a large source image.

## Do Not Verify With

Do not use these commands as Codex:

```powershell
dotnet build
dotnet run
msbuild
```
