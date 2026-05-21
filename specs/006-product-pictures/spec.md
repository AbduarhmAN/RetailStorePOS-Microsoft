# Feature Specification: Product Pictures

**Feature Branch**: `[006-product-pictures]`  
**Created**: 2026-04-24  
**Status**: Draft  
**Input**: User description: "Add optimized product image support so images are selected from the Products page, stored efficiently as local files, referenced through SQL by path/identifier, and displayed on checkout product cards without changing the current card sizing algorithm."

## Clarifications

### Session 2026-04-24

- Q: Should the app keep the original selected image, or only keep the optimized thumbnail? → A: Keep thumbnail only; the selected source image is read only to create the thumbnail and is not copied, modified, tracked, or related after save.
- Q: If thumbnail generation fails after the user selected an image, what should happen? → A: Show an image-save error toast/notification, clear the failed image selection as if no image is selected, and let the user save the product without an image without showing an additional no-image warning.
- Q: What should happen to old app-created thumbnail files when an image is replaced, removed, or the product is deleted? → A: Delete old app-created thumbnails best-effort only after the edited product is saved; replacing/removing a product image happens through the existing product edit flow, and original source image locations are forgotten immediately after thumbnail creation.
- Q: How should checkout thumbnails fit inside the product card image section? → A: Resize each thumbnail to the exact width and height of the card image section; larger images must shrink to fit the section, smaller images must stretch up to fill the section, and the card size must not change.
- Q: What should checkout product cards with images show in the bottom section? → A: Show product name and price in the bottom section; long product names must wrap to two lines without shrinking the font, and the image section may shrink inside the same fixed card to make room.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add or Change Product Picture (Priority: P1)

A user maintaining products can select a product picture while adding a new product or editing an existing product, then save the product with the selected picture attached.

**Why this priority**: Product pictures must originate from the Products page because that is where the product catalog is maintained.

**Independent Test**: Can be tested by creating a product with a picture, editing that product to replace the picture, reopening the product, and confirming the selected picture remains associated with the product.

**Acceptance Scenarios**:

1. **Given** the user is adding a new product, **When** they select a valid image and save the product, **Then** the product is saved with a thumbnail reference and an optimized thumbnail is available for checkout display.
2. **Given** the user is editing an existing product, **When** they select a replacement image during edit and save, **Then** the product uses the replacement thumbnail, the previous app-created thumbnail is best-effort deleted, and no original source image location is retained.
3. **Given** the user is editing an existing product with an image, **When** they remove the image and save, **Then** the product returns to the no-image display behavior.

---

### User Story 2 - Show Product Picture During Checkout (Priority: P2)

A cashier can see product pictures on checkout product cards while keeping the current checkout card sizing behavior unchanged.

**Why this priority**: Checkout is the main place where product pictures improve recognition speed, but it must not regress the existing card layout or add-to-cart flow.

**Independent Test**: Can be tested by adding images to some products, opening checkout, and confirming image products render with image/name sections while no-image products still use the previous card layout.

**Acceptance Scenarios**:

1. **Given** a product has an optimized thumbnail, **When** checkout search results show the product card, **Then** the card displays the image filling the top portion and the product name with price in the bottom portion.
2. **Given** a product has no image, **When** checkout search results show the product card, **Then** the card keeps the existing no-image visual behavior.
3. **Given** a product name is long, **When** it appears below the image, **Then** the name wraps to two lines without reducing font size, the image area shrinks inside the same fixed card if needed, and the checkout card sizing algorithm remains unchanged.

---

### User Story 3 - Preserve Product and Checkout Performance (Priority: P3)

The system keeps checkout responsive when many products exist by displaying optimized thumbnails rather than loading full-size images in the checkout grid.

**Why this priority**: Product pictures must not make checkout slow or memory-heavy, especially when the catalog contains around 1000 products.

**Independent Test**: Can be tested by loading a large product catalog with a mix of image and no-image products and confirming checkout remains usable without visible UI freezing.

**Acceptance Scenarios**:

1. **Given** a large source image is selected, **When** the product is saved, **Then** the system creates a display-optimized thumbnail and does not store or maintain a reference to the source image.
2. **Given** checkout displays many products over time, **When** the cashier scrolls or searches, **Then** only lightweight display images are used for card rendering.
3. **Given** thumbnail creation fails, **When** the user attempts to save the selected image, **Then** the system shows an image-save error toast/notification, clears the failed image selection, and leaves the product in a no-image state.

### Edge Cases

