# RetailStorePOS — UI Style Guide (2.0.0)

This is the visual language applied to: Receipts, Reports Dashboard, Advanced Reports
(Pages 1–3), Store Management, Tax Configuration, Users, My Preferences, Products,
CheckoutPage, Open/Close Register dialogs, and Discount dialog.

It is **not** applied to: LoginPage (intentional brand layout, left untouched).

---

## 1. Color palette

### Surface
| Token       | Hex       | Use |
|-------------|-----------|-----|
| Page bg     | `#F9FAFB` | Behind the cards (CheckoutPage). Other pages use Transparent + theme bg. |
| Card bg     | `White`   | All section cards |
| Soft fill   | `#FAFBFC` | Table header rows, search box backgrounds |
| Subtle fill | `#FAFBFC` / `#F1F5F9` | Inline banners, neutral chips |

### Borders
| Token        | Hex       | Use |
|--------------|-----------|-----|
| Card border  | `#E8EAF0` | Standard 1px border on cards, search boxes, table headers |
| Row divider  | `#F1F4F9` | Bottom border on table rows |
| Input border | `#E8EAF0` | Default for most inputs |

### Text
| Token              | Hex       | Use |
|--------------------|-----------|-----|
| Primary text       | `#1A1D23` | Section titles, headlines, table cell values |
| Body text          | `#1A1D23` | Form input text, KPI values |
| Muted text         | `#5F6775` | Field labels (uppercase tracking style) |
| Secondary / helper | `#8F96A3` | Subtitles, helper text, table count text |
| Soft hint          | `#454443` | Less-used variant for emphasized labels |

### Accent (blue)
| Token            | Hex       | Use |
|------------------|-----------|-----|
| Accent primary   | `#3B82F6` | Section icons (when used), primary highlights |
| Accent strong    | `#1D4ED8` | Status banner text, qty pill text |
| Accent bg light  | `#EFF6FF` | Status banner bg, qty pill bg |
| Accent border    | `#BFDBFE` / `#DBEAFE` | Status banner border |

### Semantic
| State    | Bg        | Border    | Text      |
|----------|-----------|-----------|-----------|
| Success  | `#ECFDF5` | `#A7F3D0` | `#065F46` (Change Due chip, receipt confirmation) |
| Warning  | `#FFFBEB` | `#FDE68A` | `#92400E` / `#78350F` (read-only, unlock-reason) |
| Danger   | `#FEF2F2` | `#FCA5A5` | `#991B1B` / `#DC2626` / `#7F1D1D` (delete buttons, danger zones) |
| Info     | `#EFF6FF` | `#BFDBFE` | `#1D4ED8` (status messages) |

### POS-only colors (kept on CheckoutPage)
| Use | Color |
|-----|-------|
| Dark footer bg | `#1E293B` (slate) |
| Cart card accent stripe | `#3B82F6` |
| TENDER input bg | `#F1F5F9` (slate-50) |
| TENDER input border | `#CBD5E1` (slate-300) |
| Quick cash +1 | `#E0E7FF` bg / `#3730A3` text (indigo) |
| Quick cash +2 | `#F5F3FF` bg / `#5B21B6` text (purple) |
| Quick cash +3 | `#ECFDF5` bg / `#065F46` text (green) |

---

## 2. Cards

The standard section card across all settings pages:

```xml
<Style x:Key="..._Card" TargetType="Border">
    <Setter Property="Background"     Value="White"/>
    <Setter Property="BorderBrush"    Value="#E8EAF0"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius"   Value="12"/>
    <Setter Property="Padding"        Value="22"/>
</Style>
```

### Variants
- **Danger card** (Reset/destructive): `#FFF5F5` bg, `#FECACA` border, same corner/padding.
- **Card with table** (e.g. Tax Authorities): `Padding="0"` so the inner header row + table can extend edge-to-edge.

### Spacing between cards
`StackPanel Spacing="16"` is the standard vertical gap.

---

## 3. Typography

| Token             | Size | Weight   | Color     | Use |
|-------------------|------|----------|-----------|-----|
| Page title        | 22   | SemiBold | `#1A1D23` | Top-of-page title |
| Page subtitle     | 13   | normal   | `#8F96A3` | Top-of-page subtitle, wraps |
| Section title     | 15   | SemiBold | `#1A1D23` | Card section header |
| Section subtitle  | 12   | normal   | `#8F96A3` | Card section subhead, wraps |
| Field label       | 11   | SemiBold | `#5F6775` | Above inputs, with `CharacterSpacing="40"` for uppercase tracking feel |
| Helper text       | 11   | normal   | `#8F96A3` | Below inputs, wraps |
| Table header      | 11   | SemiBold | `#5F6775` | Sortable column headers, uppercase tracking |
| Table cell        | 13   | normal   | `#1A1D23` | Body cells |
| Status banner     | 13   | normal   | semantic  | Inline status banner text |
| KPI value         | 28   | SemiBold | `#1A1D23` | Big numbers on dashboards |
| KPI label         | 11   | SemiBold | `#5F6775` | Above KPI value, with `CharacterSpacing="40"` |

