# Project Cost Forecast codebase audit and remediation plan

- Date prepared: 2026-08-29
- Source guide: `docs/Vibe_Coded_Codebase_Common_Problems_and_Audit_Guide_EXPANDED.pdf`
- Execution model: one narrowly scoped packet per Luna Max run, followed by an independent Sol Ultra acceptance audit.

## 1. Outcome and scope

The guide is relevant to this repository, but not every web-oriented item applies. This is a local .NET 8 WPF desktop application that persists JSON files and imports/exports CSV and Excel files. It has no server API, authentication system, payment flow, tenant boundary, ORM, or database.

The plan therefore prioritises:

1. prevention of user data loss and partial operations;
2. trustworthy automated tests and release gates;
3. versioned, validated, recoverable persistence;
4. clear state ownership and separation of business logic from WPF;
5. event, binding, async, and refresh correctness;
6. safe import/export, dependency, and release practices;
7. measured performance work; and
8. documentation that agrees with the implemented product.

This document is a point-in-time audit and execution plan. It does not assert that every suspected issue is a defect. Packets marked "verify, then fix" require a failing characterization test or other reproducible evidence before production code is changed.

## 2. Repository profile and evidence baseline

The inventory was taken from the current working tree, including existing uncommitted work:

- 113 C# or XAML files and approximately 53,683 lines under `src` and `tests`.
- 20 root-level `MainWindow*.cs` files with approximately 15,317 lines.
- 21 `MainWindowViewModel*.cs` files with approximately 10,507 lines.
- A single 1,844-line console acceptance harness in `tests/ProjectCostForecast.Tests/Program.cs`.
- One production package, `ClosedXML` 0.105.0, plus seven transitive packages.
- No package lock file and no existing CI workflow.
- A GitHub remote is configured, so a Windows GitHub Actions gate is appropriate.
- Release executables, DLLs, PDBs, runtime files, and packaged sample data are tracked under `release/ProjectCostForecast`.
- `Temp/data_anonymised.xlsx` is also tracked.
- The working tree already contains many modified and untracked files. These changes pre-date this plan and must not be discarded or silently mixed with audit work.

Primary evidence reviewed:

- `README.md`
- `docs/ARCHITECTURE.md`
- `docs/CODEX_START_HERE.md`
- `docs/REBUILD_SPEC_FOR_CODEX.md`
- `docs/RELEASE_NOTES.md`
- the source guide named above
- application, model, service, and view-model source
- the acceptance harness and both project files
- tracked release contents, package inventory, and current Git status

### Existing controls that must be preserved

- `AtomicJsonFile` writes to a unique same-directory temporary file, flushes to disk, and atomically replaces the destination.
- `ProjectFileService` creates collision-safe pre-save backups.
- The acceptance harness already covers substantial calculation, persistence, import, scheduling, WPF interaction, and performance behaviour.
- The schedule comparison flow already uses cancellation; it needs lifecycle hardening rather than removal.
- The application explicitly sets `en-NZ` culture.
- `docs/ARCHITECTURE.md` already defines the intended dependency direction, refresh rules, and incremental modular-monolith approach.
- The 2026-08-29 NuGet audit found no known vulnerable direct or transitive package from the configured NuGet source.
- A targeted source scan found no obvious committed credential literal. This is not a substitute for the source-and-history scan in the release packet.

## 3. Guide applicability matrix

| Guide area | Applicability | Repository-specific interpretation | Packets |
|---|---|---|---|
| Architecture, boundaries, oversized modules | Direct | WPF types and `MainWindow` calls exist in model classes; UI and file dialogs are embedded in view-model orchestration; two main partial-class families hold about 25,800 lines. | LUNA-12 through LUNA-16B |
| Duplication and inconsistent implementations | Direct | Parallel panning, report-canvas interaction, colour parsing, and forecast-curve implementations need characterization and consolidation. | LUNA-19A to LUNA-19C |
| State ownership, derived state, collections, identity | Direct | The persisted dataset and observable view-model collections are synchronised in both directions, and calculated/presentation values are also stored on models. | LUNA-12, LUNA-16A, LUNA-16B |
| UI events, bindings, forms, and lifecycle | Direct | Large programmatic grids, repeated Loaded/DataContext refreshes, many event subscriptions, timers, and async dispatcher work create lifecycle and stale-UI risk. | LUNA-18A to LUNA-18C |
| Business-rule placement and validation | Direct | Calculation services exist, but validation is currently refreshed for display rather than proven as a save/load boundary. | LUNA-05, LUNA-06A, LUNA-14, LUNA-15A, LUNA-15B |
| Async, concurrency, retries, and idempotency | Direct/partial | Relevant to dispatcher tasks, cancellation, repeated import, double invocation of "New Month," and concurrent edits to the same local file. Distributed-job guidance is not applicable. | LUNA-04, LUNA-06B, LUNA-09, LUNA-18B |
| Error handling and observability | Direct | User-facing message boxes and `Debug.WriteLine` exist, but there is no persistent diagnostic trail or global exception policy; preference corruption is silently swallowed. | LUNA-08 |
| Persistence, schema evolution, integrity, backup, recovery | Direct and high priority | JSON is atomic and backed up, but it is unversioned, migration logic is mixed into the view model, null can become an empty project, and restore is not exercised as a product capability. | LUNA-04 to LUNA-07 |
| File and spreadsheet security | Direct | JSON, CSV, XLSX, and XLSM are untrusted input boundaries. CSV formula injection, malformed/oversized workbooks, resource limits, and sample-data disclosure are relevant. | LUNA-09, LUNA-22 |
| Performance and scalability | Direct | Full collection replacement, grid-column rebuilds, and refresh fan-out need realistic measurement. Desktop virtualization replaces web pagination concerns. | LUNA-17, LUNA-20 |
| Configuration and environment separation | Partial | Local preferences, diagnostic locations, packaged data, build configuration, and release settings apply. Server environment variables and secret stores do not currently apply. | LUNA-08, LUNA-21 to LUNA-23 |
| Dependencies and supply chain | Direct | ClosedXML and transitives process complex files; restore is not locked and no automated vulnerability gate exists. | LUNA-21 |
| Testing, review capacity, version control, release readiness | Direct and high priority | `dotnet test` discovers no real suite, CI is absent, the working change set is broad, and generated releases are committed. | LUNA-00 to LUNA-02, LUNA-11, LUNA-22 to LUNA-24 |
| Dates, time zones, and locale | Direct/partial | NZ business dates are important, while persisted audit/snapshot timestamps mix `DateTime.Now` and local `DateTimeOffset.Now`. | LUNA-10 |
| Context drift, specification drift, scope bloat, comprehension debt | Direct | Release notes claim a close warning that the window does not call; architecture guidance and source still diverge; very large change sets reduce review confidence. | All packet rules, especially LUNA-00, LUNA-12, and LUNA-23 |
| Authentication, sessions, authorization, tenant isolation | Not applicable now | There are no accounts, roles, server sessions, or tenant-owned records. Re-open this area before adding shared or hosted access. | None |
| HTTP APIs, CORS/CSRF, API rate limiting, pagination | Not applicable now | There is no HTTP service or browser client. | None |
| Payments and webhooks | Not applicable now | No payment or webhook flow exists. | None |
| Database, ORM, SQL injection, query plans, N+1, DB migrations | Not applicable now | Persistence is local JSON plus spreadsheet files. File-format migration remains applicable and is covered separately. | None |
| Distributed deployment, queues, workers, caches | Not applicable now | The application is a single desktop process. Release packaging and recovery still apply. | None |

