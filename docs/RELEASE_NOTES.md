# Project Cost Forecast Release Notes

## Release Candidate - 2026-05-10

This build advances the original Alpha 1 workbook rebuild toward the Alpha 3+ roadmap and a usable release candidate.

### Added

- Calculation service for cost to date, current-period actuals, CTC, FCC, last-month variance, month variance, budget variance, and category summaries.
- Manual Name-first transaction matching so AP contractor payments drill into the correct supplier/resource.
- Editable monthly forecasts in the resource drilldown panel.
- Audit events for forecast edits, line changes, imports, and saves.
- Validation tab for forecast and transaction data quality issues.
- Workbook Report tab matching the spreadsheet's FY spent-to-date, cost-to-complete, planned-cost, AP/LTP budget, and variance block.
- Actuals Pivot tab matching the workbook pivot's task/resource/FY-period grouping.
- Open, save, and save-as project JSON files, with timestamped backups when overwriting.
- Verified backup restore to a new project path, safe overwrite restore with a pre-restore backup, and bounded backup retention.
- Bounded sanitized diagnostics, top-level exception handling, and timestamped quarantine for malformed user preferences.
- CSV import for raw transactions with common header aliases.
- CSV export for raw transactions.
- Bounded CSV/XLSX/XLSM import with typed malformed/oversized-file failures, cancellation safety, deterministic duplicate skipping, and closed file handles.
- Spreadsheet-safe CSV text export that neutralizes formula-like values while preserving canonical project data.
- Documented NZ business-date, NZ display-time, and UTC durable-instant rules, with injectable clocks and legacy local-timestamp migration.
- Forecast line add, duplicate, and delete commands.
- Release-candidate status/header information and Save / Discard / Cancel handling for unsaved changes on close.
- No-dependency command-line acceptance checks in `tests/ProjectCostForecast.Tests`.
- Complete discovered xUnit migration with a 428/428 legacy-assertion coverage map, named calculation/import/scheduling/view-model/WPF tests, and one authoritative verification path.
- State/source-of-truth contract covering every project dataset collection, persisted calculated field, identity rule, dirty-tracking seam, serialization boundary, and explicit LUNA-16A/LUNA-16B follow-up.
- Project and task/category metadata presentation moved into App-level WPF converters; persisted icon keys and colour hex values remain plain JSON data.
- Forecast and summary WPF projections, attached grid state, and model-folder converters moved into the App presentation boundary; persisted forecast, summary, and workspace layout values remain unchanged.
- Project open/save/save-as decisions moved into the headless `ProjectFileWorkflow`; WPF file-picker and prompt adapters preserve the existing dialogs while enabling cancellation, validation, I/O, and stale-file conflict tests without a real window.
- Import/export path selection, cost-centre mapping, auto-create preview, unmatched-import review, and backup-restore path selection moved behind the headless `IImportExportInteraction`; staged import edits commit only after the final validation succeeds.
- Forecast lines and transactions now use dataset-owned observable collections exposed directly by the view model; saved-month viewing uses a separate display projection and initial cost-load snapshots calculate on cloned state.
- Schedule activities/calendars/baselines, saved-month history, and project-local workspace layouts now have explicit dataset-owned collection boundaries; reload-safe subscription tracking detaches old schedule/workspace objects, while application-wide preferences remain separate.
- Refresh work now enters a coalescing `RefreshCoordinator` with explicit projection targets, phase counters/timings, one end-of-batch spreadsheet refresh, and dispatcher-coalesced dynamic grid-column rebuilds.
- Forecast-grid refreshes preserve stable selection/current-cell identity, scroll offsets, active editor text, filters, and task-group expansion when the underlying items and columns remain available.
- A scoped WPF binding-error capture gate now covers the main forecast, resources/ledger, schedule, monthly report, and saved-month smoke surfaces without globally suppressing binding diagnostics.
- Right-click grid panning now uses one shared attached behavior across the main, cost-centre mapping, and task/category editor grids, with bounded two-axis scrolling and drag-safe context-menu handling.
- Monthly report cards now share one canvas drag/position controller, while hexadecimal colour parsing, labels, and the default header gradient use one tested presentation boundary.
- Forecast curve previews and applied forecasts now share the pure `ForecastCurveMath` allocator, including profile weights, cent rounding, residual assignment, and user-shape resampling.

