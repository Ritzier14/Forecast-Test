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
- Raw transaction CSV import/export.
- Validation and audit tabs.
- Add, duplicate, and delete forecast lines.
- Command-line acceptance checks for the workbook-derived seed data.
- A P6-style Schedule tab with a CPM Gantt chart: activities, milestones, headings
  with sub-grouping, hammock tasks, FS/SS/FF/SF links with lag, early/late dates,
  total float and critical path, constraint dates, multiple calendars with holidays,
  named editable baselines, and working-day slip tracking against the active baseline.

## How to open

1. Install Visual Studio 2022 or later with `.NET desktop development` workload.
2. Open `ProjectCostForecast.sln`.
3. Set `ProjectCostForecast.App` as startup project.
4. Run with `F5`.

## How to verify

Run:

```powershell
dotnet build ProjectCostForecast.sln
dotnet run --project tests\ProjectCostForecast.Tests\ProjectCostForecast.Tests.csproj
```

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
