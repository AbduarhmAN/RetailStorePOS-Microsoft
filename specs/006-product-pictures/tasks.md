# Tasks: Product Pictures

**Input**: Design documents from `E:\Projects\Retail_Store\V\1.3.3\specs\006-product-pictures`
**Prerequisites**: [plan.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/plan.md), [spec.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/spec.md), [research.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/research.md), [data-model.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/data-model.md), [product-image-ui-contract.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/contracts/product-image-ui-contract.md), [quickstart.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/quickstart.md)

**Tests**: No automated test tasks generated. The specification and plan require manual/source verification only, and this repository forbids Codex from compiling or running build commands.

**Organization**: Tasks are grouped by user story so each story can be implemented and manually verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on incomplete tasks in the same phase.
- **[Story]**: Maps to the user story from [spec.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/spec.md).
- Every task includes exact file paths.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the new media service file without changing behavior yet.

- [x] T001 [P] Create the product media service shell with app-owned thumbnail folder constants and public method placeholders in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Services\ProductImageService.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the shared schema/model/repository support required before any user story can save or display image references.

**Critical**: No user story work should begin until this phase is complete.

- [x] T002 [P] Add additive migration version 18 for nullable `products.thumbnail_path TEXT` using `ColumnExists` guard in `E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Class1.cs`
- [x] T003 [P] Add nullable `ThumbnailPath` property to the product entity in `E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Products\Product.cs`
- [x] T004 Update all `ProductRepository` SELECT, INSERT, UPDATE, and `MapProduct` paths to read/write `thumbnail_path` in `E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Products\ProductRepository.cs`
- [x] T005 Implement thumbnail path resolution, app-folder guarding, async thumbnail generation, and best-effort app-thumbnail cleanup in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Services\ProductImageService.cs`

**Checkpoint**: Product rows can carry a nullable thumbnail reference, old databases default to no-image, and the WinUI app has a service for app-owned thumbnails.

---

## Phase 3: User Story 1 - Add or Change Product Picture (Priority: P1)

**Goal**: A product maintainer can select, preview, replace, remove, and save product pictures from the Products page without storing or modifying the original source image.

**Independent Test**: Create a product with a valid image, edit the product to replace the image, reopen the product, then remove the image and save; confirm the product record keeps only the thumbnail reference or returns to no-image.

### Implementation for User Story 1

- [x] T006 [US1] Extend `ProductEditDraft` with `PendingSourceImagePath`, `PendingRemoveImage`, `PreviewImagePath`, image reset, image load, and image change detection in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T007 [US1] Add product image picker, preview, and remove/cross controls to the add/edit product form in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml`
- [x] T008 [US1] Implement `FileOpenPicker` image selection and remove/cross event handlers that update draft state only, without writing SQL or deleting files, in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T009 [US1] Integrate `ProductImageService` into `SaveNewProduct` so selected images generate thumbnails before product create and source paths are forgotten after save handling in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T010 [US1] Integrate `ProductImageService` into `SaveSelectedProduct` so replacement/removal updates `ThumbnailPath` only after save succeeds and old app-created thumbnail cleanup runs best-effort after save in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T011 [US1] Preserve cancel behavior so pending selected images or pending removals are discarded and the saved thumbnail state is restored in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T012 [US1] Show an image-save error through the existing Products page status/InfoBar path, clear failed pending image state, and allow save as no-image in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T013 [US1] Clean up a product's app-created thumbnail best-effort only after successful product deletion in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml.cs`
- [x] T014 [US1] Surface saved product thumbnail state in the selected product model for Products page preview bindings in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Models\ProductListItem.cs`

**Checkpoint**: User Story 1 is complete when the Products page can add, replace, remove, save, cancel, and delete image references without touching original source images.

---

## Phase 4: User Story 2 - Show Product Picture During Checkout (Priority: P2)

**Goal**: Checkout product cards display product thumbnails while preserving the existing card sizing algorithm and no-image card behavior.

**Independent Test**: Add images to some products, open checkout, and confirm image-backed products show image/name/price cards while no-image products still use the current no-image card style.

### Implementation for User Story 2

- [x] T015 [US2] Add thumbnail reference, resolved thumbnail visibility, no-image visibility, and formatted price text properties to checkout search result items in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Models\SearchResultItem.cs`
- [x] T016 [US2] Update the checkout search result item template to switch between existing no-image layout and image-backed layout in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml`
- [x] T017 [US2] Implement the image-backed checkout card internals with a top image section and bottom product-name-plus-price section in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml`
- [x] T018 [US2] Configure checkout thumbnail rendering to fill the exact image-section width and height, shrinking large thumbnails and stretching small thumbnails inside the fixed card in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml`
- [x] T019 [US2] Configure long product names on image cards to wrap to two lines without reducing font size and let the image section shrink inside the fixed card in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml`
- [x] T020 [US2] Preserve `SearchResultMinTileWidth`, `SearchResultTileHeight`, `SearchResultTileGap`, `SearchResultMaxColumns`, and `UpdateSearchResultsGridLayout()` behavior in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs`
- [x] T021 [US2] Preserve existing checkout search result click, keyboard selection, hover, and selected-border behavior in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs`

**Checkpoint**: User Story 2 is complete when checkout image cards render without changing cards-per-row behavior and no-image products still render as before.

