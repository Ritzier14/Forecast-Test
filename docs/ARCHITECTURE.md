# Project Cost Forecast architecture

## Direction

The application is an incremental modular monolith. WPF remains the delivery shell, while business calculations, scheduling, persistence, and user interaction are separated behind explicit seams before assemblies are split. This avoids a high-risk rewrite of the spreadsheet-derived workflow.

The dependency direction for new work is:

```text
WPF views and adapters
        |
application/view-model orchestration
        |
domain models and calculation services
        |
storage and import/export adapters
```

Domain and calculation code must not acquire new dependencies on `Window`, `DataGrid`, `Brush`, `Visibility`, file dialogs, or message boxes. Existing presentation properties in model files are migration debt, not a pattern to extend.

## Current composition boundary

`MainWindowViewModelDependencies` owns construction of calculation, scheduling, import, storage, validation, and preferences services. The parameterless view-model constructor is retained for XAML/startup compatibility and delegates to that boundary. Tests and future startup code should pass explicit dependencies and an initial-dataset factory.

Storage is accessed through `IProjectFileService` and `IUserPreferencesService`. JSON writes go to a unique temporary file in the destination directory, are flushed, and then replace the destination. Project backups use collision-safe names, are verified through the project migration/validation boundary, and follow a bounded retention policy. `DiagnosticsService` is a best-effort local rolling log; it records operation and exception type with sanitized context, and its write failures never mask the original failure. Malformed preferences are quarantined before defaults are loaded.

CSV/XLSX/XLSM files are untrusted boundaries. `CsvTransactionService` applies explicit compressed-file, uncompressed-workbook, worksheet, row, column, cell, and cell-text limits before returning a complete import batch; cancellation or parse failure returns no partial batch. Re-import uses the existing transaction duplicate key and skips matching rows, so an all-duplicate import leaves project state unchanged. Formula-like text is neutralized only while writing CSV output; canonical model and JSON values are never rewritten for spreadsheet safety.

Date/time behavior follows `docs/DATE_TIME_CONTRACT.md`: fiscal and schedule calendar values are `DateOnly` NZ business dates, durable audit/snapshot/preference instants are UTC `DateTimeOffset` values, and display conversion uses `Pacific/Auckland` with `en-NZ`. Current workflow time enters through `IClock`; model constructors use explicit sentinels rather than ambient time. JSON converters accept legacy offset-free NZ-local timestamps and normalize persisted output to invariant UTC without changing fiscal-period dates.

The WPF composition root attaches `RuntimeExceptionPolicy` to the dispatcher,
application-domain, and unobserved-task boundaries. An unexpected UI or
application-domain failure is logged and follows a fail-fast shutdown policy;
an isolated unobserved task is logged and marked observed so the shell can
continue. The dispatcher policy presents a generic user message without
displaying exception details, project values, imported rows, names, or paths.

## State and refresh rules

- A user operation should enter through one view-model method or command.
- Bulk edits must use an edit batch and request one dependent refresh at the end.
- Calculation services may scan transactions once per operation; they must not rebuild the same aggregate once per changed row.
- Collection resets and dynamic grid-column rebuilds should be coalesced on the dispatcher.
- Every editable persisted collection must track item and collection changes, update affected totals, and set `IsDirty`.
- Subscriptions owned by a window must be named, idempotent, detached when the data context changes, and detached when the window unloads.

The pre-refactor state inventory and ownership boundary is recorded in
[`STATE_MODEL.md`](STATE_MODEL.md). It classifies every `ProjectDataset` root
collection and persisted calculated field, records the current identity and
dirty-tracking seams, and names LUNA-16A/LUNA-16B as the owners of the future
canonical forecast/transaction and schedule/workspace state decisions.

## Feature boundaries

New code should be organized by feature rather than added to the root `MainWindow` partial indefinitely. The first extraction candidate is Schedule because it already has distinct commands, a view-model partial, a calculation service, and Gantt rendering. The intended slices are:

1. `Forecast` — forecast grid, curve operations, filters, and calculated summaries.
2. `Transactions` — import/export, matching, raw ledger, and resource drilldown.
3. `Schedule` — activity editing, CPM calculation, baselines, and Gantt presentation.
4. `Reporting` — monthly/fiscal reports, pivots, KPI projections, and charts.
5. `Workspace` — tabs, saved column layouts, selection, and presentation preferences.

Each extracted WPF feature should expose a small control/view-model API. Cross-feature communication should use application methods or immutable result objects, not reach into another control's visual tree.

## Planned assembly split

After the remaining WPF types are removed from persisted/domain models:

- `ProjectCostForecast.Core` — persisted models, fiscal periods, calculations, forecast curves, validation, and scheduling.
- `ProjectCostForecast.Application` — use cases, refresh coordination, project session state, and service contracts.
- `ProjectCostForecast.Infrastructure` — JSON storage, preferences, CSV/Excel adapters, and backups.
- `ProjectCostForecast.App` — WPF views, dialogs, converters, templates, and composition root.

The split should be performed feature by feature with characterization tests. Moving files without correcting dependency direction does not count as a layer boundary.

## Verification gates

Every architecture change must keep these gates green:

1. `.\scripts\verify.ps1` as the authoritative entry point: Release build followed by the discovered tests in `tests/ProjectCostForecast.UnitTests`.
2. project save/load and CSV/XLSX/XLSM import/export boundary checks.
3. scheduling correctness, deterministic WPF interaction checks, and the characterized large-schedule/calculation timing checks.
4. a large-data refresh benchmark as soon as refresh coordination is extracted.

`docs/TEST_COVERAGE_MAP.md` maps all 428 logical assertions from the original console harness to named discovered tests. The executable in `tests/ProjectCostForecast.Tests` remains unchanged as an opt-in compatibility smoke check invoked with `-RunLegacySmoke`; it is deliberately not a second default test gate and must not be retired without explicit user approval.
