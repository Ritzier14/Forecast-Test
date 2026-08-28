# Luna packet ledger

This ledger records one packet at a time. Existing work before the checkpoint is treated as baseline evidence, not as audit implementation.

## LUNA-00

Packet: LUNA-00 - Baseline and recoverable checkpoint
Status: complete
Baseline commit: `4f1fc24` (`chore(audit): checkpoint pre-audit working tree`)
Changed files: `docs/audit/BASELINE.md`, `docs/audit/PACKET_LEDGER.md`, plus the pre-existing worktree preserved by the checkpoint commit.
Characterization evidence: baseline restore, Release build, console acceptance, and diff-check commands recorded in `docs/audit/BASELINE.md`.
Commands and results: `git add -A`, checkpoint commit, and `git push origin alpha/1.13-1.22` all succeeded.
Manual checks: confirmed the branch and mixed worktree state before editing.
Residual risks / follow-up packet: the baseline commit includes broad pre-existing changes and is not an audit-quality feature diff.

## LUNA-01

Packet: LUNA-01 - One-command local verification
Status: complete
Baseline commit: `4f1fc24`
Changed files: `scripts/verify.ps1`, `README.md`, `docs/audit/PACKET_LEDGER.md`, and the LUNA status row in `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`.
Characterization evidence: the script invokes restore, Release build, and the console harness through one repository-rooted PowerShell entry point; its command runner throws on any non-zero native exit code.
Commands and results: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1'` exited 0 with restore, Release build, and the full console harness passing. The same command with `-NoRestore` exited 0. Invoking the script by its absolute path from `C:\Windows\Temp` with `-NoRestore` exited 0. Invoking it with `-NoRestore -DotnetCommand where.exe` made the child exit 2 and the script exit 1.
Manual checks: the repository path containing spaces was exercised; generated build output remained under ignored `bin/` and `obj/` directories; the failure path reported the child exit code.
Residual risks / follow-up packet: the existing console harness remains an executable rather than a discovered test project; that is LUNA-02 scope.

## LUNA-02

Packet: LUNA-02 - Discovered-test foundation
Status: complete
Baseline commit: `4f1fc24`
Changed files: `tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj`, `tests/ProjectCostForecast.UnitTests/PersistenceAndCalculationTests.cs`, `ProjectCostForecast.sln`, `scripts/verify.ps1`, `README.md`, `docs/audit/PACKET_LEDGER.md`, and the LUNA status row in `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`.
Characterization evidence: four discovered tests cover calculation totals, nested atomic project save/load, replacement of an existing project, and rapid distinct loadable backups. Each test owns a unique temporary directory and deletes it during disposal; no application/UI process is started. The legacy console harness remains a separate mandatory gate.
Commands and results: `dotnet restore ProjectCostForecast.sln` exited 0; `dotnet build ProjectCostForecast.sln -c Release --no-restore` exited 0 with 0 warnings and 0 errors; `dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Release --no-build --no-restore` discovered and passed 4 tests; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -NoRestore` exited 0 with both test gates passing; `git diff --check` exited 0.
Manual checks: the verifier was exercised from the repository path containing spaces. Test-only `Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, and `xunit.runner.visualstudio` 2.8.2 were added because conventional discovery requires a test SDK/framework/adapter; no production dependency was added. No STA mechanism was added because the ported tests are headless service/persistence tests and create no WPF windows.
Residual risks / follow-up packet: WPF-specific tests still need a narrowly scoped STA fixture if a later packet introduces them; broader characterization remains in the console harness until incrementally ported.

## LUNA-03