### Verified

- The solution builds cleanly.
- Seed data contains 63 raw transactions totalling 27,695.
- Stanley Drake drilldown resolves 39 transaction lines totalling 15,000.
- Flex Projects L drilldown resolves 4 AP transaction lines totalling 7,420.
- Flex Projects L AP rows group by Manual Name rather than the generic `Contractors Payments` resource description.
- Category summaries recalculate from forecast lines.
- FY report values match Excel's cached formulas to two decimal places.
- Actuals pivot values match the workbook pivot totals.
- Seed data has no validation errors.
- The LUNA-12 state characterization suite round-trips editable nested project state, proves known monthly/contingency/budget dirty boundaries, and confirms schedule-derived outputs remain runtime-only.
- The LUNA-13 characterization suite proves the persisted task/category models have no WPF or `MainWindow` dependency, preserves icon/colour fallback behavior, and round-trips current and legacy project fixtures.
- The LUNA-14 architecture suite proves the model candidate set has no WPF/window/control references, rejects a deliberate forbidden reference, preserves forecast/grid/KPI/workspace presentation behavior, and round-trips current and legacy forecast/summary fixtures.
- The LUNA-15A workflow suite proves open/save success, cancellation, validation failure, I/O failure, stale-file conflict decisions, audit rollback, and active-session/dirty-state preservation with 10 headless tests.
- The LUNA-15B workflow suite proves import/export cancellation and failure reporting, headless mapping and preview decisions, no partial transaction/mapping mutation before commit, unmatched review routing, and the absence of direct file-dialog/window dependencies from `MainWindowViewModel.ProjectIO.cs`.
- The LUNA-16A suite proves canonical financial collection identity, derived category-cache rebuilding from inputs, legacy-fixture total parity, and saved-month projection isolation/restoration.
- The LUNA-16B suite proves schedule/snapshot collection identity, idempotent replacement subscription tracking, dirty coverage for supported persisted editors, preference separation, and old-project subscription detachment.
- The LUNA-17 suite proves merged refresh requests, batch-held refreshes, one calculation/pivot path for a full refresh, stable filter/resource/line selection, and the measured refresh phase contract; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-18A suite proves one named MainWindow lifecycle owner, matching routed/grid/scroll detachment, generation-guarded dispatcher work, and view-model timer/refresh disposal; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-18B suite proves observed schedule-comparison refresh tasks, separate cancellation/failure handling, close-time cancellation and awaiting, stale-write suppression, diagnostics routing, and child close-handler detachment; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-18C suite proves scoped WPF binding trace capture, actionable surface/path diagnostics, and zero unexpected binding errors across the representative main-window smoke path; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-19A suite proves the shared grid-pan threshold, direction and bounds policy, disabled-axis behavior, cancellation lifecycle, and three-surface wiring; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-19B suite proves supported hex normalization, shared colour labels and default-gradient construction, canvas position bounds, shared controller wiring, z-order restoration, and WPF parser/brush separation; the Release verifier also covers the retained 428-assertion smoke gate.
- The LUNA-19C suite proves preview/application curve parity across profiles, boundary and negative inputs, exact rounding residuals, total preservation, user-shape allocation, and removal of duplicate profile formulas; the Release verifier also covers the retained 428-assertion smoke gate.

### Remaining Before A Formal Production Release

- Package/sign an installer or MSIX.
- Build a direct `.xlsm` importer from the workbook instead of relying on extracted JSON seed data.
- Add richer Excel/PDF report export.
- Add persistent multi-project database storage if multiple live projects need to be managed together.
- Run user acceptance testing with more than the supplied workbook.
