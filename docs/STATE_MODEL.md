# State model and source-of-truth contract

Status: LUNA-12 evidence packet, 2026-08-29.

This document records the state contract that exists before the forecast and
schedule ownership refactors. It is a map of the current application, not a
claim that every current mutation path is already ideal. The next ownership
packets must use this document as their boundary: LUNA-16A owns the canonical
forecast/transaction state and derived totals; LUNA-16B owns canonical
schedule, workspace, snapshot, and preference state.

## 1. State categories

Every value in the project workflow belongs to one of these categories:

| Category | Meaning | Persistence rule |
|---|---|---|
| Authoritative persisted input | A project value that a user, import, or workflow records and that future calculations or history must be able to use. This includes raw transactions, planning inputs, project metadata, audit history, and frozen saved-month records. | Stored in the project JSON, unless explicitly identified as application preference state. |
| Derived value | A value reproducible from authoritative current inputs or from a calculation service. A derived value may currently be written for compatibility, but it is not an independent source of truth. | Runtime-only when marked `JsonIgnore`; persisted derived caches remain compatibility debt until a versioned migration removes or replaces them. |
| Presentation preference | A project-local or user-local choice about layout, visibility, ordering, colours, filters, or display context. A project-local preference may affect which configured budget or columns are shown, but it is not financial source data. | Project-local preferences live under `ProjectDataset`; application-wide preferences live in the separate user-preferences file. |
| Transient UI state | Selection, editing, view-model wrappers, WPF objects, chart geometry, validation rows, and other state that can be rebuilt from the previous categories. | Not stored in the project JSON. |

The durable project boundary is `ProjectDataset`. The live view-model
collections are projections of that document while a project is open. The
current implementation still mirrors several lists and synchronizes them in
`SyncDatasetFromCollections`; that implementation detail is deliberately
visible here so LUNA-16A and LUNA-16B can remove it safely.

## 2. Ownership rules

There is one owner for each mutable state category:

| State or operation | Owner | Contract |
|---|---|---|
| Durable project document | `ProjectDataset`, written by `ProjectFileService` | The JSON document is the durable representation. `ProjectDatasetMigrationPipeline` normalizes it before use or save. |
| Open-project session mutations | `MainWindowViewModel` | A command or public view-model operation is the supported mutation gateway. It synchronizes the dataset, requests dependent recalculation, records audit data where applicable, and sets `IsDirty`. |
| Forecast and financial derived values | `CalculationService` | Current totals, actual-cost fields, forecast totals, variances, and category summaries are recalculated from inputs here. No view should become a second calculation owner. |
| Schedule derived values | `SchedulingService` | CPM dates, float, critical-path flags, baseline comparison, and schedule notes are outputs of schedule recalculation. |
| Format compatibility and normalization | `ProjectDatasetMigrationPipeline` | Version 0/1 shape repair, defaults, period/date normalization, and compatible migrations happen at the storage boundary. |
| Atomic persistence, revision, and backup | `ProjectFileService` | Validation, revision comparison, backup, temporary-file write, and replacement are storage concerns. |
| Application-wide preferences | `UserPreferencesService` and `AppUserPreferences` | Filter/display defaults and forecast-curve presets are separate from a project’s financial/domain state. |
| Visual/WPF state | Views and WPF adapters | `Brush`, `ImageSource`, `Visibility`, chart geometry, selection visuals, and control lifetime are rebuilt at the shell boundary. |

`ObservableModel` supplies notifications; it does not own persistence, dirty
state, or recalculation. A property setter therefore does not by itself prove
that the containing project has been marked dirty. The known notification
coverage is recorded in section 7 rather than hidden behind the model types.

## 3. ProjectDataset root inventory

The following table covers every root property currently serialized by
`ProjectDataset`.

