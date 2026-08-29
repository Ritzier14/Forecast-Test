# LUNA-22 bundled-data review

Recorded: 2026-08-29

This review inventories tracked JSON, workbook, and release-copy inputs without
printing cell or project values. SHA-256 hashes establish which copies are
duplicates. The normal application project now copies only the explicitly
anonymised startup workbook. The `release/` and `Temp/` copies were legacy
artifacts and are now removed from Git tracking by the separately authorized
F-13 cleanup packet. Local copies are preserved on disk and remain ignored.

| Path | Size | SHA-256 | Role and decision |
|---|---:|---|---|
| `src/ProjectCostForecast.App/Data/data_anonymised.xlsx` | 117,883 | `6809F9F97172359D65AF35428291D2F9F25C8FA29F82538266AAC65797335572` | Required deterministic startup fixture; filename identifies it as anonymised and it is the only data file copied by a normal app build. Retain in source; release-data distribution approval remains a separate gate. |
| `src/ProjectCostForecast.App/Data/SampleData.json` | 245,842 | `DBFBE83930B51624C9EC5DE1706BC9B5A0B31C2AD17E2613FD3071D4D8AB98D2` | Workbook-derived JSON fallback and test fixture. Retain in source for compatibility tests, but do not copy into the normal app package. |
| `src/ProjectCostForecast.App/Data/InitialCostLoad.xlsx` | 30,234 | `AD240B97E7DB7E2CAEAA13A5846149A174908DF55B67882F079CF0E6F3927FF2` | Legacy source/test fixture with no runtime reference found in the app. Not copied by the normal app package; retain as a tracked source input. |
| `release/ProjectCostForecast/Data/data_anonymised.xlsx` | 117,883 | `6809F9F97172359D65AF35428291D2F9F25C8FA29F82538266AAC65797335572` | Duplicate of the approved startup fixture in the legacy release tree. Removed from Git tracking by F-13; any local copy is preserved and ignored. |
| `release/ProjectCostForecast/Data/SampleData.json` | 245,842 | `DBFBE83930B51624C9EC5DE1706BC9B5A0B31C2AD17E2613FD3071D4D8AB98D2` | Duplicate legacy release copy of the non-packaged fallback fixture. Removed from Git tracking by F-13; any local copy is preserved and ignored. |
| `release/ProjectCostForecast/Data/InitialCostLoad.xlsx` | 30,234 | `AD240B97E7DB7E2CAEAA13A5846149A174908DF55B67882F079CF0E6F3927FF2` | Duplicate legacy release copy with no runtime reference. Removed from Git tracking by F-13; any local copy is preserved and ignored. |
| `Temp/data_anonymised.xlsx` | 117,883 | `6809F9F97172359D65AF35428291D2F9F25C8FA29F82538266AAC65797335572` | Duplicate scratch artifact. Removed from Git tracking by F-13; any local copy is preserved and ignored. |
| `source_workbook/1.Mar 26.xlsm` | 135,493 | `A2582FAE62E48FA399EBB9BFF620BD7B5605BEBF6E56ABF4CB532B1E4E9EB41C` | Archived source workbook, not copied by the app project. Retain only as provenance until the release-data owner confirms distribution policy. |
| `project_cost_forecast_wpf_ui_pack/icon_manifest.json` | 8,870 | `2B3600551095FD45EEA79649F77C353FEE416E83D7941AC62934DE5F0EFA990B` | UI asset manifest rather than project data; no runtime package copy. Retain with the UI source pack. |

`.gitignore` excludes future `release/ProjectCostForecast/` and `Temp/`
artifacts, so normal builds do not create new untracked release or scratch
outputs. The F-13 packet removes the exact 16 release files and one `Temp` file
from the Git index while preserving local copies. The CI workflow uploads
generated verification reports as short-retention artifacts rather than
committing build output. See [`F-13-CLEANUP.md`](F-13-CLEANUP.md) for the
authorization, exact scope, recovery path, and verification evidence.
