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

Project open/save use cases are coordinated by the headless `ProjectFileWorkflow`.
It depends on `IProjectFilePicker` and `IProjectPrompt` for paths, user
decisions, and notifications, and returns explicit succeeded/cancelled/failed
operation results. The workflow owns validation, Save As routing, content-hash
conflict handling, reload, and rollback of a caller-provided pre-save mutation.
`WpfProjectFilePicker` and `WpfProjectPrompt` are the only presentation
adapters used by the project open/save workflow; they construct its dialogs or
display its messages and do not own persistence or session state.

Import/export paths and review decisions are supplied through
`IImportExportInteraction`. `WpfImportExportInteraction` owns the WPF file
pickers, cost-centre mapping window, auto-create preview, unmatched-import
viewer, and notifications. The application workflow keeps CSV/XLSX/XLSM
parsing in `CsvTransactionService`, stages mapping and preview edits on a
cloned dataset, and commits them only after the final validation succeeds.
Backup restore path selection uses the same generic adapter boundary; the WPF
shell's close/discard and new-month prompts remain separate UI concerns.

CSV/XLSX/XLSM files are untrusted boundaries. `CsvTransactionService` applies explicit compressed-file, uncompressed-workbook, worksheet, row, column, cell, and cell-text limits before returning a complete import batch; cancellation or parse failure returns no partial batch. Re-import uses the existing transaction duplicate key and skips matching rows, so an all-duplicate import leaves project state unchanged. Formula-like text is neutralized only while writing CSV output; canonical model and JSON values are never rewritten for spreadsheet safety.

Date/time behavior follows `docs/DATE_TIME_CONTRACT.md`: fiscal and schedule calendar values are `DateOnly` NZ business dates, durable audit/snapshot/preference instants are UTC `DateTimeOffset` values, and display conversion uses `Pacific/Auckland` with `en-NZ`. Current workflow time enters through `IClock`; model constructors use explicit sentinels rather than ambient time. JSON converters accept legacy offset-free NZ-local timestamps and normalize persisted output to invariant UTC without changing fiscal-period dates.

### Project metadata presentation boundary

`ProjectTaskCode` and `ProjectCategory` are persisted model types. They retain
only the project metadata needed by JSON and application commands: names,
icon keys, colour hex values, ordering, and editability flags. They do not
expose WPF media types or call `MainWindow`.

`ProjectMetadataPresentation` and its WPF converters materialize the task and
category icons, colour brushes, labels, and invalid-colour fallbacks for
`TaskCategoryEditorWindow`. The ignored `DefaultHeaderColorHex` and
`DefaultColorHex` values remain plain fallback inputs assigned by the view
model; their visual interpretation belongs to the converter layer. This keeps
the persisted JSON names and values unchanged while making the model boundary
safe for the planned Core assembly.

The WPF composition root attaches `RuntimeExceptionPolicy` to the dispatcher,
application-domain, and unobserved-task boundaries. An unexpected UI or
application-domain failure is logged and follows a fail-fast shutdown policy;
an isolated unobserved task is logged and marked observed so the shell can
continue. The dispatcher policy presents a generic user message without
displaying exception details, project values, imported rows, names, or paths.

### Forecast and summary presentation boundary

The `Models` folder is now the agreed candidate set for persisted/domain
types. `ForecastLine`, `MonthlyForecast`, `CategorySummary`, the true summary
DTOs, and the other model files contain no `System.Windows`, control, dialog,
brush, image, visibility, or `MainWindow` dependency. `MonthlyForecast` keeps
its persisted amount/lock contract and editability notification; its old row
brush projections were dead UI state and have been removed.

The WPF-only `KpiPill`, `WorkspaceViewTab`, and
`ForecastMonthColumnDefinition` projections, the attached grid state helpers,
and the model-folder converters now live under
`src/ProjectCostForecast.App/Presentation`. `WorkspaceViewLayout` remains the
plain project-local persisted layout; `WorkspaceViewTab` is the live shell
projection and its `IconPreview` stays ignored by JSON. The XAML resource and
attached-property bindings point to the App namespace, preserving the existing
icons, colours, locking, separators, comparison visibility, and grid behavior.