## 4. Prioritised finding register

Priority meanings:

- **P0**: credible data-loss, corruption, or audit-trust risk; resolve before calling the build production-ready.
- **P1**: high reliability, security, test, or architectural risk; resolve before broad feature expansion.
- **P2**: maintainability, performance, or release-hardening work; fix after P0/P1 controls exist, unless measurement elevates it.

| ID | Priority | Confidence | Finding and evidence | Planned response |
|---|---:|---|---|---|
| F-00 | P0 process gate | Confirmed | The working tree has dozens of existing modified files plus untracked source files. An agent cannot safely infer ownership or create a reviewable audit diff without a checkpoint. | LUNA-00 |
| F-01 | P0 | Confirmed | `MainWindow.OnClosing` at `MainWindow.xaml.cs:269` only persists window preferences. It does not call the existing `MainWindowViewModel.ConfirmClose()`. `RELEASE_NOTES.md` says the warning exists, while `ReimplementationTodoWindow.cs` says it is disabled. | LUNA-03 |
| F-02 | P0 | Confirmed | `SetupNewMonth` in `MainWindowViewModel.ProjectIO.cs:1146` inserts a baseline, mutates every forecast line, advances the period, writes audit events, and refreshes before calling `SaveProject()` at line 1190. A failed or cancelled save leaves live state partially rolled forward. | LUNA-04 |
| F-03 | P1 | Confirmed | The only test project is an executable with no test SDK. `dotnet test` can build it without executing its assertions; `docs/ARCHITECTURE.md` explicitly warns about this. There is no CI workflow. | LUNA-01, LUNA-02, LUNA-11, LUNA-22 |
| F-04 | P1 | Confirmed | `ProjectFileService.Load` deserializes directly and converts JSON `null` into a new empty project. `ProjectDataset` has no explicit format version. Compatibility normalization and migration-like work occur in view-model loading code. | LUNA-05 |
| F-05 | P1 | Confirmed/verify extent | `ValidationService` is called during refresh, but save does not directly enforce a validated boundary. Load failure handling, duplicate identity rules, required-field invariants, and session preservation need explicit tests. | LUNA-06A |
| F-06 | P1 | Confirmed | Two app instances can load the same file and overwrite each other without a checked file revision or explicit conflict decision. Atomic replacement protects file shape, not stale-write loss. | LUNA-06B |
| F-07 | P1 | Confirmed | Backups live beside the source, have no retention policy, and are not verified through a clean restore drill. They protect against some bad saves but not same-location loss or an unusable backup. | LUNA-07 |
| F-08 | P1 | Confirmed | `UserPreferencesService.Load` catches every exception and silently returns defaults. Runtime diagnostics are primarily debug output; no global dispatcher/task exception policy or persistent support log exists. | LUNA-08 |
| F-09 | P1 | Confirmed/verify extent | CSV escaping only handles delimiters and quotes, not spreadsheet formula prefixes. Import paths need explicit file/row/cell limits, malformed-file tests, and idempotency checks. | LUNA-09 |
| F-10 | P1 | Confirmed | Model files reference `Brush`, `ImageSource`, `Visibility`, dependency properties, converters, and `MainWindow.GetBuiltInImageSourceByPath`, contrary to the documented dependency direction. | LUNA-13, LUNA-14 |
| F-11 | P1 | Confirmed/verify behaviour | Dataset lists and observable collections are mirrored through `SyncDatasetFromCollections` and repeated `ReplaceCollection` calls. Calculated totals and summaries also live on persisted models. The authoritative state and recomputation contract are not explicit. | LUNA-12, LUNA-16A, LUNA-16B |
| F-12 | P1 | Confirmed | File dialogs and message boxes are called directly from `MainWindowViewModel.ProjectIO.cs`, coupling use cases to WPF and making failure paths harder to test. | LUNA-15A, LUNA-15B |
| F-13 | P1 | Confirmed | Release binaries, PDBs, runtime files, and data files are tracked. A second anonymised workbook is tracked under `Temp`. Generated binaries obscure review and bundled data needs a privacy/provenance decision. | LUNA-22 |
| F-14 | P2, elevate if reproduced | Needs focused audit | Loaded/DataContextChanged flows rebuild multiple views; there are hundreds of subscription sites and several timers/dispatcher callbacks. Some code correctly detaches handlers, so this must be audited by owner/lifetime rather than mechanically rewritten. | LUNA-18A, LUNA-18B |
| F-15 | P2, elevate if reproduced | Confirmed/needs runtime scan | There is no automated WPF binding-error gate. Schedule comparison dispatches an `async` lambda through `BeginInvoke`, which needs explicit exception, cancellation, and disposal ownership. | LUNA-18B, LUNA-18C |
| F-16 | P2 | Confirmed | Parallel implementations exist for grid panning, report-canvas selection/drag, colour parsing/labels, and forecast-curve math. Behaviour must be characterized before choosing a canonical implementation. | LUNA-19A to LUNA-19C |
| F-17 | P2, elevate if measured | Needs measurement | Collection replacement, notification fan-out, and dynamic grid rebuilds can cause redundant work. Existing debug timings and synthetic schedule checks are useful but do not establish realistic end-to-end targets. | LUNA-17, LUNA-20 |
| F-18 | P2 | Confirmed | Restore is not locked. ClosedXML 0.105.1 is available while 0.105.0 is used. No known vulnerabilities were reported on 2026-08-29, but that point-in-time result is not an automated control. | LUNA-21 |
| F-19 | P2 | Confirmed | Persisted event/snapshot metadata mixes `DateTime.Now` and local `DateTimeOffset.Now`. Business dates, display-local times, and durable instants do not have a documented policy. | LUNA-10 |
| F-20 | P2 | Confirmed | Release notes are stale and contradict close behaviour; packaging/signing remains unfinished; no automated release checklist or recovery runbook exists. | LUNA-22, LUNA-23 |