| Root property | Classification | Identity / owner | Mutation and dependencies | Serialization notes |
|---|---|---|---|---|
| `FormatVersion` | Authoritative persisted format metadata | Project-file boundary; owned by migration pipeline | Set by migration/prepare-for-save; no calculation dependency | Current format is version 1. Legacy unversioned input is treated as version 0. |
| `Header` | Authoritative persisted project input | One project header per document; session owner is the view model | Project/new/open and header-edit paths; current period controls period locking and month rollover | Plain JSON object; required fields are checked at validation boundaries. |
| `Phases` | Authoritative persisted project planning input | Phase name plus project-local ordering; `MainWindowViewModel.Phases` is the live projection | Phase commands/edit paths; dates feed presentation and project context, not financial calculation | Plain list of `PhaseItem`. |
| `ForecastPeriods` | Authoritative persisted period configuration | Normalized FY-period label; dataset-owned because there is no public mirror collection | Import, migration, new-project, and period repair paths; drives monthly forecast/allocation date alignment | Plain list; `DateOnly` uses the LUNA-10 converter. |
| `FiscalYearBudgets` | Derived persisted compatibility mirror of the active budget line | Fiscal-year label; maintained by budget synchronization | Loaded as the legacy budget shape and regenerated from the active `BudgetLines` entry | Retained for old files and consumers; do not remove without a migration. |
| `BudgetLines` | Authoritative persisted budget inputs/configuration | Stable line `Key` (`P3M`, `LTP_AP`, or a future key); amount identity is line key plus fiscal year | Budget amount edits and active-line selection; report and budget-chart calculations consume it | `IsActive` is ignored runtime state; amounts are persisted. |
| `ActiveBudgetLineKey` | Project-local presentation/calculation selection | Budget-line key; view model owns selection command | `SetActiveBudgetLine` and budget loading; selects which budget is copied to the legacy fiscal-year view | Persisted because it changes the project’s selected comparison basis. |
| `ForecastLines` | Authoritative persisted forecast/planning inputs plus persisted derived caches | Current validation identity is normalized task number + resource name + nullable transaction-project scope; row number is a source/display locator | Import, line commands, monthly edits, comments, category/task metadata, and calculation refresh | Nested monthly forecasts, phases, cost lines, comments, and preferences are serialized. Derived fields listed in section 5 are currently also serialized unless ignored. |
| `ProjectTaskCodes` | Authoritative persisted task metadata and project-local task presentation | `SystemCode` is the task-code identity; display order is not identity | Task-code/category commands and metadata repair; forecast display resolves task names from this collection | Icon keys/colour hexes are plain data. WPF-derived properties are ignored. |
| `ProjectCategories` | Authoritative persisted category metadata and project-local category presentation | Normalized `Name` is the current category identity | Category commands and metadata repair; forecast reporting-category resolution consumes it | Icon keys/colour hexes are plain data. WPF-derived properties are ignored. |
| `ManagementResources` | Authoritative persisted management-planning inputs plus a persisted calculated-rate cache | No central typed key; current matching uses source row/task/resource/project context | Rate override/reset, monthly allocation edits, and import/resource matching; allocations and effective rate feed management forecast views | `SourceLine` is ignored. `CalculatedHourlyRate` is a persisted derived cache; `HourlyRate`, override flag, hours, and allocations are inputs. |
| `Transactions` | Authoritative persisted raw accounting input | Import duplicate fingerprint from `CsvTransactionService.BuildDuplicateKey`; source row number is a locator and is excluded from that fingerprint | CSV/XLSX/XLSM import and explicit transaction replacement; actual-cost calculation and drilldowns consume it | `LedgerResourceName` is a getter-only derived compatibility surface; current JSON behavior must be preserved or migrated before changing it. |
| `UnmatchedImportCombinations` | Authoritative persisted import-review history | Current combination is task/manual/project/category/source context; no generated durable ID | Import preview decision and unmatched-review actions | `RecordedAtDisplay` is ignored presentation. |
| `ContingencyEntries` | Authoritative persisted contingency records with a currently persisted derived remaining value | No generated ID; current row identity is date/context and collection position | Contingency collection/item tracking and contingency edit commands | `RemainingContingency` is treated as a derived compatibility value today even though its setter is public and it is serialized. |
| `CategorySummaries` | Derived persisted calculation cache | Reporting category/project code; rebuilt from forecast lines | `CalculationService.Recalculate` and view-model refresh | All fields are derived; retain shape until a migration and compatibility decision remove the cache. |
| `CostCenterNameMappings` | Authoritative persisted user/import association history | Mapping `Key` | Import association suggestions and manual-name recording | `LastUsedAt` is a durable instant; mapping is not a transient dialog row. |
| `SavedMonthSnapshots` | Authoritative persisted frozen history | Saved FY period, with snapshot order by `SavedAt`; line identity is row/task/resource/project | New Month creates one frozen record; saved-month view reads it without feeding current calculation | Values are historical observations, not live derived values. Nested forecast amounts are copied history. |
| `AuditEvents` | Authoritative persisted append-only history | `AuditId` | View-model edit/import/save/month operations append events | `ChangedAtDisplay` is ignored; `ChangedAt` is a UTC durable instant. |
| `WorkspaceViews` | Project-local presentation preference | Workspace key + content key/name; layout object identity is `ReportCanvasObjectLayout.Id` | Workspace/tab commands and explicit layout save paths | Layout, columns, report-canvas objects, and visibility are project-local, not financial inputs. |
| `WorkspaceTabOrder` | Project-local presentation preference | Workspace key string | Tab drag/reorder commands | Plain ordered string list. |
| `DetailWorkspaceTabOrder` | Project-local presentation preference | Detail workspace key string | Detail-tab drag/reorder commands | Plain ordered string list. |
| `ForecastGroupHeaderIconKeys` | Project-local presentation preference | Forecast group key | Header/icon customization commands | Case-insensitive string dictionary; no calculation dependency. |
| `ForecastGroupHeaderIconColorHexes` | Project-local presentation preference | Forecast group key | Header colour customization commands | Case-insensitive string dictionary; plain hex data. |
| `ForecastCalendarYearHeaderColorHexes` | Project-local presentation preference | Calendar-year header key | Header colour customization commands | Presentation only. |
| `ForecastFiscalYearHeaderColorHexes` | Project-local presentation preference | Fiscal-year header key | Header colour customization commands | Presentation only. |
| `ForecastGroupHeaderColorHexes` | Project-local presentation preference | Forecast group key | Header colour customization commands | Presentation only. |
| `SelectedCtcMonthForecastYears` | Project-local presentation preference | Calendar-year integer | CTC-column selection commands; affects visible columns only | Persisted project-local display context, not forecast data. |
| `ShowCtcMonthForecastYearTotals` | Project-local presentation preference | Single project flag | CTC-column display command | Persisted project-local display context. |
| `Schedule` | Authoritative persisted schedule inputs/configuration plus ignored derived outputs | Activity/calendar IDs and baseline names; `SchedulingService` owns outputs | Schedule commands and explicit schedule edit paths; CPM recalculation consumes schedule inputs | Schedule input and history are serialized; computed activity outputs are ignored. |