`Luna14ArchitectureTests` scans every C# file in `Models` for forbidden WPF or
window references and includes a deliberate negative-control source string,
so adding a dependency to the agreed candidate set fails the discovered test
gate.

### Canonical financial state boundary

`ProjectDataset.ForecastLines` and `ProjectDataset.Transactions` are the
canonical live financial collections. They use the model-layer batch
observable collection so `MainWindowViewModel` can expose the same instances
to WPF without maintaining a second authoritative list or copying them during
`SyncDatasetFromCollections`. `CalculationService` remains the sole owner of
forecast-line derived values and the persisted category-summary compatibility
cache; resource and fiscal projections are rebuilt from those canonical inputs.

Saved-month viewing uses a separate display collection and swaps only the
active forecast presentation/view. It never replaces or edits the live
dataset collection. Initial cost-load period snapshots use a cloned dataset for
the same isolation guarantee. Persisted derived caches remain in the format
for backward compatibility and are refreshed from inputs before application
saves; their removal requires a future versioned migration.

### Canonical schedule, snapshot, and workspace state boundary

`ScheduleData` owns observable activity, calendar, link, and baseline
collections. The schedule view model exposes the activity and calendar
collections directly, while `SchedulingService` recalculates derived CPM
values and rebuilds links in place. Named collection subscription tracking
detaches old activity/calendar/baseline items and attaches replacement
references once during project load or collection reset.

`ProjectDataset.SavedMonthSnapshots` is the canonical observable history
collection exposed by the view model. Saved-month edits use the explicit
history editor and never enter current-period calculation. Project-local
workspace layouts remain dataset-owned presentation preferences; WPF
`WorkspaceViewTab` instances are a separate projection because they carry
ignored visual preview state. Application-wide `AppUserPreferences` stays in
its own preference-file boundary and changing it does not dirty a project.

## State and refresh rules

- A user operation should enter through one view-model method or command.
- Bulk edits must use an edit batch and request one dependent refresh at the end.
- Calculation services may scan transactions once per operation; they must not rebuild the same aggregate once per changed row.
- Collection resets and dynamic grid-column rebuilds should be coalesced on the dispatcher.
- Every editable persisted collection must track item and collection changes, update affected totals, and set `IsDirty`.
- Subscriptions owned by a window must be named, idempotent, detached when the data context changes, and detached when the window unloads.

`RefreshCoordinator` is the application refresh boundary. A user operation submits
one `RefreshRequest` with explicit projection flags; overlapping requests merge
before dispatcher execution, and spreadsheet edit batches hold the request until
the batch closes. `RefreshDiagnostics` records request/coalescing/execution
counts and phase durations for calculation, calculated views, collection views,
raw-transaction pivots, grid columns, totals, ledger, grouping, and filter lists.
The forecast grid captures stable item/column identity, selection, scroll offsets,
active editor text, and group expansion before dynamic column replacement and
restores each value only when its identity still exists. Filter values remain in
the view model, while dispatcher-queued column rebuilds prevent collection and
property notifications from rebuilding the same grid more than once per turn.

`MainWindow` owns one named lifetime boundary for visual wiring. `Loaded` and
`DataContextChanged` call the idempotent view-model/Gantt attach methods, while
`Unloaded` and `Closed` detach grid, scroll-viewer, routed-event, width,
timer, and dispatcher work. Each queued window action captures a lifetime
version and is ignored after unload or close. Closing the shell also disposes
the view model, stopping its preference/search timers and aborting queued
refresh or schedule work. Reopening the same window reattaches handlers and
restores transient visual state without repeating the initial column rebuild.

Child-window asynchronous work must use an observed task boundary. The
schedule comparison window owns a lifetime cancellation source, one source per
refresh request, and the set of active refresh tasks. A newer request cancels
the previous source; close cancels and awaits the active tasks before
disposing the lifetime source. Cancellation is handled separately from
failures, and failures are sent to the diagnostics service before any error
message is shown. UI assignment is allowed only while both the window and its
request are active.