---

## Phase 5: User Story 3 - Preserve Product and Checkout Performance (Priority: P3)

**Goal**: Product image processing and checkout rendering remain responsive with a large catalog by using only lightweight app-created thumbnails.

**Independent Test**: Load a catalog around 1000 products with mixed image/no-image products and confirm checkout search/scroll remains usable and Products page thumbnail generation does not visibly freeze.

### Implementation for User Story 3

- [x] T022 [US3] Ensure thumbnail generation uses bounded output dimensions and JPEG encoding options suitable for checkout display in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Services\ProductImageService.cs`
- [x] T023 [US3] Ensure thumbnail generation work runs asynchronously/background and does not keep original source paths after completion or failure in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Services\ProductImageService.cs`
- [x] T024 [US3] Ensure missing, moved, renamed, or unreadable thumbnail files fall back to no-image checkout card behavior in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Models\SearchResultItem.cs`
- [x] T025 [US3] Ensure product search indexing keeps only product metadata and thumbnail references, not decoded images or source image paths, in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Modules\Products\ProductSearchService.cs`
- [x] T026 [US3] Preserve CSV import/export no-image behavior by leaving image import/export unsupported for this feature in `E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Products\ProductImportService.cs`

**Checkpoint**: User Story 3 is complete when checkout uses only thumbnail references, failed/missing images degrade safely, and source images are never retained.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final source-level checks and documentation alignment.

- [x] T027 [P] Update source-check instructions if implementation changes the verification details in `E:\Projects\Retail_Store\V\1.3.3\specs\006-product-pictures\quickstart.md`
- [x] T028 [P] Confirm implementation still matches the product image UI contract in `E:\Projects\Retail_Store\V\1.3.3\specs\006-product-pictures\contracts\product-image-ui-contract.md`
- [x] T029 Confirm `thumbnail_path` appears in the migration and all repository read/write paths using the source checks documented in `E:\Projects\Retail_Store\V\1.3.3\specs\006-product-pictures\quickstart.md`
- [x] T030 Confirm checkout sizing constants and `UpdateSearchResultsGridLayout()` remain behaviorally unchanged using the source checks documented in `E:\Projects\Retail_Store\V\1.3.3\specs\006-product-pictures\quickstart.md`
- [x] T031 Confirm Products page XAML names and code-behind handlers remain synchronized after image UI edits in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml`
- [x] T032 Confirm Checkout page XAML names and code-behind handlers remain synchronized after card template edits in `E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational.
- **User Story 2 (Phase 4)**: Depends on Foundational; can start independently after the repository exposes thumbnail data, but manual verification is stronger after US1 creates image-backed products.
- **User Story 3 (Phase 5)**: Depends on Foundational; can run alongside US1/US2 where file ownership does not conflict.
- **Polish (Phase 6)**: Depends on desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: MVP; no dependency on US2/US3 after Foundational.
- **US2 (P2)**: No dependency on US1 code, but needs products with thumbnails for full manual verification.
- **US3 (P3)**: Depends on thumbnail service and checkout model shape from Foundational/US2.

### Parallel Opportunities

- T001, T002, and T003 can run in parallel.
- After T003 and T004, Products page UI work and checkout model/template work can be split by file owner.
- US1 `ProductsPage.xaml` work and US2 `SearchResultItem.cs` work can proceed in parallel after Foundational.
- Polish tasks T027 and T028 can run in parallel.

---

## Parallel Example: User Story 1

```text
Task: "Add product image picker, preview, and remove/cross controls to the add/edit product form in E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\ProductsPage.xaml"
Task: "Surface saved product thumbnail state in the selected product model for Products page preview bindings in E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Models\ProductListItem.cs"
```

## Parallel Example: User Story 2

```text
Task: "Add thumbnail reference, resolved thumbnail visibility, no-image visibility, and formatted price text properties to checkout search result items in E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Models\SearchResultItem.cs"
Task: "Preserve SearchResultMinTileWidth, SearchResultTileHeight, SearchResultTileGap, SearchResultMaxColumns, and UpdateSearchResultsGridLayout() behavior in E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs"
```

## Parallel Example: User Story 3

```text
Task: "Ensure product search indexing keeps only product metadata and thumbnail references, not decoded images or source image paths, in E:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Modules\Products\ProductSearchService.cs"
Task: "Preserve CSV import/export no-image behavior by leaving image import/export unsupported for this feature in E:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Products\ProductImportService.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup.
2. Complete Phase 2 foundational schema/model/repository/media service.
3. Complete Phase 3 US1 Products page add/edit/remove flow.
4. Stop and manually validate US1 using [quickstart.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/quickstart.md).

### Incremental Delivery

1. Deliver US1 so product thumbnails can be created and maintained.
2. Deliver US2 so checkout displays thumbnails without changing card sizing.
3. Deliver US3 so large image handling, missing files, and large catalogs remain safe.
4. Run Phase 6 source-level checks.

### Manual Verification Only

Do not run `dotnet build`, `dotnet run`, `msbuild`, or equivalent compile/build commands as Codex. Use source inspection and the manual checklist in [quickstart.md](E:/Projects/Retail_Store/V/1.3.3/specs/006-product-pictures/quickstart.md).