## 4. Nested persisted entities

### Project, periods, budgets, and metadata

| Type | Persisted authoritative fields | Derived, ignored, or preference fields |
|---|---|---|
| `ProjectHeader` | `ProjectTitle`, `ReportTitle`, `CurrentPeriod`, `SourceWorkbook`, `ImportNotes` | None; current-period validity is a validation rule. |
| `PhaseItem` | `Name`, nullable `Start`, nullable `End` | None. |
| `ForecastPeriod` | `Column`, `Label`, nullable `StartDate` | None; the canonical calendar month is derived from the label during validation/migration. |
| `FiscalYearBudget` | `FiscalYear`, `Budget` | Legacy budget representation; current budget editing is owned by `BudgetLines`. |
| `FiscalYearBudgetLine` | `Key`, `Name`, `Amounts` | `IsActive` and `Total` are ignored runtime state; `Total` is the sum of amounts. |
| `FiscalYearBudgetAmount` | `FiscalYear`, `Amount` | None. `Amount` is an editable budget input. |
| `ProjectTaskCode` | `SystemCode`, `TaskName`, `IsRawDataCode`, `IsManualCode`, `DisplayOrder`, `IconKey`, `IconColorHex`, `HeaderColorHex` | `DefaultHeaderColorHex`, `CanEditSystemCode`, `CanDelete`, `IconPreview`, `IconColorBrush`, `IconColorLabel`, `HeaderColorBrush`, and `HeaderColorLabel` are ignored derived/presentation values. |
| `ProjectCategory` | `Name`, `ColorHex`, `IconKey`, `DisplayOrder` | `DefaultColorHex`, `IconPreview`, `ColorBrush`, and `ColorLabel` are ignored derived/presentation values. |