Packet: LUNA-03 - Safe close with unsaved changes
Status: complete
Baseline commit: `4f1fc24`
Changed files: `src/ProjectCostForecast.App/Services/CloseDecisionPolicy.cs`, `src/ProjectCostForecast.App/MainWindow.xaml.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.ProjectIO.cs`, `src/ProjectCostForecast.App/ReimplementationTodoWindow.cs`, `docs/RELEASE_NOTES.md`, `tests/ProjectCostForecast.UnitTests/SafeCloseTests.cs`, `docs/audit/PACKET_LEDGER.md`, and the LUNA status row in `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`.
Characterization evidence: clean close bypasses prompting; dirty close requires an explicit Save, Discard, or Cancel decision; Cancel preserves dirty state; Discard clears the pending dirty state without saving; Save clears dirty state only after persistence succeeds; and a forced persistence exception leaves the close rejected and dirty state intact. The close handler also guards against re-entrant close events.
Commands and results: `dotnet build ProjectCostForecast.sln -c Release --no-restore` exited 0 with 0 warnings and 0 errors; `dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Release --no-build --no-restore` exited 0 with 9 tests passed; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -NoRestore` exited 0 with the legacy harness and 9 discovered tests passing; `git diff --check` exited 0.
Manual checks: the window close path now maps Yes / No / Cancel to Save / Discard / Cancel, persists preferences only after close approval, and retains the existing Save command error dialog while avoiding a modal error dialog during headless close-policy tests. The TODO window and release notes now describe the implemented behavior.
Residual risks / follow-up packet: Save As remains a WPF file-picker path and is not automated in the headless suite; dialog and broader open/save boundary extraction remain LUNA-15A scope. Transactional New Month remains the next P0 packet, LUNA-04.

## LUNA-04

Packet: LUNA-04 - Transactional and idempotent New Month
Status: complete
Baseline commit: `2ff4158`
Changed files: `src/ProjectCostForecast.App/Services/ProjectDatasetCloner.cs`, `src/ProjectCostForecast.App/Services/NewMonthOperation.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModelDependencies.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.ProjectIO.cs`, `tests/ProjectCostForecast.UnitTests/NewMonthTests.cs`, `docs/audit/PACKET_LEDGER.md`, and the LUNA status row in `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`.
Characterization evidence: New Month now prepares a serialized clone, recalculates and adds the single baseline/audit set on the staged dataset, persists it, and reloads it into live state only after success. Cancelled and failed saves leave period, forecast values, snapshots, audit IDs, selected objects, and dirty state unchanged. A duplicate current-period baseline is rejected, and a re-entrant invocation during persistence is rejected by an interlocked guard.
Commands and results: `dotnet build ProjectCostForecast.sln -c Release --no-restore` exited 0 with 0 warnings and 0 errors; `dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Release --no-build --no-restore` exited 0 with 14 tests passed; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -NoRestore` exited 0 with the legacy harness and 14 discovered tests passing; `git diff --check` exited 0.
Manual checks: successful rollover advances `26-09` to `26-10`, creates one `26-09` snapshot, rolls prior-month values once, preserves the selected forecast line by identity key after commit, and clears dirty state only after staged persistence succeeds. The normal Save command still persists the live dataset and retains its existing Save As behavior.
Residual risks / follow-up packet: Save As remains a WPF file-picker path and is not automated in the headless suite; project format versioning and migration remain LUNA-05 scope.

## LUNA-05

Packet: LUNA-05 - Versioned project format and migration pipeline
Status: complete
Baseline commit: `5f6d508`
Changed files: `src/ProjectCostForecast.App/Models/ProjectDataset.cs`, `src/ProjectCostForecast.App/Services/ProjectDatasetMigrationPipeline.cs`, `src/ProjectCostForecast.App/Services/ProjectFileService.cs`, `src/ProjectCostForecast.App/Services/SampleDataService.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModelDependencies.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.Dataset.cs`, `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.Presentation.cs`, `tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj`, `tests/ProjectCostForecast.UnitTests/PersistenceAndCalculationTests.cs`, `tests/ProjectCostForecast.UnitTests/Fixtures/ProjectFiles/*.json`, `docs/audit/PACKET_LEDGER.md`, and the LUNA status row in `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`.
Characterization evidence: Project files now carry an explicit format version independent of the app version. Unversioned format 0 files are normalized and migrated to format 1; root and nested null lists, resource-comment defaults, period labels, canonical dates, and missing continuous periods are handled by the WPF-free pipeline. Top-level JSON null, malformed content, invalid version values, and unsupported future versions produce typed, actionable format errors before deserialization can replace the active session.
Commands and results: `dotnet build ProjectCostForecast.sln -c Release --no-restore` exited 0 with 0 warnings and 0 errors; focused persistence/migration tests exited 0 with 10 tests passed; `dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Release --no-build --no-restore` exited 0 with 20 tests passed; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -NoRestore` exited 0 with the legacy harness and 20 discovered tests passing; `git diff --check` exited 0.
Manual checks: legacy, current v1, null-root, malformed, and future-v99 fixtures were loaded through `ProjectFileService`; legacy output saved with `FormatVersion` 1; a second normalization reported no data changes; current fixture values and audit identity round-tripped unchanged. The existing open-project path loads and migrates before `LoadDataset`, so format errors are caught without replacing the active dataset.
Residual risks / follow-up packet: required-field, duplicate-identity, numeric-bound, and post-migration validation remain LUNA-06A; stale-write protection remains LUNA-06B; backup verification and restore remain LUNA-07.
