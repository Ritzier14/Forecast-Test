# Post-SOL architecture review and remediation plan

Date: 2026-08-29

Review baseline: `0fc49ca` (`docs(audit): complete SOL-00 acceptance`)

Execution model: one Luna Max packet at a time, followed by an independent Sol Ultra review

## Review outcome

The post-SOL pass re-read the complete LUNA-00 through LUNA-25 packet ledger,
the SOL-00 report, the architecture and state contracts, the production project,
the discovered tests, and the retained smoke harness. The accepted SOL-00
closure remains valid: no new P0 or P1 defect was found, and the formal-release
limitations recorded by SOL-00 are unchanged.

The current branch is clean at the review baseline. The discovered suite passes
186 tests, the retained compatibility harness passes all 428 assertions, and a
focused architecture/lifecycle/workflow pass covers 50 tests. The source remains
an incremental modular monolith: one WPF production assembly contains 58,410
lines, including 18,551 lines across `MainWindow` partials/XAML and 11,046 lines
across `MainWindowViewModel` partials.

Three bounded P2 architecture gaps require remediation. They refine prior packet
claims; they do not reopen the accepted codebase-audit finding register.

## Findings

### ARCH-01 — model dependency direction is not closed

`Models/AuditEvent.cs` and `Models/ProjectDataset.cs` call
`Services.DateTimeContract`; `Models/ForecastLine.cs` calls
`Services.FiscalPeriod`. The LUNA-14 source gate rejects WPF/window tokens but
does not reject upward dependencies from model candidates into Services or
Presentation.

Required outcome: model files have no dependency on Services, Presentation,
ViewModels, or WPF. Display formatting moves to presentation projections or
bindings, period ordering uses a neutral model-safe value/boundary, and the
architecture test includes a deliberate negative control for every forbidden
layer.

### ARCH-02 — revision-aware save has a check/write race

`ProjectFileService.SaveWithRevision` captures and compares the destination
revision before calling `AtomicJsonFile.Write`. Another cooperating writer can
change the file between those operations. Existing tests prove sequential stale
write rejection, not an interleaved writer race. `IProjectFileService` also
exposes an unconditional `Save` escape hatch.

Required outcome: the expected-revision check and replacement execute inside a
single per-destination writer boundary shared by application service instances;
an injectable interleaving test proves that only one competing writer commits.
The application-facing interface must not expose unconditional overwrite as a
normal workflow operation. Atomic temp-file replacement, backup behavior, old
file compatibility, and typed conflict recovery must remain intact. Any limit
against non-cooperating external writers or cross-host filesystems must be stated
precisely rather than hidden by a broad compare-and-swap claim.

### ARCH-03 — deferred MainWindow work bypasses its lifetime owner

`QueueMainWindowWork` captures the MainWindow lifetime generation, but raw
dispatcher calls remain in schedule editing, grid builders, spreadsheet editing,
menus, filters, and related interaction paths. `CancelPendingWindowWork` cannot
abort or invalidate callbacks that bypass the helper, so queued work can target
an unloaded window or replaced data context.

Required outcome: all MainWindow-owned deferred callbacks are generation-guarded
or explicitly retained and aborted. Nested callbacks inherit the same lifetime;
unload/data-context replacement tests prove stale work is suppressed; and a
source contract rejects newly introduced raw MainWindow dispatcher scheduling
outside the lifecycle helper. If the change exceeds the normal packet-size
limit, Luna must split ARCH-03 into ordered sub-packets before editing.

## Ordered Luna Max packets

### LUNA-26A — model dependency closure

Scope:

- characterize the three current model-to-service references and their UI/sort
  consumers;
- remove the upward dependencies without changing serialized names or values;
- strengthen the model architecture gate with negative controls; and
- preserve current/legacy JSON round trips and NZ display behavior.

Acceptance:

- no source under the model candidate root references Services, Presentation,
  ViewModels, WPF, windows, controls, dialogs, or message boxes;
- audit and unmatched-import times still display under the documented NZ
  date/time contract;
- forecast comment ordering remains deterministic; and
- focused tests, full verification, retained smoke, and `git diff --check` pass.

Implementation status: complete on baseline `7c42c5f`.

Implementation evidence: `AuditEvent` and `UnmatchedImportCombination` retain
their persisted `DateTimeOffset` values while `DateTimeDisplayConverter`
formats them through `DateTimeContract` at the WPF boundary. Forecast comment
ordering and the existing public `Services.FiscalPeriod` parsing/sort API share
one canonical model-safe fiscal-period implementation, with the service facade
delegating downward. Runtime parity and malformed-label coverage plus a
service-delegation source contract prevent the two paths from diverging. The
architecture gate covers the full model candidate root, recognizes qualified
namespace references and using directives after removing comments/literals,
and has deliberate negative controls for Services, Presentation, ViewModels,
and WPF. The packet changed seven production files and one test file. The one
production-file variance above the six-file packet limit is the parent-requested
canonicalization correction in `Services/FiscalPeriod.cs`; no assembly split or
LUNA-26B work was included.