### Forecast lines and nested forecast state

`ForecastLine` mixes several kinds of state and is therefore the highest-risk
object in this packet. Its inputs, caches, history, and presentation outputs
must not be treated as interchangeable:

| Nested type / fields | Classification | Owner and dependency |
|---|---|---|
| `RowNumber`, `TaskNumber`, `ResourceName`, `ProjectCode`, `UseLedgerResourceMatchOnly`, nullable `TransactionProjectCode`, `ReportingCategoryOverride`, `FormatGroup`, `IsManuallyAdded` | Authoritative persisted input/configuration | Forecast-line commands/import; matching and category/task resolution consume them. |
| `Budget` | Authoritative persisted planning input | Budget edit path; report and variance calculations consume it. |
| `CommentsOnMonthForecastVariance`, `CommentsOnMonthBudgetVariance`, `CommentsOnTotalBudgetVariance`, `ManualAllMonthComment`, `UseManualAllMonthComment`, `ManualCommentPeriodLabel`, `ManualCommentMonthLabel`, nullable `ManualCommentRecordedAt` | Authoritative persisted user commentary/configuration | Comment commands; reports and saved-month views display them. |
| `ResourceCommentMetrics` | Authoritative persisted per-line presentation preference | Resource-detail display commands; no financial calculation dependency. |
| `MonthlyCommentHistory` | Authoritative persisted commentary history | Comment commands; history is not a calculation input. |
| `MonthlyForecasts` | Authoritative persisted monthly planning inputs | `MonthlyForecast.Amount` edits feed `CalculationService`; `PeriodLabel`/`PeriodStartDate` are aligned to `ForecastPeriods`. |
| `TaskPhases` | Authoritative persisted forecast-line planning input | Detail/report presentation; period labels are validated. |
| `TaskCostLines` | Authoritative persisted forecast-line planning input | Detail/report presentation; amounts are validated. |
| `LastMonthPlannedCost`, `LastMonthForecast` | Authoritative persisted roll-forward history/input | New Month writes the prior-period snapshot; variance calculations compare current values to these frozen values. They are not live recalculation outputs. |
| `CostToDate`, `TotalBudgetVarianceQuick`, `PlannedCostVsBudgetQuick`, `CurrentMonthCost`, `LastMonthForecastQuick`, `VarianceActualVsForecastMonthQuick`, `TotalForecastCtc`, `CostToDateSummary`, `PlannedCostFcc`, `MonthForecast`, `VarianceLastMonthToDate`, `MonthForecastVariance`, `TotalBudgetVariance` | Derived values currently persisted for compatibility | `CalculationService` is the sole calculation owner. These fields must be removed or explicitly versioned only in LUNA-16A. |
| `RowDisplayHeight`, `HasCustomRowHeight`, `TaskName`, `ReportingCategory`, `HasManualAllMonthComment`, `AllMonthComments`, the indexer, and `GetCalendarYearForecastTotal` | Transient/derived presentation or calculation helpers | Rebuilt from inputs; ignored or getter-only and not project authority. |

For `MonthlyForecast`, `PeriodLabel`, `PeriodStartDate`, `Amount`, and
`IsLocked` are currently serialized. `IsLocked` is a persisted workflow/UI
choice used to prevent editing closed periods. `ActualCostAmount`,
`IsEditable`, `BackgroundBrush`, and `ForegroundBrush` are derived or WPF
presentation state; the latter are ignored.