## 5. Luna Max operating contract

### Mandatory start gate

Do not begin product changes while F-00 is unresolved. The user must first authorise a recoverable checkpoint of the current work, such as a named branch plus commit, or another explicitly approved snapshot. Luna must not stash, reset, delete, commit, or push existing work without that authorisation.

After the checkpoint, every Luna run must:

1. read this plan, `docs/CODEX_START_HERE.md`, and `docs/ARCHITECTURE.md`;
2. read only the guide sections relevant to its assigned packet;
3. inspect `git status --short` and the prior packet evidence;
4. state the assigned packet ID and its allowed scope before editing;
5. add or identify characterization tests before changing risky behaviour;
6. preserve unrelated changes and avoid broad formatting;
7. run the packet tests plus the full current verification gate;
8. run `git diff --check` and inspect the final diff;
9. update the packet ledger with commands, results, changed files, and residual risks; and
10. stop after one packet. Do not opportunistically start the next packet.

### Chunk-size limit

One packet is the maximum scope for one Luna Max run. A normal packet should touch no more than six production files, six test files, and roughly 600 net production lines. Generated files, fixtures, and mechanical moves are excluded, but their behavioural effect must still be reviewed.

If the packet cannot fit that boundary, Luna must stop after characterization and divide it into ordered `.1`, `.2`, and so on sub-packets in this plan. It must not silently expand the task.

### Change rules

- No broad redesign and no assembly split in one pass.
- No removal of serialized fields until backward-compatible fixtures and migration behaviour are green.
- No dependency addition without stating why the platform or existing code cannot meet the requirement.
- No performance rewrite without a before measurement and a reproducible benchmark.
- No duplicate consolidation until tests show that the variants are intended to behave the same.
- No swallowed exceptions. Expected failures must become typed results or narrowly handled exceptions with useful context.
- No logging of project financial values, imported row contents, personal names, or full user paths by default.
- No deletion under a backup-retention policy until the user has approved the policy and a dry run has listed the exact candidates; never remove the only verified good copy.
- No destructive repository cleanup in a mixed packet. Release-file removal is isolated in LUNA-22 and requires explicit user approval.
- No commit or push unless the user explicitly authorises it.

### Standard verification gate

Until LUNA-01 exists, run:

```powershell
dotnet restore ProjectCostForecast.sln
dotnet build ProjectCostForecast.sln -c Release --no-restore
dotnet run --project tests/ProjectCostForecast.Tests/ProjectCostForecast.Tests.csproj -c Release --no-build
git diff --check
```

After LUNA-01, use the repository verification script. After LUNA-02, that script must also run discovered tests. After LUNA-22, the same gate must run on a clean Windows GitHub Actions worker.

### Packet completion note

Each Luna run must append or update a ledger entry containing:

```text
Packet:
Status: complete | blocked | split
Baseline commit:
Changed files:
Characterization evidence:
Commands and results:
Manual checks:
Residual risks / follow-up packet:
```

A packet is not complete merely because the project builds.

## 6. Ordered Luna work packets

### Phase 0 - establish trustworthy change and test gates

#### LUNA-00 - Baseline and recoverable checkpoint

- Priority: P0 process gate
- Depends on: user authorisation for the checkpoint

Scope:

- Preserve all existing modified and untracked work without interpreting it as audit work.
- Record the checkpoint identifier, SDK/runtime version, Git status, package inventory, and current build/acceptance results.
- Record any baseline failure without repairing unrelated code in this packet.
- Create `docs/audit/BASELINE.md` and the packet ledger if they do not exist.

Acceptance:

- The pre-audit state is recoverable and named.
- Existing changes are attributable to the baseline rather than a later packet.
- Build and acceptance-harness status are recorded with exact commands and exit codes.

