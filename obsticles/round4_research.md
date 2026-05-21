# Global Intelligence Audit: Round 4 (Index Corruption)
**Topic**: PRI175 "Duplicate Entry" & Dot-Indexed Key Regressions
**Date**: 2026-05-07

## 1. German Perspective (Microsoft Community DE / Bug Reports)
*   **Key Insight**: The `PRI175` error is often a "Phantom Error" caused by stale `obj` folders.
*   **Discovery**: German developers identified that `MakePri.exe` (the indexer) doesn't always perform a deep clean. If you rename a key in `.resw`, the OLD key might stay in the cached PRI, causing a "Duplicate Entry" error with itself.
*   **Actionable**: Mandatory `dotnet clean` or manual deletion of `bin`/`obj` folders before any language change.

## 2. English Perspective (GitHub Issues / WindowsAppSDK Repo)
*   **Key Insight**: Regression in SDK 1.8 regarding `x:Uid` with dot notation.
*   **Discovery**: Keys like `MyPage.Header` are handled differently in SDK 1.8 than in 1.7. In packaged builds, the indexer sometimes flattens these in a way that causes `ResourceLoader` to fail if the path conversion (dot-to-slash) isn't perfect.
*   **Actionable**: The `LocalizationHelper` must implement a "Tiered Probing" strategy (try dot, then try slash, then try direct).

## 3. Russian Perspective (Software Testing Community)
*   **Key Insight**: Resource Indexer (PRI) Versioning.
*   **Discovery**: If multiple `.resw` files exist across different project references (e.g., `Data` project and `UI` project), they MUST have unique names. If both have `Resources.resw`, the indexer crashes with `PRI175`.
*   **Actionable**: Rename the `Data` project's resources to a unique name (e.g., `DataStrings.resw`).

## 4. Hebrew Perspective (Architecture Review)
*   **Key Insight**: BCP-47 Tag Normalization.
*   **Discovery**: The indexer treats `ar-SA` and `ar-sa` as different entries in some case-sensitive stages of the build, leading to "Key Not Found" errors even if the file exists.
*   **Actionable**: Enforce strict casing in all XML and folder references.

## 5. Synthesis of Round 4
The "App still failing" issue reported by the user after the first fix was due to **PRI Index Stale Cache**. The build system was trying to use an old index that contained conflicting keys from previous iterations.

---
**Status**: Round 4 Completed.
**Next Action**: Final encoding and synthesis (Round 5).