`ForecastMonthlyComment` persists `PeriodLabel`, `MonthLabel`, `ResourceName`,
`Text`, and `RecordedAt`. `PeriodSortKey` and `DisplayText` are ignored
presentation. `ResourceCommentMetricPreference` persists `Key`, `Label`,
`IsVisible`, and `DisplayOrder`.

### Management resources and transactions

| Type | Authoritative fields | Derived or transient fields |
|---|---|---|
| `ManagementResource` | `SourceRowNumber`, `TaskNumber`, `ResourceName`, `ProjectCode`, `MonthlyAllocations`, `HourlyRate`, `IsHourlyRateOverridden`, `MonthlyHours` | `CalculatedHourlyRate` is a persisted derived cache from source/rate calculation. `RateStatus`, the indexer, and `SourceLine` are derived/ignored. |
| `ManagementResourceAllocation` | `PeriodLabel`, nullable `PeriodStartDate`, `Percentage` | None; percentage is an editable planning input. |
| `ManagementResourceTableRow` | None | Transient wrapper around a `ManagementResource` and metric; never a dataset item. |
| `CostTransaction` | `RowNumber`, `FyPeriod`, `TaskNumber`, `Period`, nullable `DocDate`, `Units`, `UnitRate`, `Amount`, ledger/account/project fields, resource fields, source/PO fields, supplier/narrative/who/ECM fields, `ManualName` | `LedgerResourceName` is a derived getter used for matching. It is not a second resource field; its current serializer exposure requires compatibility care. |

### History, contingency, mappings, and workspaces

| Type | Classification and fields |
|---|---|
| `ContingencyEntry` | Authoritative `Date`, `ContingencyExpended`, `ProposedExpenditure`, `Reason`, and `Status`; `RemainingContingency` is currently a persisted derived compatibility value. |
| `CategorySummary` | Entirely derived persisted cache: `ProjectCode`, `TotalForecast`, `CostToDate`, `CurrentMonthCost`, `PlannedCost`, `Budget`, `TotalBudgetVariance`, and `MonthForecastVariance`. |
| `CostCenterNameMapping` | Authoritative association history: `Key`, resource/source descriptors, `ManualName`, `UseCount`, and durable `LastUsedAt`. |
| `UnmatchedImportCombination` | Authoritative import-review record: `RecordedAt`, task/manual/project/category/source context, `Amount`, and `TransactionCount`; `RecordedAtDisplay` is ignored presentation. |
| `SavedMonthSnapshot` | Authoritative frozen history: `Period`, `SavedAt`, `CostToDate`, `CostToComplete`, `FinalForecast`, `TotalBudgetVariance`, and nested `SavedMonthForecastLine` values. These values are not inputs to current-period calculation. |
| `WorkspaceViewLayout` | Project-local presentation preference: workspace/content/name/icon data, hidden columns, column layouts, zero-display/grouping options, and report-canvas settings/objects. |
| `ReportCanvasObjectLayout` | Project-local layout preference: stable `Id`, object type, geometry, text/style/chart/filter/date settings, and value keys. |
| `WorkspaceColumnLayout` | Project-local column preference: `Key`, `Width`, and `DisplayIndex`. |

### Schedule

`ScheduleData` persists `ProjectStart`, `MustFinishBy`, `DefaultCalendarId`,
`ActiveBaselineName`, `Calendars`, `Activities`, `Links`, and `Baselines`.