#### LUNA-01 - One-command local verification

- Priority: P1
- Depends on: LUNA-00

Scope:

- Add a PowerShell verification script under `scripts` that restores when requested, builds Release, runs the console acceptance harness, and propagates failures.
- Keep generated logs/artifacts outside tracked source or ignore them.
- Document prerequisites and a fast `-NoRestore` or equivalent path.

Acceptance:

- One command reproduces the current release gate from a clean checkout.
- A deliberately failing child command makes the script exit non-zero.
- It works from a path containing spaces.

#### LUNA-02 - Discovered-test foundation

- Priority: P1
- Depends on: LUNA-01

Scope:

- Add a conventional discovered test project while retaining the console harness.
- Port the atomic file, project save/load, backup, and core calculation characterization checks first.
- Provide an STA mechanism only for tests that genuinely require WPF.
- Update the verification script to run both the old harness and `dotnet test`.

Acceptance:

- `dotnet test` discovers and executes non-zero tests.
- The old acceptance harness remains green and mandatory.
- Ported tests use isolated temporary directories and leave no files or processes behind.

### Phase 1 - remove data-loss and persistence hazards

#### LUNA-03 - Safe close with unsaved changes

- Priority: P0
- Depends on: LUNA-02

Scope:

- Wire the window close path to an explicit Save / Discard / Cancel decision.
- Keep the window open on Cancel or failed/cancelled Save.
- Ensure application shutdown and repeated close events cannot bypass or duplicate the decision.
- Put the decision boundary behind a testable prompt abstraction if needed; do not grow direct `MessageBox` coupling.

Acceptance:

- Clean close has no prompt.
- Dirty + Cancel keeps the window and state open.
- Dirty + Discard closes only after explicit confirmation.
- Dirty + Save closes only after successful persistence; a forced save exception keeps the window open and dirty.
- Release notes no longer contradict behaviour.

#### LUNA-04 - Transactional and idempotent New Month

- Priority: P0
- Depends on: LUNA-02

Scope:

- Move month rollover into a testable application operation.
- Stage changes on a copy or retain a complete rollback snapshot.
- Commit the new baseline, prior-month values, current period, and audit events to live state only when persistence succeeds.
- Prevent double invocation while an operation is in progress.

Acceptance:

- A forced save failure leaves period, forecasts, snapshots, audit events, selection, and dirty state exactly as they were before invocation.
- Success creates exactly one baseline and advances exactly one period.
- Repeated click/command execution cannot create duplicate baselines.
- Save/cancel/error paths have discovered tests.

#### LUNA-05 - Versioned project format and migration pipeline

- Priority: P1
- Depends on: LUNA-02

Scope:

- Add an explicit project-file format version independent of the app binary version.
- Create a pure migration/normalization pipeline outside the WPF view model.
- Move existing null-list/default/date normalization into that pipeline in small compatible steps.
- Reject JSON `null`, malformed files, and unsupported future versions without replacing the current session.
- Add representative fixtures for the oldest supported format, current format, null, malformed, and future format.

Acceptance:

- Old supported fixtures migrate deterministically and save as current format.
- Current files round-trip without unexplained value changes.
- Future or invalid files produce a clear error and preserve the open project.
- Migration is idempotent and has no WPF dependency.

#### LUNA-06A - Validation at operation boundaries

- Priority: P1
- Depends on: LUNA-05

Scope:

- Define validation severity and stable issue codes.
- Enforce errors before save, month rollover, and committing imports; warnings remain visible but do not silently become errors.
- Validate post-migration invariants before a loaded project replaces the active session.
- Characterize required identifiers, duplicate keys, invalid periods/dates, and financial numeric bounds.

Acceptance:

- Invalid state cannot be persisted as a successful save.
- Failed load/import leaves the active project unchanged.
- All blocking rules have focused tests and user-facing messages name the operation and remedy.

#### LUNA-06B - Same-file stale-write protection

- Priority: P1
- Depends on: LUNA-05

Scope:

- Capture a file revision token when a project is opened or saved.
- Before overwrite, detect external modification and require an explicit Reload / Save As / Cancel decision.
- Do not pretend that an overwrite prompt solves field-level merge; document that merge is unsupported.

Acceptance:

- Two service/session instances reproducing a stale write cannot silently overwrite the newer file.
- Save As remains available and normal single-instance saving is unchanged.
- Conflict handling is testable without opening WPF dialogs.

#### LUNA-07 - Backup integrity, retention, and restore drill

- Priority: P1
- Depends on: LUNA-05, LUNA-06A

Scope:

- Define a bounded, documented retention policy without deleting the only known-good copy.
- Validate a backup can deserialize, migrate, and pass integrity checks before reporting it as usable.
- Provide a safe restore workflow that defaults to a new path or creates a pre-restore backup.
- Document the limitation of same-directory backups and the recommended external/off-device copy.

Acceptance:

- Automated tests create, verify, restore, and compare a backup in a clean temporary directory.
- A corrupt backup is rejected without damaging the current project.
- Retention is deterministic and tested at timestamp/name collisions.
- A user-facing recovery runbook can be followed without source-code knowledge.

#### LUNA-08 - Runtime diagnostics and corrupt preferences

- Priority: P1
- Depends on: LUNA-01

Scope:

- Add a small diagnostics abstraction with bounded local rolling logs.
- Handle dispatcher, app-domain, and unobserved-task failures at the appropriate boundary while preserving fail-fast behaviour for unsafe states.
- Include operation and exception type, but redact project values, imported row contents, personal names, and full paths by default.
- Quarantine malformed preference files with a timestamped name, use defaults, and provide a diagnostic/user notice instead of a silent catch-all.

