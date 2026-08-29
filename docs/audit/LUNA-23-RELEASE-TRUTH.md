# LUNA-23 release truth and readiness

Recorded: 2026-08-29
Pre-cleanup baseline: `85f3b03` (`docs(audit): prepare LUNA-24 Sol handoff`)

This is the as-built release boundary for the current candidate. The older
rebuild specification and release notes remain useful history, but this file
is the concise source of truth for what is implemented, what is verified, and
what is still a release limitation.

The F-13 tracked-artifact cleanup is recorded in
[`F-13-CLEANUP.md`](F-13-CLEANUP.md). It removes the exact 17 legacy
release/scratch paths from Git tracking while preserving local copies; the
parent Sol agent owns the cleanup commit and independent SOL-00 review.

## Product and version boundary

| Area | As-built behavior | Evidence |
|---|---|---|
| Application | Windows desktop WPF executable targeting `net8.0-windows` | `src/ProjectCostForecast.App/ProjectCostForecast.App.csproj` |
| Application version | `1.0.1` (`Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion`) | application project file |
| Persistence | Local project JSON files; current persisted format is version 1 | `ProjectDatasetMigrationPipeline.CurrentVersion`, `PersistenceAndCalculationTests` |
| Legacy persistence | Unversioned files are treated as format 0, normalized through the migration pipeline, and saved as format 1; future versions are rejected | `ProjectDatasetMigrationPipeline`, `PersistenceAndCalculationTests` |
| Raw transaction import | `.csv`, `.xlsx`, and `.xlsm`, with bounded preflight, validation, duplicate handling, and cancellation-safe staging | `CsvTransactionService`, `ImportBoundaryTests`, `Luna15ImportExportWorkflowTests` |
| Raw transaction export | CSV export with formula-like text neutralized only at the spreadsheet output boundary | `CsvTransactionService`, `ImportBoundaryTests` |
| Startup data | The normal app package copies only `Data/data_anonymised.xlsx`; it is the deterministic startup fixture | `LUNA-22-BUNDLED-DATA-REVIEW.md`, `Luna22RepositoryHygieneTests` |
| Archived source | `source_workbook/1.Mar 26.xlsm` is provenance, not a normal package input | `LUNA-22-BUNDLED-DATA-REVIEW.md` |
| Direct workbook rebuild | A direct project-data importer for the original `.xlsm` workbook is not claimed; the app uses the extracted/anonymised startup fixture and supports raw transaction workbook import | `docs/RELEASE_NOTES.md` remaining-work list |

The project format version is independent of the application version. A format
change requires a migration, old/current/future fixtures, and a persistence
verification run; changing the application assembly version alone does not
silently rewrite project files.

## Release gates and evidence

The authoritative local command is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -RunLegacySmoke
```

At this packet boundary it passed locked restore, a Release build with zero
warnings and errors, 182 discovered tests, and all 428 retained console-smoke
assertions. The checked-in Windows workflow runs that same command on
`windows-latest` with `contents: read`, then runs the dependency audit, the
redacted source/history secret scan, and the LUNA-20 performance gate.

The supporting gates are:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\audit-dependencies.ps1' -NoRestore -FailOnVulnerability
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\scan-secrets.ps1' -FailOnMatch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify-performance.ps1' -EnforceRegression
```

The dependency inventory contains 22 resolved packages and zero vulnerability
records. The secret scanner reports locations and types only; the final LUNA-22
scan covered 420 tracked files and 56 commits with zero findings. The short
three-sample performance gate compares medians because p95 is the maximum sample
at that size; reports retain p95, and runs with at least 20 samples enforce p95.

## Release-readiness status

| Gate | Status | Truth / follow-up |
|---|---|---|
| Version and format identity | Ready | App is `1.0.1`; project format is v1 with versioned migration from v0 |
| Clean build and discovered tests | Ready | Release verification passes with 182 tests; retained smoke has 428 assertions |
| Locked dependency and vulnerability audit | Ready | 22 packages, zero vulnerability records; license review is in `docs/RELEASE_CHECKLIST.md` |
| Package contents and bundled data | Review required | Normal output is filtered to the anonymised startup workbook; release-data owner approval remains pending |
| Source/history secret scan | Ready | Redacted scan passes with zero findings |
| Backup and restore drill | Ready | Verified restore to a new path is the recommended flow; explicit overwrite creates a verified pre-restore backup |
| Upgrade fixture | Ready | Legacy unversioned and current v1 persistence fixtures are covered; future-version input is rejected |
| Signing and installer/MSIX | Not implemented | No signed installer or MSIX is produced by this repository |
| Deployment rollback | Manual only | Use verified backups and the prior approved release artifact; no installer rollback channel is claimed |
| User acceptance | Pending | Run against additional real projects before a formal production release |
| Tracked release/`Temp` cleanup | Complete | F-13 cleanup removes exactly 16 release files plus one `Temp` file from the Git index with `git rm --cached`; local copies are preserved and ignored |

## Explicit negative constraints

These are deliberate product boundaries, not missing claims:

| Constraint | Current evidence |
|---|---|
| No server, authentication, authorization, or database subsystem | Local WPF composition, JSON persistence, and the dependency inventory; the audit plan marks hosted/database areas not applicable |
| No silent file migration | `ProjectDatasetMigrationPipeline` owns format detection/normalization, rejects unsupported future versions, and has legacy/current/future tests |
| No destructive restore default | `RECOVERY_RUNBOOK.md` recommends a new destination; overwrite is explicit and creates a verified pre-restore backup first |
| No financial-row or personal-value diagnostics by default | `DIAGNOSTICS_RUNBOOK.md`, `DiagnosticsService`, and diagnostics tests require sanitized operation/exception context |
| No contradiction in close-warning behavior | `MainWindow.OnClosing` prompts Save/Discard/Cancel only when dirty and delegates to `ConfirmClose`; cancel or failed save keeps the window open |
| No installer/signing claim | The checklist and release notes mark packaging/signing as remaining work |
| No direct original-workbook project importer claim | The release boundary distinguishes the anonymised startup fixture from supported raw transaction workbook imports |

## Deferred work and ownership

- The release owner must approve the anonymised/source workbook distribution
  policy. The separate F-13 tracked-artifact cleanup has been authorized and
  prepared as its own reviewable change; it does not constitute approval to
  distribute release data.
- The release/build owner must choose and implement the signed installer or
  MSIX channel, including upgrade and rollback behavior, before a formal
  production release.
- The application owner must decide whether a direct original-workbook
  project importer is still required; until then, the extracted startup
  fixture and raw transaction import boundary remain the supported behavior.
- Product acceptance owners must exercise additional real projects and record
  any findings before promotion.

The full command checklist, license metadata, data review, recovery runbook,
and diagnostics runbook are linked from
[`docs/RELEASE_CHECKLIST.md`](../RELEASE_CHECKLIST.md) and the repository
`README.md`.
