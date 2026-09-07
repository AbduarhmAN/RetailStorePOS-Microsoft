# 1.3.1 Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-20

## Active Technologies

- C# / .NET 9+ + WinUI 3, LiveChartsCore (SkiaSharp), Microsoft.Data.Sqlite (1234-fix-dashboard-charts)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# / .NET 9+

## Code Style

C# / .NET 9+: Follow standard conventions

## Recent Changes

- 1234-fix-dashboard-charts: Added C# / .NET 9+ + WinUI 3, LiveChartsCore (SkiaSharp), Microsoft.Data.Sqlite

<!-- MANUAL ADDITIONS START -->
## Absolute Rules

- **NEVER run `dotnet build`, `dotnet restore`, or any full build/compile command.** This applies universally to ALL projects, subprojects, sandbox apps, and folders without exception. Do not run any build commands under any circumstances. Verify changes through code inspection only.
<!-- MANUAL ADDITIONS END -->