Acceptance:

- A simulated preference parse failure preserves the bad file, loads defaults, and records a sanitized reason.
- A simulated top-level UI exception follows the documented user-message and diagnostic policy.
- Log growth is bounded and failure to write diagnostics never masks the original failure.

#### LUNA-09 - CSV and workbook boundary hardening

- Priority: P1
- Depends on: LUNA-02, LUNA-06A, LUNA-08

Scope:

- Neutralize spreadsheet formula injection for exported text fields beginning, after ignorable leading characters, with `=`, `+`, `-`, or `@`.
- Keep the canonical in-memory/JSON value unchanged; apply export encoding only at the boundary.
- Define file-size, worksheet, row, and cell limits based on realistic project data, with actionable failures.
- Test malformed CSV, malformed/unsupported workbook, multiline fields, duplicate import, and disposed/closed file handles.
- Ensure a cancelled or failed import commits no partial rows.

Acceptance:

- Formula payload fixtures are inert when exported and retain their original domain value.
- Oversized or malformed input fails within bounded time/memory and leaves the project unchanged.
- Importing the same source twice follows the documented duplicate policy.
- Existing supported CSV/XLSX/XLSM round trips remain green.

#### LUNA-10 - Date, time-zone, and locale contract

- Priority: P2
- Depends on: LUNA-05

Scope:

- Document which values are NZ business dates (`DateOnly`), local display times, and durable instants (`DateTimeOffset` in UTC).
- Introduce a clock seam for audit/snapshot workflow timestamps.
- Migrate legacy local timestamps compatibly; do not change period semantics.
- Use invariant representations in persisted files and filenames while retaining `en-NZ` display.

Acceptance:

- Tests cover NZ daylight-saving transitions, month/year rollover, legacy JSON, and a non-NZ machine culture.
- Saving and reopening does not shift a business date or instant.
- No new durable timestamp uses ambient `DateTime.Now`.

#### LUNA-11 - Complete discovered-test migration

- Priority: P1
- Depends on: LUNA-02 through LUNA-10

Scope:

- Inventory every logical section of the console harness in a coverage map.
- Port remaining calculation, import, scheduling, UI interaction, and performance checks into named discovered tests.
- Keep WPF-dependent tests isolated and deterministic.
- Retire or reduce the executable harness only after the map proves parity and the user approves the change.

Acceptance:

- `dotnet test` alone executes the complete mapped suite and fails when a representative assertion is deliberately broken.
- Tests do not depend on order, shared mutable state, machine-specific paths, or an interactive desktop unless explicitly categorized as manual UI tests.
- The verification script has one authoritative automated-test path; any retained smoke executable has a distinct documented purpose.

### Phase 2 - make state and boundaries understandable

#### LUNA-12 - State/source-of-truth contract

- Priority: P1
- Depends on: LUNA-05, LUNA-11

Scope:

- Create `docs/STATE_MODEL.md` listing each important field/collection as authoritative persisted input, derived value, presentation preference, or transient UI state.
- Map ownership, mutation entry points, stable identity, dirty tracking, recalculation dependencies, and serialization compatibility.
- Add characterization tests for editable persisted collections and items before refactoring.
- This is an evidence packet; only fix tiny, isolated dirty-tracking omissions discovered by a focused test.

Acceptance:

- Every `ProjectDataset` collection and persisted calculated field is classified.
- There is one stated owner for each mutable state category.
- Unclear/high-risk slices are converted into explicit extensions of LUNA-16A or LUNA-16B rather than hidden in this audit.

#### LUNA-13 - Remove project/category presentation from persisted models

- Priority: P1
- Depends on: LUNA-12

Scope:

- Keep persisted icon keys and colour hex values as plain data.
- Move `ImageSource`, `Brush`, and `MainWindow.GetBuiltInImageSourceByPath` behaviour for project/task/category models into WPF presenters, converters, or view models.
- Preserve JSON shape unless LUNA-05 supplies a migration.

Acceptance:

- The affected persisted model types compile without references to WPF media or `MainWindow`.
- Icon/colour visuals and invalid-colour fallbacks remain equivalent in manual and automated checks.
- Old project fixtures still load and round-trip.

#### LUNA-14 - Remove WPF types from forecast and summary domain candidates

- Priority: P1
- Depends on: LUNA-12, LUNA-13

Scope:

- Move forecast row brushes, summary `Visibility`/image/brush state, and model-folder converters into presentation-specific types.
- Distinguish true domain summaries from WPF row projections rather than mechanically moving namespaces.
- Establish an automated architecture check for the agreed core-model candidate set.

Acceptance:

- Core/persisted model candidates have no `System.Windows`, WPF control, dialog, or `MainWindow` dependency.
- Grid locking, colours, icons, comparison visibility, and summary values behave the same.
- The architecture check fails on a deliberately introduced forbidden reference.

#### LUNA-15A - Decouple open/save use cases from WPF dialogs

- Priority: P1
- Depends on: LUNA-03 through LUNA-08, LUNA-14

Scope:

- Introduce narrow file-picker, prompt, and operation-result boundaries.
- Move open/save/save-as decision logic into testable application workflows.
- Keep the WPF adapter responsible only for displaying dialogs and messages.

Acceptance:

- Open/save success, cancellation, validation failure, conflict, and I/O failure are tested without a real window.
- The active session and dirty flag remain correct on every failure path.
- Open/save view-model code no longer directly constructs `OpenFileDialog`, `SaveFileDialog`, or `MessageBox`.

#### LUNA-15B - Decouple import/export use cases from WPF dialogs

- Priority: P1
- Depends on: LUNA-09, LUNA-15A

Scope:

