# Project Cost Forecast - Release Candidate

This is the desktop rebuild of `1.Mar 26.xlsm` as a C#/.NET 8 WPF program.

The current build focuses on the highest-value spreadsheet workflow and the release foundations needed to move beyond the original Alpha 1 package:

- CTC forecast grid as a desktop table.
- Raw cost transactions imported from the workbook seed data.
- Category report and contingency views.
- A resource drilldown panel: click a resource/forecast line and see every raw cost transaction that has come into that resource.
- Recalculation of CTD, current month actuals, CTC, FCC, and variances from source objects.
- Workbook-style FY report totals for spent to date, cost to complete, planned cost, AP/LTP budget, and variance.
- Actuals pivot view grouped by task, resource, and FY-period.
- Editable monthly forecasts with audit entries.
- Open/save project JSON files with backups.
- Verify and restore project backups to a new file, with a pre-restore backup for explicit overwrites.
- Sanitized, bounded local diagnostics with corrupt-preference quarantine and startup notices.
- Explicit NZ business-date and UTC durable-instant handling with deterministic clock injection.
- Hardened raw-transaction CSV/XLSX/XLSM import with bounded file/workbook limits, malformed-input rejection, and duplicate-row skipping.
- Spreadsheet-safe CSV export that neutralizes formula-like text without changing project data.
- Validation and audit tabs.
- Add, duplicate, and delete forecast lines.
- Command-line acceptance checks for the workbook-derived seed data.
- A named discovered xUnit suite mapping all 428 legacy harness assertions, including deterministic STA-hosted WPF checks.
- A P6-style Schedule tab with a CPM Gantt chart: activities, milestones, headings
  with sub-grouping, hammock tasks, FS/SS/FF/SF links with lag, early/late dates,
  total float and critical path, constraint dates, multiple calendars with holidays,
  named editable baselines, and working-day slip tracking against the active baseline.

## How to open

1. Install Visual Studio 2022 or later with `.NET desktop development` workload.
2. Open `ProjectCostForecast.sln`.
3. Set `ProjectCostForecast.App` as startup project.
4. Run with `F5`.

## Architecture

The codebase is being migrated toward a modular WPF architecture with explicit application, domain, and storage seams. The current boundaries, state/source-of-truth contract, rules for new code, and phased assembly split are documented in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/STATE_MODEL.md`](docs/STATE_MODEL.md).

## How to verify

The repository verification entry point restores packages, builds the solution in
Release configuration, and runs the authoritative discovered xUnit suite. From the
repository root, run:

```powershell
.\scripts\verify.ps1
```

The first run needs a .NET 8 SDK and access to the configured NuGet source. After a
successful restore, use `.\scripts\verify.ps1 -NoRestore` for the fast local path.
The script can also be invoked by its full path from another working directory.
The script exits non-zero if restore, build, or a discovered test fails. Build
outputs remain under the existing ignored `bin/`, `obj/`, and `artifacts/` paths.
The tests live in `tests\ProjectCostForecast.UnitTests`; their mapping to the
original executable harness is documented in `docs/TEST_COVERAGE_MAP.md`.

The original console harness is retained as a distinct opt-in compatibility smoke
check. Run `.\scripts\verify.ps1 -NoRestore -RunLegacySmoke` when that comparison is
specifically useful; it is not the authoritative automated-test gate.

Restore is lock-file based: the normal verification path uses
`dotnet restore --locked-mode` and fails if the committed package graph changes.
Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
'.\scripts\audit-dependencies.ps1' -FailOnVulnerability` for the direct and
transitive package inventory and NuGet advisory check. The dependency license
review is recorded in [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md).

The Windows CI workflow in [`.github/workflows/verify.yml`](.github/workflows/verify.yml)
runs the same verification, dependency, redacted secret-scan, and performance
controls. The performance report retains min/median/p95 samples; short default
runs gate on median because p95 is the maximum sample with only three
iterations, while runs with at least 20 samples gate on p95. Bundled-data
provenance and the approval-gated tracked-artifact cleanup are recorded in
[`docs/audit/LUNA-22-BUNDLED-DATA-REVIEW.md`](docs/audit/LUNA-22-BUNDLED-DATA-REVIEW.md).

The checks verify the important workbook-derived drilldowns, including:

- Stanley Drake: 39 transaction lines totalling 15,000.
- Flex Projects L: 4 AP contractor-payment lines totalling 7,420, grouped by Manual Name rather than `Contractors Payments`.
- FY26/FY27/FY28 workbook report values and the raw-data pivot totals.

## Important files

- `docs/CODEX_START_HERE.md` - prompt/instructions to give Codex first.
- `docs/REBUILD_SPEC_FOR_CODEX.md` - full functional and technical specification.
- `docs/WORKBOOK_ANALYSIS.md` - workbook structure and calculation mapping.
- `src/ProjectCostForecast.App/Data/SampleData.json` - workbook seed data extracted from the uploaded file.
- `1.Mar 26.xlsm` - root source workbook being recreated and enhanced.
- `source_workbook/1.Mar 26.xlsm` - archived source workbook copy.
- `tests/ProjectCostForecast.Tests` - no-dependency acceptance check harness.
- `docs/RECOVERY_RUNBOOK.md` - user-facing backup, retention, and restore instructions.
- `docs/DIAGNOSTICS_RUNBOOK.md` - user-facing diagnostics and corrupt-preference recovery instructions.
- `docs/DATE_TIME_CONTRACT.md` - NZ business-date, UTC persistence, display-locale, and legacy timestamp rules.
- `docs/STATE_MODEL.md` - project state categories, ownership, identities, derived values, dirty tracking, and follow-up boundaries.
- `docs/TEST_COVERAGE_MAP.md` - complete legacy-harness-to-discovered-test mapping and test isolation rules.

## Stack

- C#
- .NET 8
- WPF
- MVVM-style view models
- JSON seed data for the initial app state
- ClosedXML for Excel import

## Architecture

The app code is organised so each file owns one domain concern:

- `Models/` - data objects, converters, and grid state attached properties.
- `Services/` - non-UI logic: `CalculationService` (CTD/CTC/FCC maths), `FiscalPeriod`
  (single home for fiscal period/year parsing, formatting, and ranges),
  `SchedulingService` (calendar-aware CPM engine: forward/backward pass, float,
  constraints, hammocks, baseline slip), `CsvTransactionService`,
  `ProjectFileService`, `ValidationService`, `UserPreferencesService`,
  `SampleDataService`.
- `ViewModels/MainWindowViewModel.*.cs` - one partial class split by domain:
  core state/commands, `Dataset`, `ProjectIO`, `ViewsAndFilters`, `Workspaces`,
  `Pivots`, `ForecastColumns`, `KpisAndCustomPivot`, `TotalsAndPreferences`,
  `LedgerChart`, `MonthlyReport`.
- `MainWindow.*.cs` - one partial code-behind class split by concern:
  core wiring, `TabDragDrop`, `WorkspacePanels`, `WindowChrome`, `GridBuilders`,
  `GridFilters`, `ColumnMenus`, `GridStatePills`, `ForecastGridInteraction`,
  `WorkspaceColumnState`.
- `BrushFactory.cs` - shared frozen-brush creation used by views, view models, and models.

New backend logic should land in `Services/` as plain testable classes; new UI behaviour
should go in the matching partial file (or a new one) rather than growing any single file.

## Release status

The app has moved past the Alpha 1 starter into a practical release-candidate shape. The main remaining production work is packaging/signing, a true Excel `.xlsm` importer, and broader user acceptance testing against more real projects.