| Schedule type | Authoritative persisted state | Derived/ignored state |
|---|---|---|
| `ScheduleCalendar` | `Id`, `Name`, `WorkingDays`, `Holidays`, `ExtraWorkDays`, `ColorHex`, `IsVisibleOnGantt` | `IsWorkingDay` and `HasAnyWorkingWeekday` are calculated helpers. |
| `ScheduleActivity` | `Id`, `Name`, `Kind`, `OutlineLevel`, `DurationDays`, `CalendarId`, `ConstraintType`, nullable `ConstraintDate`, `PredecessorText`, `HammockMemberText`, `PercentComplete`, `Notes`, `IsUnscheduled` | `IsHeading`, `IsMilestone`, `IsHammock`, `EarlyStart`, `EarlyFinish`, `LateStart`, `LateFinish`, `TotalFloatDays`, `IsCritical`, `BaselineStart`, `BaselineFinish`, `SlipDays`, and `ScheduleNote` are scheduling outputs and ignored. |
| `ActivityLink` | `PredecessorId`, `SuccessorId`, `Type`, `LagDays` | `TypeLabel` and parser helpers are derived. |
| `ScheduleBaseline` | `Name`, `CapturedAt`, and nested `ScheduleBaselineEntry` values | `FindEntry`/`EnsureEntry` are helpers; baseline entries are historical persisted state. |
| `ScheduleBaselineEntry` | `ActivityId`, nullable `Start`, nullable `Finish` | None. |
| `ParsedPredecessor` | None | Temporary parser result; not a `ScheduleData` collection. |

## 5. Persisted calculated values and compatibility policy

The following values are calculated today but remain in the JSON shape:

1. All listed `ForecastLine` quick/total/variance fields in section 4.
2. Every field of `CategorySummary`.
3. `ContingencyEntry.RemainingContingency`.
4. `ManagementResource.CalculatedHourlyRate`.
5. `FiscalYearBudgetLine.Total` is calculated but already excluded with
   `JsonIgnore`; `IsActive` is runtime selection state and is also excluded.
6. Schedule activity CPM/baseline outputs are calculated and excluded with
   `JsonIgnore`.
7. `MonthlyForecast.ActualCostAmount`, audit/unmatched display strings,
   comment display/sort values, task/category resolved display values, and
   WPF brushes/images are calculated or presentation-only and excluded.

The compatibility rule is conservative: a persisted cache is not silently
discarded in LUNA-12. It is documented, characterized, and left available to
old files. LUNA-16A or LUNA-16B must provide a format migration or an explicit
reconstruction policy before changing JSON shape.

## 6. Identity map

Identity is the key used to match, replace, audit, or preserve selection. A
display index or source row is not automatically a durable identity.

| Entity | Current identity rule | Gap or follow-up |
|---|---|---|
| Forecast line | `ValidationService` currently normalizes `TaskNumber`, `ResourceName`, and nullable `TransactionProjectCode`; null uses a legacy sentinel. `RowNumber` identifies the source/display row. | `ProjectCode` and `UseLedgerResourceMatchOnly` are not part of the current duplicate-validation key. A typed key and its migration policy belong to LUNA-16A. |
| Cost transaction | `CsvTransactionService.BuildDuplicateKey` combines normalized FY period/task/period/date/units/rate/amount, ledger/account/project/parent/resource/source, PO fields, supplier/narratives, `Who`, and ECM. | It is an import fingerprint, not a generated database ID; row number and `ManualName` are not in the fingerprint. Preserve this rule for duplicate import behavior until a replacement is characterized. |
| Task code | `SystemCode` | Raw/manual editability is policy, not identity. |
| Category | Normalized `Name` in current metadata/lookup paths | A generated category ID would require migration and reporting compatibility. |
| Forecast period | Normalized FY `Label` | `StartDate` is a canonical derived alignment check, not identity. |
| Fiscal-year budget amount | Budget-line `Key` plus normalized fiscal-year label | `FiscalYearBudgets` is the legacy active-line projection. |
| Management resource | Current source row/task/resource/project context | No single typed key exists; LUNA-16A/16B must decide whether source-row identity is stable across re-import. |
| Cost-centre mapping | `CostCenterNameMapping.Key` | Mapping key is owned by import association logic. |
| Unmatched combination | Current task/manual/project/category/source context and recorded history | No generated ID; review replacement/removal semantics need characterization. |
| Saved month snapshot | `Period` for the saved month; line uses row/task/resource/project fields | Snapshot history is append/frozen state; do not use a live forecast-line object as its identity. |
| Audit event | `AuditId` | Append-only; entity/field values describe the event target. |
| Workspace layout | Workspace key + content key/name; report object `Id` | Layout objects require stable IDs for selection/drag persistence. |
| Schedule calendar | `Id` | Activities refer to this ID. |
| Schedule activity | `Id` | Links and baselines refer to this ID. |
| Schedule link | Predecessor ID + successor ID + link type + lag | Collection order is not identity. |
| Schedule baseline | `Name` in current active-baseline selection plus its captured entries | Names are user-editable and not an ideal generated ID; LUNA-16B owns a future decision. |