- Apply the same adapter boundary to import/export, preview, and unmatched-item decisions.
- Keep CSV/Excel parsing in infrastructure services and business decisions in application workflows.
- Preserve existing preview and mapping behaviour.

Acceptance:

- Import/export cancellation and all failure paths are headless-testable.
- No partial project mutation occurs before the final user decision.
- `MainWindowViewModel.ProjectIO.cs` has no direct file-dialog dependency after both LUNA-15A and LUNA-15B.

#### LUNA-16A - Canonical forecast/transaction state and derived totals

- Priority: P1
- Depends on: LUNA-12, LUNA-14, LUNA-15B

Scope:

- Use the state contract to make one canonical owner for forecast lines and transactions.
- Stop copying authoritative items between dataset and observable mirrors where the same collection/view can be exposed safely.
- Recompute category/resource/fiscal summaries from inputs; remove persisted derived values only through a compatible LUNA-05 migration.
- Preserve saved-month read-only projections as a separate, explicit state.

Acceptance:

- A mutation has one entry point, marks dirty once, and produces one consistent set of totals.
- Save/load and old-fixture totals match expected values.
- Switching to and from a saved month cannot mutate the live project or leave stale rows.

If the state contract identifies more than one independent feature slice, split this packet before editing.

#### LUNA-16B - Canonical schedule/workspace/snapshot state

- Priority: P1
- Depends on: LUNA-12, LUNA-16A

Scope:

- Apply the same ownership and identity rules to schedules, baselines, snapshots, workspaces, and preferences.
- Ensure collection replacement detaches old item subscriptions and attaches each new item exactly once.
- Prove dirty-state coverage for every persisted edit.

Acceptance:

- Add/remove/replace/edit tests show no missed dirty state and no duplicate callbacks.
- Loading multiple projects in one process does not retain old-project subscriptions or objects.
- Workspace presentation preferences remain separate from financial/domain state where appropriate.

#### LUNA-17 - Refresh coordinator and UI-state preservation

- Priority: P1/P2
- Depends on: LUNA-16A, LUNA-16B

Scope:

- Define one coalesced refresh request per user operation, with explicit affected projections.
- Remove demonstrably duplicate recalculation, collection reset, or grid-column rebuild paths.
- Preserve selection, scroll position, active edit, filters, and group expansion when the underlying identity still exists.
- Add counters/timing around refresh phases for LUNA-20.

Acceptance:

- Representative edit/import/load/workspace operations perform the documented number of recalculations and rebuilds.
- UI state preservation has focused tests or a deterministic smoke harness.
- No correctness regression is traded for fewer refreshes.

### Phase 3 - lifecycle, duplication, and measured performance

#### LUNA-18A - Main window lifecycle and subscription ownership

- Priority: P2, elevate reproduced leaks/duplicates to P1
- Depends on: LUNA-17

Scope:

- Inventory MainWindow subscriptions, timers, Loaded/Unloaded, DataContextChanged, and close paths by owner and lifetime.
- Make attach/detach methods named and idempotent.
- Remove duplicate initial rebuilds only when counters/tests prove equivalence.

Acceptance:

- Replacing DataContext and reopening/closing the main window do not multiply callbacks or retain the old view model.
- Timers and pending dispatcher work are stopped or safely ignored after close.
- Initial grids still populate exactly once with the expected state.

#### LUNA-18B - Child-window async, cancellation, and events

- Priority: P2, elevate reproduced failure to P1
- Depends on: LUNA-18A

Scope:

- Audit child windows, especially schedule comparison, for anonymous subscriptions, timers, cancellation-token ownership, and dispatcher async lambdas.
- Replace fire-and-forget work with an observed task boundary; framework event handlers may remain `async void` only with local exception handling.
- Cancel and await or safely detach work during close.

Acceptance:

- Closing during refresh/import cannot update a disposed window or surface an unobserved exception.
- Cancellation and non-cancellation exceptions have separate tests and diagnostics.
- Repeated open/close does not grow handler counts or retain windows.

#### LUNA-18C - WPF binding-error gate

- Priority: P2
- Depends on: LUNA-18A, LUNA-18B

Scope:

- Add a debug/test trace listener that captures WPF binding errors during representative window construction and interactions.
- Fix errors in small surface-based sub-packets; do not suppress the trace globally.

Acceptance:

- The smoke path covers the main forecast, ledger, schedule, monthly report, and saved-month surfaces.
- Unexpected binding errors fail the harness with the binding path and surface name.
- Known framework noise, if any, is narrowly allow-listed with rationale.

#### LUNA-19A - Shared right-click grid panning behaviour

- Priority: P2
- Depends on: LUNA-18A

Scope:

- Characterize panning in MainWindow, CostCenterMappingWindow, and TaskCategoryEditorWindow.
- Extract one attached behaviour or shared grid component when intended semantics match.
- Preserve context-menu threshold, capture/release, escape/cancel, row resizing, and selection behaviour.

Acceptance:

- One canonical implementation serves the applicable grids.
- Right-click without drag still opens the context menu; a drag pans and suppresses accidental menu actions.
- Tests cover capture loss, bounds, and disabled scroll directions.

#### LUNA-19B - Shared report-canvas interaction and colour utilities

- Priority: P2
- Depends on: LUNA-18B

Scope:

- Consolidate duplicated canvas object selection/drag/position logic behind a shared controller or base component.
- Consolidate hex parsing, normalization, labels, and default-gradient creation where semantics match.
- Do not combine domain colour values with WPF brush construction.

Acceptance:

- Charts and canvas objects preserve selection, drag constraints, z-order, and saved position.
- One canonical pure colour parser/normalizer is tested; WPF brush creation remains a presentation concern.

#### LUNA-19C - Canonical forecast-curve math

- Priority: P1/P2
- Depends on: LUNA-14

