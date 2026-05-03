# Research: Multi-Language Support (i18n)

## R-001: WinUI 3 Resource Loading Mechanism

**Decision**: Use MRT Core via `Microsoft.Windows.ApplicationModel.Resources.ResourceLoader`

**Rationale**:
- Built into Windows App SDK — no additional NuGet package needed
- `.resw` files compile to `.pri` binary (fastest possible lookup)
- `x:Uid` in XAML provides zero-code binding for static strings
- Automatic fallback chain: requested locale → default locale → raw key
- `ApplicationLanguages.PrimaryLanguageOverride` is the official API for runtime language switching

**Alternatives considered**:
- Custom JSON/YAML i18n: Rejected — would need custom loader, no `x:Uid` support, no PRI binary optimization
- Community toolkit LocalizationService: Rejected — immature for WinUI 3, adds dependency
- ViewModel binding approach: Rejected — massive boilerplate (one property per string), unnecessary complexity

## R-002: RTL (Right-to-Left) Layout in WinUI 3

**Decision**: Set `FlowDirection = FlowDirection.RightToLeft` on `MainWindow.Content` root at startup for Arabic

**Rationale**:
- WinUI 3 inherits `FlowDirection` down the visual tree — setting it once at the root flips everything
- Grid columns, StackPanel orientations, text alignment all mirror automatically
- No per-page code needed
- Custom Canvas-drawn elements (donut chart) may need manual adjustment

**Implementation detail**:
```csharp
// In MainWindow constructor or Loaded, after checking language
if (ApplicationLanguages.PrimaryLanguageOverride == "ar")
{
    Content.FlowDirection = FlowDirection.RightToLeft;
}
```

**Alternatives considered**:
- Per-page FlowDirection: Rejected — error-prone, requires touching every page
- CSS-like direction attribute: Not applicable to WinUI 3

## R-003: Language Persistence Strategy

**Decision**: Store `app_language` key in existing SQLite `settings` table via `SettingsRepository`

**Rationale**:
- `SettingsRepository` already provides `GetSetting(key, default)` / `SetSetting(key, value)` pattern
- Settings table is guaranteed to exist (created by migration bootstrap)
- Consistent with how `currency_code`, `region_code`, `store_name` are stored
- Read at app startup in `App.xaml.cs` before `InitializeComponent()`

**Key concern**: `App.xaml.cs` constructor runs before `LoginRuntime.Initialize()`. Need to create a minimal `SqliteConnectionFactory` + `SettingsRepository` directly in `App()` to read the language setting early enough.

**Alternatives considered**:
- Windows.Storage.ApplicationData.LocalSettings: Rejected — separate from SQLite, creates dual-store complexity
- Environment variable: Rejected — not user-friendly, requires OS-level config

## R-004: x:Uid Naming Convention

**Decision**: `{PagePrefix}_{ElementPurpose}` format with `.{Property}` suffix in `.resw`

**Rationale**:
- Avoids key collisions across 21+ files
- Self-documenting: `Login_WelcomeBack` clearly belongs to LoginPage
- Property suffix in .resw (e.g., `Login_WelcomeBack.Text`) keeps the XAML clean — only `x:Uid` appears on the element
- Consistent with Microsoft documentation patterns

**Convention table**:
| Page | Prefix | Example key |
|------|--------|-------------|
| LoginPage | `Login_` | `Login_WelcomeBack` |
| CheckoutPage | `Checkout_` | `Checkout_CompleteSale` |
| ProductsPage | `Products_` | `Products_SearchPlaceholder` |
| ReportsDashboardPage | `Dashboard_` | `Dashboard_SalesToday` |
| ReportsReceiptsPage | `Receipts_` | `Receipts_ReceiptNumber` |
| UsersPage | `Users_` | `Users_AddUser` |
| SettingsPage | `Settings_` | `Settings_PageTitle` |
| StoreManagementPage | `StoreMgmt_` | `StoreMgmt_StoreName` |
| TaxConfigurationPage | `Tax_` | `Tax_Categories` |
| MyPreferencesPage | `Prefs_` | `Prefs_Theme` |
| MainWindow | `Shell_` | `Shell_BrandTitle` |
| CloseRegisterDialog | `CloseReg_` | `CloseReg_Cash` |
| OpenRegisterDialog | `OpenReg_` | `OpenReg_Title` |
| CashInOutDialog | `CashIO_` | `CashIO_Amount` |
| TimeSyncDialog | `TimeSync_` | `TimeSync_Title` |
| CheckoutCartControl | `Cart_` | `Cart_EmptyState` |
| CheckoutPaymentControl | `Payment_` | `Payment_Subtotal` |
| CheckoutProductsControl | `ProdSearch_` | `ProdSearch_Placeholder` |
| AboutPage | `About_` | `About_Version` |
| ReportsPage | `Reports_` | `Reports_Dashboard` |

## R-005: Receipt Language Independence

**Decision**: Receipts use `store_language` setting (separate from `app_language`), defaulting to English. Numbers always Western digits.

**Rationale**:
- A French-speaking cashier may serve English-speaking customers
- Receipt language should match the store's customer base, not the operator's UI preference
- Western digits (0-9) are universally readable, even in Arabic-speaking regions

**Implementation detail**:
- `SettingsRepository.GetStoreLanguage()` → returns BCP 47 tag
- Receipt helper creates a dedicated `ResourceLoader` with the store language for receipt template strings
- Number formatting always uses `CultureInfo.InvariantCulture` for Western digits