## 7. Mutation, dirty tracking, and refresh coverage

The intended operation sequence is:

```text
view/action -> MainWindowViewModel mutation gateway
            -> SyncDatasetFromCollections (where a mirror exists)
            -> CalculationService or SchedulingService
            -> derived projections and validation refresh
            -> IsDirty = true and audit/status update
            -> ProjectFileService on save
```

Current coverage is intentionally recorded as follows:

| State area | Current tracked boundary | Characterized limitation |
|---|---|---|
| Monthly forecast amount | `MonthlyForecast.AmountChanged` is subscribed for loaded forecast lines; the handler audits, recalculates, refreshes, and marks dirty. | Nested collection additions/replacements and arbitrary new item subscriptions are not a generic collection contract. |
| Contingency entries | Collection changes and each tracked `ContingencyEntry.PropertyChanged` mark dirty and refresh totals. | The tracking is a feature-specific subscription owned by the view model. |
| Budget amounts | Loaded `FiscalYearBudgetAmount.PropertyChanged` is subscribed; amount edits sync both budget representations, refresh reports/charts, and mark dirty. | Direct changes to a newly replaced nested amount list are unsafe until subscription ownership is centralized. |
| Management resources | Resource edit paths and management allocation operations explicitly notify/recalculate; live table rows wrap dataset resources. | Nested allocation lists and arbitrary direct item edits do not have one generic persisted-edit boundary. |
| Schedule | Public schedule commands and schedule input properties call `MarkScheduleDirtyAndRecalculate`; activity subscriptions are attached during schedule load. | Calendar/baseline/nested collection lifecycle and all direct object edits need the LUNA-16B replacement contract. |
| Transactions | Import and explicit collection operations own mutation and dirty state. | `CostTransaction` is not an observable model, so direct property changes do not raise a dirty event. |
| Task/category metadata and phases | Commands/load/metadata-repair paths explicitly rebuild dependent views. | Direct edits to arbitrary items are not globally observed. |
| Comments, snapshots, unmatched records, audit history | Feature commands add/update records and set dirty as appropriate. | Direct list/item mutation is not a supported generic boundary. |
| Workspace layouts and tab order | Workspace commands/build-layout paths synchronize project-local preference state. | A `WorkspaceViewTab`/nested list property change does not universally mark the project dirty by itself. |
| Header, period configuration, dictionaries, and root flags | Specialized view-model paths mutate dataset-owned values directly. | They do not all flow through `SyncDatasetFromCollections`; this is an explicit ownership seam for later packets. |

`SyncDatasetFromCollections` currently copies forecast lines, task codes,
categories, management resources, transactions, category summaries,
contingency entries, phases, saved snapshots, unmatched records, audit events,
workspace layouts, selected CTC years, and display flags, then delegates budget
and schedule synchronization. `Header`, `ForecastPeriods`, and several
project-local dictionaries/tab-order values remain dataset-owned and are
updated by specialized paths. This difference must not be erased from tests
until a canonical owner is chosen.

## 8. Recalculation dependencies

```text
raw transactions --------------------+
monthly forecast amounts ------------+
forecast-line inputs/budget ----------+--> CalculationService
task/category metadata --------------+        |
management planning inputs ----------+        +--> forecast-line derived fields
fiscal-year/budget-line inputs -------+        +--> category summaries
                                                   +--> resources/reports/pivots/KPIs/charts

schedule inputs + links + calendars -----------> SchedulingService
                                                   +--> CPM/baseline outputs

workspace and user preferences ----------------> presentation projections
saved-month snapshots and audit history -------> historical/read-only views
```