Scope:

- Compare `ForecastCurveService`, `ForecastCurveWindow`, and `ForecastCurveMath` outputs over representative and boundary inputs.
- Select a pure canonical curve implementation and make UI preview and committed forecasts call it.
- Keep display scaling separate from financial allocation/rounding.

Acceptance:

- Preview and applied forecast produce the same normalized distribution.
- Zero duration, one period, negative/invalid inputs, rounding residuals, and total-preservation cases are tested.
- Duplicate business formulas are removed, not merely renamed.

#### LUNA-20 - Realistic performance baseline and evidence-led fixes

- Priority: P2, elevate failed user targets to P1
- Depends on: LUNA-17, LUNA-18C

Scope:

- Define realistic small, normal, and stress datasets using anonymised or synthetic data; record sizes before choosing targets.
- Measure startup/load, save, import, full recalculation, common grid edit refresh, workspace switch, schedule calculation, and memory after repeated open/close.
- Profile and fix only the largest reproducible bottlenecks, splitting each hotspot into a sub-packet if needed.

Acceptance:

- Baseline and post-change measurements use the same hardware/configuration and are stored as artifacts, not vague claims.
- Correctness and UI-state tests remain green.
- No key scenario regresses by more than the agreed tolerance without an explicit rationale.
- Release thresholds are documented and enforced where deterministic enough for CI.

### Phase 4 - dependencies, repository hygiene, and release truth

#### LUNA-21 - Deterministic and audited dependencies

- Priority: P2
- Depends on: LUNA-11

Scope:

- Enable NuGet lock files and locked restore in verification/CI.
- Update ClosedXML from 0.105.0 to the compatible 0.105.1 patch after import/export regression tests.
- Add direct and transitive vulnerability audit, package inventory, and licence review to the release checklist.
- Do not force major transitive upgrades independently of their parent package.

Acceptance:

- Clean locked restore resolves the committed versions deterministically.
- Import/export and workbook boundary tests pass after the patch update.
- Vulnerability audit is green or every exception has owner, rationale, mitigation, and expiry.

#### LUNA-22 - CI, release artifacts, and bundled-data hygiene

- Priority: P1
- Depends on: LUNA-11, LUNA-20, LUNA-21

Scope:

- Add a least-privilege Windows GitHub Actions workflow that invokes the same verification script as local development.
- Review every bundled JSON/XLSX file for necessity, provenance, and anonymisation; document the decision without exposing sensitive content.
- With explicit user approval, stop tracking generated release binaries/PDBs and `Temp` artifacts; preserve required releases through GitHub release artifacts or another approved channel.
- Enforce ignore rules and define symbols/debug-file policy.
- Add source and Git-history secret scanning that reports locations/types without printing secret values.

Acceptance:

- A clean GitHub runner performs locked restore, Release build, all discovered tests, relevant smoke/benchmark gates, and dependency audit.
- Generated release outputs do not return in a normal build.
- No unapproved sample or personal/project data ships in the application package.
- Repository cleanup is a dedicated, reviewable diff and was explicitly approved.

#### LUNA-23 - Release, recovery, and architecture documentation

- Priority: P2
- Depends on: all implementation packets

Scope:

- Reconcile README, release notes, architecture, state model, file-format support, and actual behaviour.
- Add a release checklist covering versioning, clean build, tests, package audit, signing/installer status, bundled-data review, upgrade fixture, backup/restore drill, and rollback.
- Record explicit negative constraints: no server/auth/database claims, no silent file migration, no destructive restore default, and no logging of financial row data.
- Document deferred issues with owner and reason rather than presenting them as complete.

Acceptance:

- Every release claim has an executable test, manual check, or clearly marked limitation.
- The close-warning contradiction is gone.
- A new maintainer can find architecture, verification, recovery, and release instructions from README.

#### LUNA-24 - Luna closure and evidence handoff

- Priority: final Luna gate
- Depends on: all applicable Luna packets

Scope:

- Run the full verification script from a clean checkout and clean build output.
- Complete the finding-to-packet evidence matrix.
- List every deferred or accepted risk with severity, owner, reason, and expiry/review date.
- Do not hide a failed gate with a documentation-only waiver.
- Produce `docs/audit/LUNA_HANDOFF.md` for Sol Ultra.

Acceptance:

- Every F-00 through F-20 row is marked fixed, disproved with evidence, not applicable with rationale, or explicitly accepted by the user.
- No unresolved P0 exists; no P1 is silently deferred.
- CI and local gates reference the same commands and both pass at the recorded commit.
- The working tree contains only the intended reviewed changes.

## 7. Dependency and sequencing summary

```text
LUNA-00 -> LUNA-01 -> LUNA-02
                         |
                         +-> LUNA-03 -> LUNA-04
                         +-> LUNA-05 -> LUNA-06A -> LUNA-07
                                  |  -> LUNA-06B
                         +-> LUNA-08
LUNA-02 + 06A + 08 ------+-> LUNA-09
LUNA-05 --------------------> LUNA-10
LUNA-03..10 ----------------> LUNA-11
LUNA-05 + 11 ---------------> LUNA-12 -> LUNA-13 -> LUNA-14
LUNA-14 + safety packets ----> LUNA-15A -> LUNA-15B
LUNA-12 + 15B ---------------> LUNA-16A -> LUNA-16B -> LUNA-17
LUNA-17 ---------------------> LUNA-18A -> LUNA-18B -> LUNA-18C
LUNA-18A/B/C + LUNA-14 ------> LUNA-19A/B/C
LUNA-17 + 18C ---------------> LUNA-20
LUNA-11 ---------------------> LUNA-21
LUNA-20 + 21 ---------------> LUNA-22 -> LUNA-23 -> LUNA-24
LUNA-24 ---------------------> SOL-00
```

