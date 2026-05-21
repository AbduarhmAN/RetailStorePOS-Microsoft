# Data Model: Multi-Language Support (i18n)

## Entities

### Settings Table (existing — no schema change)

The language preferences are stored as key-value pairs in the existing `settings` table:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `app_language` | string (BCP 47) | `"en"` | UI display language. Drives `PrimaryLanguageOverride` and `FlowDirection`. |
| `store_language` | string (BCP 47) | `"en"` | Receipt/report language. Independent of UI language. |

**No DDL changes required.** The `settings` table already supports arbitrary key-value pairs via `SetSetting(key, value)`.

### Supported Languages (compile-time constant)

| BCP 47 Tag | Display Name (Native) | Display Name (English) | Direction |
|------------|----------------------|----------------------|-----------|
| `en` | English | English | LTR |
| `ar` | العربية | Arabic | RTL |
| `fr` | Français | French | LTR |

These are defined as a static list in the UI layer (not database-driven).

### Resource Files (file system)

| Path | Purpose | Content at ship |
|------|---------|-----------------|
| `Strings/en/Resources.resw` | English strings | Fully populated (~300-400 keys) |
| `Strings/ar/Resources.resw` | Arabic skeleton | All keys present, values empty (user fills) |
| `Strings/fr/Resources.resw` | French skeleton | All keys present, values empty (user fills) |

## State Transitions

```
App Launch
    │
    ▼
Read app_language from settings (default: "en")
    │
    ▼
Set PrimaryLanguageOverride (before InitializeComponent)
    │
    ▼
MainWindow loads → set FlowDirection based on language
    │
    ▼
All pages load → x:Uid resolves from active locale .resw
    │
    ▼
User changes language in Store Management
    │
    ▼
Save new app_language to settings → Show "restart required" message
    │
    ▼
App restart → cycle repeats with new language
```

## Validation Rules

- `app_language` MUST be one of: `en`, `ar`, `fr`
- `store_language` MUST be one of: `en`, `ar`, `fr`
- If stored value is invalid or empty, default to `en`
- `FlowDirection.RightToLeft` MUST be set if and only if active language is `ar`