Current calculation rules are:

- Transactions provide actual cost and attribution inputs.
- Monthly forecast amounts and budget provide planning inputs.
- Forecast-line task/category metadata controls resolution and grouping.
- `CalculationService` writes current derived values and rebuilds category
  summaries; the view model then rebuilds resource summaries, reports, pivots,
  monthly reports, KPIs, and chart data.
- Contingency totals are derived from the contingency collection and are not a
  forecast-line calculation dependency.
- Schedule inputs are independent of financial calculation; only the schedule
  service may write CPM/baseline outputs.
- Saved snapshots and audit events are frozen history. They are never silently
  fed back into current-period calculation.
- Workspace and application preferences affect presentation and interaction,
  except for explicitly documented project-local selections such as the active
  budget line.

## 9. Serialization boundary

Project load follows this contract:

1. `ProjectFileService` reads bytes and passes the stream to
   `ProjectDatasetMigrationPipeline`.
2. The pipeline rejects null/malformed/future formats, deserializes the
   supported shape, repairs compatible defaults, and normalizes dates/lists.
3. The resulting dataset is validated before it can replace the active
   session.
4. `MainWindowViewModel.LoadDataset` creates the live projections, subscribes
   the feature-specific events, recalculates, and clears dirty state for a
   clean load.

Project save follows this contract:

1. The view-model operation synchronizes live projections to its dataset
   boundary and checks the revision when saving an existing path.
2. `ProjectFileService` prepares the dataset for the current format, validates
   it, creates the approved backup/revision boundary, and writes JSON through
   the atomic file service.
3. `JsonIgnore` properties and WPF objects are excluded. Date-only business
   dates and UTC durable instants use the LUNA-10 converters.
4. Dirty state is cleared only after a successful save; a validation, conflict,
   or write failure retains the dirty session.

`AppUserPreferences` follows a separate JSON boundary. Its filter flags,
selected display settings, KPI keys, and `UserForecastCurvePreset` records are
application preferences, not project financial state. A project load must not
silently turn those preferences into `ProjectDataset` authority.

## 10. Explicit follow-up extensions

The following items are intentionally not fixed by LUNA-12:

- LUNA-13/LUNA-14: remove WPF media/brush/window dependencies from persisted
  model files and move presentation into presenters, converters, or views.
- LUNA-16A: make forecast lines and transactions canonical, give mutations one
  entry point, recompute summaries from inputs, and remove persisted derived
  values only through a compatible migration. Decide the typed forecast-line
  identity and transaction replacement semantics.
- LUNA-16B: apply the ownership rules to schedule, workspace, and snapshot
  state; attach/detach nested item subscriptions exactly once; prove dirty
  coverage for every persisted edit; and keep project-local presentation
  separate from financial state.
- LUNA-17: coalesce recalculation and projection refreshes after ownership is
  clear.
- LUNA-18A/B: audit window lifetime, dispatcher work, cancellation, and
  subscription disposal.

No production refactor is hidden in this document. Any unresolved/high-risk
state slice must enter one of those packets with a named acceptance test.

## 11. LUNA-12 characterization evidence

`StateModelCharacterizationTests` records the pre-refactor contract for:

- round-tripping editable root collections and nested items through the
  project-file boundary;
- preserving currently persisted compatibility caches while excluding ignored
  derived/WPF values;
- monthly forecast item edits driving recalculation, audit, and dirty state;
- contingency collection/item edits driving totals and dirty state;
- budget amount item edits synchronizing report state and dirty state; and
- schedule recalculation owning CPM outputs without persisting those outputs.

These tests are characterization tests, not permission to broaden the packet.
They should be changed only when the relevant LUNA-16A or LUNA-16B ownership
decision is implemented and its migration/compatibility impact is reviewed.