P0 packets may be expedited, but their tests and dependencies may not be skipped. Architecture and performance packets must not begin on top of unresolved data-safety failures.

## 8. Sol Ultra independent final audit

### SOL-00 - Final acceptance, not implementation by assumption

Sol Ultra receives a fresh context containing:

- the source guide;
- this plan;
- `docs/audit/BASELINE.md`;
- the complete Luna packet ledger;
- `docs/audit/LUNA_HANDOFF.md`; and
- the final candidate commit.

Sol must not treat Luna's completion claims as evidence. Its first pass is read-only except for disposable build/test artifacts and its audit report.

#### Sol verification procedure

1. Confirm the candidate derives from the recorded baseline and inspect the complete diff, commit sequence, and repository status.
2. Rebuild from a clean checkout/clean output using locked restore.
3. Run the same local verification command and compare it with the GitHub Actions workflow.
4. In a disposable clone or worktree, run all discovered tests and confirm a deliberate temporary failure is detected by both local and CI entry points; discard only that disposable copy afterward.
5. Repeat package vulnerability, dependency inventory, licence, secret, tracked-binary, and bundled-data checks.
6. Review each F-00 through F-20 item against source and tests, including items Luna marked disproved or not applicable.
7. Exercise the critical failure paths with disposable files:
   - dirty close: Save, Discard, Cancel, and failed Save;
   - New Month with forced persistence failure and repeated invocation;
   - old, current, null, malformed, and future-version project files;
   - external same-file modification conflict;
   - corrupt preferences and unavailable diagnostics directory;
   - valid, duplicate, malformed, oversized, and formula-bearing CSV/workbook input;
   - backup verification and clean restore.
8. Run a WPF smoke pass covering main forecast, ledger, reports, schedule, saved months, context menus, panning, curve preview/apply, window close/reopen, and binding diagnostics.
9. Re-run realistic performance scenarios and compare them with the recorded baseline and thresholds.
10. Verify release documentation, package contents, version, signing/installer status, recovery steps, and rollback instructions agree with the artifact under review.

#### Sol acceptance rule

Sol may approve only when:

- there are no unresolved P0 findings;
- every P1 is fixed or explicitly accepted by the user with a concrete mitigation and review date;
- all required automated and manual gates pass;
- no unrelated broad rewrite or unreviewed generated artifact is present;
- old supported project files remain readable and current files restore successfully; and
- the final report links each conclusion to code, test output, or a reproducible manual check.

If Sol finds a defect, it records `SOL-FAIL-n` with severity, reproduction, expected/actual result, affected packet, and required acceptance test. The fix returns to a new narrowly scoped Luna packet. Sol then repeats the affected checks and the final full gate. Sol should not silently repair broad failures during the supposedly independent acceptance pass.

## 9. Status ledger

The executing agent updates this table sequentially. "Complete" requires the packet acceptance criteria and evidence note, not just an implementation claim.

| Packet | Status | Evidence link / note |
|---|---|---|
| LUNA-00 | Complete | `docs/audit/BASELINE.md` and `docs/audit/PACKET_LEDGER.md`; checkpoint `4f1fc24` pushed |
| LUNA-01 | Complete | `scripts/verify.ps1`; default, `-NoRestore`, path-with-spaces, and failure-propagation checks pass |
| LUNA-02 | Complete | Discovered test project passes 4 persistence/calculation tests; legacy harness remains mandatory |
| LUNA-03 | Complete | `CloseDecisionPolicy`, wired `MainWindow.OnClosing`, and 5 focused safe-close tests; verifier passes with 9 discovered tests plus the legacy harness |
| LUNA-04 | Complete | `NewMonthOperation` stages rollover on a cloned dataset; cancellation, failure, success, duplicate, and re-entrant tests pass; verifier passes with 14 discovered tests plus the legacy harness |
| LUNA-05 | Complete | `ProjectDatasetMigrationPipeline` adds format 1, migrates unversioned files, rejects null/malformed/future files, and passes the verifier with 20 discovered tests plus the legacy harness |
| LUNA-06A | Complete | Validation severity/codes, blocking save/load/month/import boundaries, post-migration session guard, and 8 focused boundary tests; Release verification passes with 28 discovered tests plus the legacy harness |
| LUNA-06B | Complete | Content-hash revision tokens, atomic stale-write rejection, injectable Reload / Save As / Cancel decisions, and 3 focused conflict tests; Release verification passes with 31 discovered tests plus the legacy harness |
| LUNA-07 | Complete | Verified backup creation, two-copy minimum / ten-copy default retention, safe new-path or pre-restore overwrite recovery, corruption protection, and a user recovery runbook; Release verification passes with 35 discovered tests plus the legacy harness |
| LUNA-08 | Not started | |
| LUNA-09 | Not started | |
| LUNA-10 | Not started | |
| LUNA-11 | Not started | |
| LUNA-12 | Not started | |
| LUNA-13 | Not started | |
| LUNA-14 | Not started | |
| LUNA-15A | Not started | |
| LUNA-15B | Not started | |
| LUNA-16A | Not started | |
| LUNA-16B | Not started | |
| LUNA-17 | Not started | |
| LUNA-18A | Not started | |
| LUNA-18B | Not started | |
| LUNA-18C | Not started | |
| LUNA-19A | Not started | |
| LUNA-19B | Not started | |
| LUNA-19C | Not started | |
| LUNA-20 | Not started | |
| LUNA-21 | Not started | |
| LUNA-22 | Not started | Requires explicit approval before removing tracked artifacts |
| LUNA-23 | Not started | |
| LUNA-24 | Not started | |
| SOL-00 | Not started | Independent final audit |