The WPF binding gate is test-scoped rather than a production-wide trace
suppression. `WpfBindingErrorCapture` attaches to
`PresentationTraceSources.DataBindingSource` only for the representative
smoke path, prefixes each captured error with its surface name, and restores
the previous trace level on disposal. The path exercises the main forecast,
resources/ledger, schedule, monthly report, and saved-month surfaces; no
framework noise or application binding errors required an allow-list.

Right-click grid panning is a presentation concern owned by the attached
`RightClickGridPanBehavior`. MainWindow, cost-centre mapping, and task/category
editor grids all use the same 6px threshold, header/scrollbar exclusions,
capture/release lifecycle, bounded horizontal and vertical offsets, and
post-drag context-menu suppression. `RightClickGridPanSession` keeps the
threshold and offset policy independent of WPF input, while the behavior owns
per-grid state and cancels it on capture loss, Escape, unload, or detach.

Monthly report cards implement `IReportCanvasObjectHost` and delegate header
dragging to one `ReportCanvasDragController`. The controller owns canvas
coordinate conversion, bounded position updates, mouse capture, and temporary
z-order changes; each card keeps its own layout and dirty-state callback.
`ReportCanvasObjectPositioning` is the shared clamp used by placement, resize
containment, and drag updates. Persisted colour values are parsed and
normalized by the WPF-free `ColorValueParser`; `BrushFactory` is the only
boundary that turns those values into WPF brushes and owns the shared default
header gradient. Common colour labels and icon swatches use `ColorPalette`.

Forecast curve allocation has one pure financial boundary in
`ForecastCurveMath`. `ForecastCurveService` uses it when applying a curve to
editable forecast months, and `ForecastCurvePresets` uses the same allocator
for editor previews and saved user shapes. The allocator rounds financial
values to cents and preserves the requested total by assigning any residual
to the largest period. Cumulative markers and their interactive smoothing
remain separate display/interaction math, so chart scaling and pointer edits
do not redefine committed financial allocation.

Performance evidence has an explicit opt-in gate in
[`scripts/verify-performance.ps1`](../scripts/verify-performance.ps1). It builds
Release, runs the headless LUNA-20 workload over deterministic small, normal,
and stress datasets, records dataset byte sizes and p95 timings in
[`docs/audit`](audit), and compares every scenario with the baseline using the
documented `max(10 ms, 25%)` tolerance. Forecast-only spreadsheet refreshes
omit the unrelated raw-transaction pivot; imports and manual full recalculation
retain that dependency. `RefreshDiagnostics` records the phase counters used
to prove both paths.

Dependency resolution is controlled by the root `Directory.Build.props`: every
project writes a committed `packages.lock.json`, restore enables NuGet audit for
direct and transitive packages, and `scripts/verify.ps1` uses `--locked-mode`.
`scripts/audit-dependencies.ps1` records the resolved package graph and queries
the configured NuGet advisory source; the current 22-package inventory has zero
vulnerability records. License metadata and the required release review are
recorded in [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md). ClosedXML is pinned
to the compatible 0.105.1 patch; transitive versions are not independently
upgraded.

The state inventory and ownership boundary is recorded in
[`STATE_MODEL.md`](STATE_MODEL.md). It classifies every `ProjectDataset` root
collection and persisted calculated field, records the current identity and
dirty-tracking seams, and records the LUNA-16A canonical forecast/transaction
decision, the LUNA-16B schedule/workspace state boundary, the LUNA-17
refresh-preservation contract, and the LUNA-20 targeted refresh evidence.

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
4. `.\scripts\verify-performance.ps1 -EnforceRegression` for the deterministic
   LUNA-20 workload baseline and p95 regression threshold.
5. `.\scripts\audit-dependencies.ps1 -FailOnVulnerability` for the locked
   direct/transitive package inventory and advisory check.

`docs/TEST_COVERAGE_MAP.md` maps all 428 logical assertions from the original console harness to named discovered tests. The executable in `tests/ProjectCostForecast.Tests` remains unchanged as an opt-in compatibility smoke check invoked with `-RunLegacySmoke`; it is deliberately not a second default test gate and must not be retired without explicit user approval.
