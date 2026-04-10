# UI Redesign Specification: Notion-Style C# Application

Please redesign the UI of this C# application to strictly follow a "Notion-like" design aesthetic. 

The application uses WPF / WinUI (XAML) for its interface. Your task is to update the application's Resource Dictionaries (Themes/Styles) and structural layout to achieve an ultra-minimalist, productivity-focused interface.

## 1. Core Visual Directives
- **Zero Clutter**: Remove unnecessary borders, drop shadows, gradients, and background colors. The interface must feel like a blank sheet of paper.
- **Backgrounds**:
  - Main content areas must be pure white (`#FFFFFF`) or extremely light gray (`#FCFCFC`).
  - Sidebars and secondary panes should use a very subtle off-white (`#F7F7F5`).
- **Typography is the UI**: 
  - Do not use system defaults. The primary font stack should be `Inter`, `Segoe UI Variable`, or a clean sans-serif. 
  - Headings must use high contrast (bold/heavy weights) with tight letter spacing.
  - Paragraph and meta-text should be subdued and highly legible.

## 2. Design Tokens to Implement (XAML Resources)

### Colors (Light Mode)
- **Primary Text**: `#37352F` (do not use pure `#000000`, it's too harsh).
- **Muted Text / Icons**: `#787774` (used for secondary labels, placeholders, and unselected sidebar items).
- **Background (Main)**: `#FFFFFF`.
- **Background (Sidebar)**: `#F7F7F5`.
- **Hover States**: `#EFEFED` (should apply instantly to list items, buttons, and navigation without long fade animations).
- **Border/Dividers**: `#EDEDEB` (1px solid, used sparingly).
- **Accent Tags**: Soft backgrounds with slightly darker text (e.g., Purple tag: `Background="#F4F0FA" Foreground="#9A6DD7"`).

### Typography (Define in standard TextBlocks/Styles)
- **Page Title**: `FontWeight="Bold" FontSize="36" Foreground="{StaticResource PrimaryText}" Margin="0,0,0,24"`
- **Section Headers**: `FontWeight="SemiBold" FontSize="20" Foreground="{StaticResource PrimaryText}" Margin="0,16,0,8"`
- **Body Text**: `FontSize="15" Foreground="{StaticResource PrimaryText}" LineHeight="24"`
- **Meta / Labels**: `FontSize="13" Foreground="{StaticResource MutedText}"`

## 3. Layout & Structure Requirements

1. **The Navigation Sidebar**:
   - Must be a flat list of items on the left side.
   - No heavy bounding boxes for list items. The selection state should just be a light gray background (`#E8E8E6`) with `CornerRadius="4"`.
   - Items should have subtle padding (`8px` vertical, `12px` horizontal) and use muted text icons.

2. **The Top Bar (Breadcrumbs & Tools)**:
   - Should be minimal. Use a simple text breadcrumb (e.g., `Retail-Store / Nexill POS — Reports & Insights`).
   - Action buttons (Share, Export, Settings) should be small, flat buttons with just an icon or very subtle text, only revealing a background on hover.

3. **The Main Editor Area**:
   - Must have generous margins. Content should not touch the edges of the window. Left/Right padding should be at least `48px` to `96px` depending on window size.
   - Data presentation (like "Properties") should look like a simple grid or a list: `Label (Muted)` aligned left, `Value (Dark)` aligned right, with no visible gridlines.

## 4. Interaction & Micro-Animations
- Remove long easing animations. Interactions should feel immediate and crisp. 
- Button/List item hovers should change the background color instantly (`0ms - 50ms` duration). 
- Avoid using `.DropShadowEffect`. If depth is absolutely required for a floating menu or tooltip, use a very large, soft, transparent shadow (e.g., `BlurRadius="24" Opacity="0.1"`).

## Instructions for the AI / Developer
1. Review the existing [.xaml](file:///e:/Projects/Retail_Store/app/RetailStorePOS.App/App.xaml) resource files (like [Theme.xaml](file:///e:/Projects/Retail_Store/app/RetailStorePOS.App/Resources/Styles/Theme.xaml), `Styles.xaml`, or [App.xaml](file:///e:/Projects/Retail_Store/app/RetailStorePOS.App/App.xaml)).
2. Rip out any heavy styling (gradients, thick borders, primary-colored backgrounds).
3. Implement the Notion Design Tokens listed above as `SolidColorBrush` resources.
4. Update the primary `Window` or `Page` layout to feature the two-pane structure (subtle sidebar left, pure white content right).
5. Modify `Button`, `TextBox`, and `ListBoxItem` default styles to use flat, borderless designs that only show hover backgrounds.