- Products with no image must remain valid and keep the existing checkout product display behavior.
- Missing, moved, renamed, or unreadable thumbnail files must not break checkout; affected products must fall back to the no-image display.
- Invalid or unsupported image selections must show a clear product-save error and must not corrupt the product record.
- Failed thumbnail generation must show an image-save error toast/notification, remove the failed thumbnail location from the pending product state, and treat the product as having no selected image.
- Very large source images must be handled without freezing the Products page.
- Replacing an image must not leave the product pointing to an obsolete thumbnail.
- Removing, replacing, or deleting a product image must attempt to delete only app-created thumbnail files; cleanup failure must not block product save/delete.
- Pressing the image remove/cross control while editing a product must only mark the existing image for removal and return the image button to its normal no-image state; the SQL image reference and old app-created thumbnail are removed only if the user saves the product.
- The originally selected image file must not be moved, edited, deleted, copied into app storage, or stored as a persistent product reference.
- Product thumbnails must be resized to fill the exact checkout image section width and height; larger thumbnails must shrink, smaller thumbnails must stretch up, and the card size must not change.
- Existing databases without image fields must migrate safely and treat every existing product as no-image by default.
- Long product names must be readable in the 30% name section without changing the card size calculation.
- Product import/export behavior must remain intact unless image import/export is explicitly added later.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Products page MUST allow selecting an image while adding a product.
- **FR-002**: The Products page MUST allow changing or removing an image while editing a product.
- **FR-002a**: When an existing product image is shown in the edit form, the image control MUST expose a remove/cross affordance that marks the image for removal and visually returns the control to the no-image state without deleting the saved thumbnail until the product is saved.
- **FR-003**: The system MUST store only generated product thumbnail files locally and MUST NOT store full image binaries in the product SQL record.
- **FR-004**: The product record MUST store only a stable thumbnail reference or metadata needed to find the product thumbnail.
- **FR-005**: The system MUST generate an optimized thumbnail when a selected product image is saved.
- **FR-005a**: The system MUST NOT copy, modify, delete, track, or persist a reference to the original selected image after thumbnail generation.
- **FR-006**: The Products page MUST avoid visible UI freezing while processing selected images.
- **FR-007**: Checkout product cards MUST display the optimized thumbnail for products that have one.
- **FR-007a**: Checkout product cards with images MUST resize the thumbnail to the exact width and height of the image section, including shrinking larger thumbnails and stretching smaller thumbnails, without changing the card size.
- **FR-008**: Checkout product cards MUST preserve the existing card sizing algorithm.
- **FR-009**: Checkout product cards with images MUST allocate the top visual area to the image and the bottom visual area to the product name and price.
- **FR-010**: Products without images MUST keep the existing checkout no-image card style and behavior.
- **FR-011**: The product name MUST remain readable when long by wrapping to two lines without reducing font size; if needed, the image area may shrink inside the same fixed card while the checkout card size calculation remains unchanged.
- **FR-012**: Missing or unreadable thumbnail files MUST fall back to the no-image card behavior.
- **FR-013**: Existing products and existing databases MUST remain valid after the feature is added.
- **FR-014**: Product create, edit, delete, search, checkout add-to-cart, import, export, and receipt flows MUST continue to work for products with and without images.
- **FR-015**: Image replacement MUST keep the product reference stable and must not require changing the product identity.
- **FR-016**: If thumbnail generation fails, the system MUST show an image-save error toast/notification, clear any failed thumbnail reference or pending thumbnail location, and allow the user to save the product as a no-image product without displaying an additional no-image warning.
- **FR-017**: When a product image is replaced, removed, or the product is deleted, the system MUST attempt to delete only old app-created thumbnail files and MUST NOT touch any original source image file.
- **FR-018**: Image replacement MUST be available through the existing product edit flow.

### Key Entities *(include if feature involves data)*

- **Product**: Existing catalog item; gains optional image reference data while retaining existing pricing, inventory, tax, barcode, SKU, and unit behavior.
- **Product Image Reference**: Optional product-associated metadata that identifies the optimized thumbnail only.
- **Source Image Selection**: Temporary user-selected image file used only as input for thumbnail generation during product save; its location is not retained after thumbnail generation.
- **Product Thumbnail File**: Optimized local image used by checkout product cards.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can add or replace a product picture during product maintenance without leaving the Products page.
- **SC-002**: Existing products without images continue to appear in checkout with the same no-image card behavior after upgrade.
- **SC-003**: Checkout displays image-backed product cards without changing the number of cards per row produced by the existing sizing behavior.
- **SC-004**: A catalog of 1000 products with thumbnails remains usable for search and scrolling without visible UI freeze during normal checkout use.
- **SC-005**: Invalid, missing, or unreadable image files do not block checkout and do not prevent no-image products from being sold.
- **SC-006**: Product image changes persist across app restart.
- **SC-007**: When thumbnail generation fails, users see a clear image-save error and can still complete product save without an image.

## Assumptions

- Existing source confirmation: the current `Product` model and `products` table do not contain image fields.
- Existing source confirmation: product create/update is coordinated through the Products page and `ProductRepository`.
- Existing source confirmation: checkout search result cards use an `ItemsWrapGrid` whose item width and height are calculated in `CheckoutPage.xaml.cs`; this sizing logic must remain unchanged.
- Existing source confirmation: the app already has `AppDataPaths` for application-controlled local storage under the local app data root, with fallback locations if local app data is unavailable.
- The first version of this feature supports local product pictures only; cloud sync and remote image hosting are out of scope.
- The first version keeps image import/export out of scope unless a later plan explicitly extends CSV behavior.
- Thumbnail generation should prefer a broadly supported display format suitable for product photos.
- Original selected images remain outside app ownership after thumbnail generation.