Field labels and table headers use:
```xml
<Setter Property="CharacterSpacing" Value="40"/>
```
This gives them a subtle uppercase-tracking effect without actually uppercasing the text — improves the "this is metadata" feel.

---

## 4. Form inputs

### TextBox / PasswordBox / ComboBox / DatePicker / NumberBox
| Property      | Value |
|---------------|-------|
| CornerRadius  | `6`   |
| MinHeight     | `34`–`36` |
| FontSize      | default (15 in larger contexts) |

For multi-line text (`AcceptsReturn="True"`):
| Property      | Value |
|---------------|-------|
| MinHeight     | `80`+ |
| TextWrapping  | `Wrap` |
| CornerRadius  | `6`   |

### ComboBox sizing
- `HorizontalAlignment="Left"` — sized to content, NOT stretched
- `MinWidth="220"`–`260"` so they don't look tiny but don't stretch to column width

### Field grid layout
Two-column form rows use `ColumnSpacing="14"–"20"`, `RowSpacing="14"`, with each cell as:
```xml
<StackPanel Spacing="6">
    <TextBlock Style="..._FieldLabel" Text="..." />
    <TextBox  Style="..._TextBox"   .../>
</StackPanel>
```

---

## 5. Buttons

### Primary (Save / Confirm / Add)
```xml
<Style x:Key="..._PrimaryButton" TargetType="Button"
       BasedOn="{StaticResource AccentButtonStyle}">
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Padding"      Value="14,7"/>
    <Setter Property="MinHeight"    Value="34"/>
    <Setter Property="FontSize"     Value="13"/>
    <Setter Property="FontWeight"   Value="SemiBold"/>
</Style>
```

For top-action-bar Save buttons (e.g., Store Management): `CornerRadius=8`,
`Padding=16,8`, `MinHeight=36`.

### Ghost (Cancel / Refresh / Import / Export)
```xml
<Setter Property="Background"     Value="White"/>
<Setter Property="BorderBrush"    Value="#E8EAF0"/>
<Setter Property="BorderThickness" Value="1"/>
<Setter Property="Foreground"     Value="#1A1D23"/>
<Setter Property="CornerRadius"   Value="6"/>
<Setter Property="Padding"        Value="14,7"/>
<Setter Property="MinHeight"      Value="34"/>
<Setter Property="FontSize"       Value="13"/>
```

### Danger (Delete / Deactivate)
```xml
<Setter Property="Background"     Value="White"/>
<Setter Property="BorderBrush"    Value="#FCA5A5"/>
<Setter Property="BorderThickness" Value="1"/>
<Setter Property="Foreground"     Value="#DC2626"/>
<Setter Property="CornerRadius"   Value="6"/>
<Setter Property="FontWeight"     Value="SemiBold"/>
```

### Row icon button (Edit / Delete in tables)
```xml
<Setter Property="Background"     Value="Transparent"/>
<Setter Property="BorderBrush"    Value="Transparent"/>
<Setter Property="Padding"        Value="6"/>
<Setter Property="MinWidth"       Value="32"/>
<Setter Property="MinHeight"      Value="32"/>
<Setter Property="CornerRadius"   Value="6"/>
```

Edit icon glyph `&#xE70F;` in `#5F6775`. Delete icon glyph `&#xE74D;` in `#DC2626`.

---

## 6. Status banner

The generic status banner that appears below editor cards (used by Store Management,
Tax, My Preferences):

```xml
<Border Background="#EFF6FF" BorderBrush="#BFDBFE"
        BorderThickness="1" CornerRadius="8" Padding="12,10"
        Visibility="{x:Bind ViewModel.StatusVisibility, Mode=OneWay}">
    <TextBlock Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
               Foreground="#1D4ED8" FontSize="13" TextWrapping="Wrap"/>
</Border>
```

For error states (Users page): swap to red palette
(`#FEF2F2` bg, `#FCA5A5` border, `#991B1B` text).

The `StatusVisibility` property was added to `SettingsViewModel` to drive
`Visible/Collapsed` based on whether `StatusMessage` has content.

---

## 7. Tables (master list inside a card)

For Tax Authorities / Rules / Groups and Receipts list — the structure is:

1. **Card header** (padding 22,20,22,16) with title, subtitle, and an Add button
   on the right.
2. **Table header row** with `Background="#FAFBFC"`, `BorderThickness="0,1,0,1"`,
   `BorderBrush="#E8EAF0"`. Column titles use `_TableHeaderText` style.
3. **ListView** body. Critically:
   ```xml
   <ListView.ItemContainerStyle>
       <Style TargetType="ListViewItem">
           <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
           <Setter Property="Padding"   Value="0"/>
           <Setter Property="MinHeight" Value="0"/>
           <Setter Property="Margin"    Value="0"/>
       </Style>
   </ListView.ItemContainerStyle>
   ```
   This is **required** for header columns to align with body columns. Without it,
   `ListViewItem` adds its own padding and rows shift right.

4. **Row template** with same column definitions as the header, `Padding="22,10"`,
   `BorderBrush="#F1F4F9" BorderThickness="0,0,0,1"` for soft row dividers.

### Column alignment rules
- **Text fields** (name, code, description): `HorizontalAlignment="Left"`
- **Currency / numbers**: `HorizontalAlignment="Right"` (financial convention)
- **Action buttons**: `HorizontalAlignment="Right"` or `Center`

---

## 8. Empty states

Centered title + subtitle, max-width ≈320–380px. **No** boxy bordered containers,
**no** decorative icons:

```xml
<StackPanel HorizontalAlignment="Center" Spacing="6"
            Padding="0,32,0,0" MaxWidth="380">
    <TextBlock Text="..." FontSize="15" FontWeight="SemiBold"
               Foreground="#1A1D23" HorizontalAlignment="Center"/>
    <TextBlock Text="..." FontSize="12" Foreground="#8F96A3"
               TextWrapping="Wrap" TextAlignment="Center"/>
</StackPanel>
```

---

## 9. Patterns

### Standard settings-page layout
1. **Top action bar** (Row 0): page title + subtitle on the left, Save button on the right.
   Margin `0,0,0,20` below before the scroll viewer.
2. **ScrollViewer** (Row 1) wrapping a `StackPanel Spacing="16"` of cards.
3. **Status banner** appears between content and danger zone.
4. **Danger zone** (where applicable, e.g. Reset on Store Management) styled with
   `_DangerCard`.

### Master-detail (Users, Products)
- Two-column grid, ratios vary (Users uses 0.5 / 1.5; Products uses 1.25 / 0.95)
- Each column is a card with internal header + list
- Editor pane on the right has its own header bar with action buttons

### Render-first / skeleton-first (Advanced Reports)
- Page paints from snapshot immediately
- Live data loads via `DispatcherQueue.TryEnqueue` after Loaded
- Shimmer skeletons (`SkeletonShimmerBrush` linear gradient) bridge the gap

---

## 10. What I deliberately **don't** use

- **Icon badges on every section header** — used once or twice early on, then
  pulled back. Cards with section icon badges (Store Management, Tax) were too
  noisy. Now: typography hierarchy only.
- **Theme resource brushes** for new code — those use Fluent's mutable theme.
  This system uses explicit hex so the design renders the same in light/dark and
  doesn't drift if Microsoft changes Fluent.
- **Heavy shadows on rows** — one heavy shadow per cart line item piles up.
  Cart cards use `Translation="0,0,4"` for a hint of depth, no more.
- **`CommandBar` + `AppBarButton`** — replaced with plain `Button` rows. Less
  mystery meat, easier to style consistently.
- **Tiny corner radius (3–5)** — looks dated. Standard is `6` for inputs,
  `8` for banners and buttons-in-dialogs, `10–12` for cards, `14` for dialogs.

---

## 11. Reference: which page uses what

| Page | Style prefix | Notes |
|------|--------------|-------|
| Store Management | `SM_*` | 5 sections, has a Danger Zone for Reset |
| Tax Configuration | `TX_*` | 3 of the 5 sections are tables (Authorities/Rules/Groups) |
| Users | `UP_*` | Master-detail; PIN/Admin/Roles editor on the right |
| My Preferences | `MP_*` | Auto-save (no Save button) |
| Products | `PP_*` | Implicit input styles + ghost/primary/danger button styles |
| Reports Dashboard | inline | Uses 4 KPI cards + main content row + side widgets |
| Receipts | inline | Card-wrapped table |
| Advanced Reports | inline (each page) | Snapshot-first + shimmer pattern |
| Operations | inline | Hour×DOW heatmap built dynamically in code |
| CheckoutPage | inline (page resources) | POS-specific palette; dark footer |
| Discount/Open/Close dialogs | inline | Match settings pages where it makes sense |