Focused evidence: `dotnet test
tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c
Release --no-restore --filter
"FullyQualifiedName~Luna26A|FullyQualifiedName~Luna14ArchitectureTests|FullyQualifiedName~DateTimeLocaleContractTests|FullyQualifiedName~StateModelCharacterizationTests"`
exited 0 with 46 tests passed. The required verifier exited 0 with a
zero-warning/zero-error Release build, 213 discovered tests passed, and all
428 retained smoke assertions passed.

### LUNA-26B — revision-safe persistence boundary

Depends on: LUNA-26A complete.

Scope:

- add a deterministic interleaving characterization test;
- serialize revision check plus commit per canonical destination across
  cooperating `ProjectFileService` instances;
- narrow unconditional save access at the application interface; and
- document the exact concurrency guarantee and residual filesystem boundary.

Acceptance:

- two writers starting from one revision cannot both commit;
- the losing writer receives `ProjectFileConflictException` with the actual
  revision and existing Reload / Save As / Cancel recovery remains intact;
- new-file save, backup/restore, migration, validation, and atomic replacement
  behavior remain green; and
- focused tests, full verification, retained smoke, and `git diff --check` pass.

### LUNA-26C — MainWindow deferred-work lifetime closure

Depends on: LUNA-26B complete.

Scope:

- inventory every MainWindow dispatcher schedule site;
- route it through the lifetime helper or retain and abort its operation;
- cover nested schedules, unload, close, and data-context replacement; and
- add a static architecture guard with one explicit helper-only allowance.

Acceptance:

- queued MainWindow work cannot mutate controls or an old view model after its
  lifetime generation changes;
- ordinary focus/edit/menu timing behavior remains characterized;
- no raw MainWindow dispatcher scheduling remains outside the owned helper;
- representative WPF binding diagnostics stay clean; and
- focused tests, full verification, retained smoke, performance gate, and
  `git diff --check` pass.

## Packet operating contract

Each Luna Max run handles exactly one packet, reads the baseline architecture
documents and prior evidence, inspects repository status, adds characterization
before risky edits, preserves unrelated work, and stops after recording the
packet in `docs/audit/PACKET_LEDGER.md`. A normal packet remains within the
existing six-production-file/six-test-file/roughly-600-net-production-line limit;
larger work must be split explicitly.

Every packet runs its focused tests and:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -NoRestore -RunLegacySmoke
git diff --check
```

The final packet also runs the deterministic performance regression gate. No
packet may weaken migration, release-data, dependency, secret-scan, performance,
or compatibility-harness controls to obtain a pass.

## SOL-01 — independent post-SOL architecture review

Sol Ultra receives the clean candidate only after LUNA-26A through LUNA-26C are
complete. Sol independently re-reads the prior claims intersected by ARCH-01
through ARCH-03, reviews the full diff and commit sequence, runs locked clean
verification and the retained smoke harness, exercises the interleaved writer
and stale-dispatch races, repeats the WPF binding and performance gates, and
checks that documentation states the actual guarantees.

If Sol finds a defect, it records `SOL-01-FAIL-n` and returns only the affected
scope to a new Luna Max packet. Sol does not silently repair its own audit
candidate.

## Assembly-split decision

The documented Core/Application/Infrastructure/App split remains worthwhile but
is not part of this corrective pass. The current model folder still mixes
persisted domain state with non-WPF presentation rows, and the view model retains
WPF timers, collection views, commands, and shell prompts. Moving those files now
would create a broad mechanical diff without first resolving their ownership.

After SOL-01, a separate architecture initiative may be approved to classify the
model candidates, create a non-Windows Core project, move composition defaults to
the App root, and then extract Application and Infrastructure one bounded feature
at a time. Completion of ARCH-01 through ARCH-03 is not contingent on claiming
that larger migration is already complete.

## Status

| Packet | Status | Evidence |
|---|---|---|
| LUNA-26A | Complete | Model dependency closure with one canonical fiscal parser; 46 focused tests, 213 discovered tests, and 428 retained smoke assertions pass |
| LUNA-26B | Pending | Revision-safe persistence boundary |
| LUNA-26C | Pending | MainWindow deferred-work lifetime closure |
| SOL-01 | Pending | Independent post-SOL architecture review |
