# Luna handoff to Sol Ultra

Handoff state: prepared for independent SOL-00 review; F-13 cleanup is complete
and final acceptance remains pending the parent Sol audit

Pre-cleanup candidate baseline: `85f3b03` (`docs(audit): prepare LUNA-24 Sol handoff`)

This handoff is an evidence index, not a completion claim. Sol Ultra must
independently inspect the source, commit history, and gates. Luna's packet
claims are not substitutes for Sol's acceptance procedure.

## Required reading

1. `docs/CODEX_START_HERE.md`
2. `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`
3. `docs/audit/BASELINE.md`
4. `docs/audit/PACKET_LEDGER.md`
5. `docs/audit/LUNA-23-RELEASE-TRUTH.md`
6. `docs/audit/LUNA-24-FINDING-MATRIX.md`
7. `docs/RELEASE_CHECKLIST.md`
8. `docs/RECOVERY_RUNBOOK.md` and `docs/DIAGNOSTICS_RUNBOOK.md`

## Candidate evidence

The working tree was clean at the pre-cleanup candidate boundary. A fresh isolated
Release build was produced under `artifacts/luna24-clean-build-final/`; it completed
with zero warnings and errors, and the pre-cleanup isolated UnitTest assembly
passed all 184 discovered tests. The authoritative local command is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -RunLegacySmoke
```

The final LUNA-23 run passed with a Release build at zero warnings/errors, 182
discovered tests, and all 428 retained smoke assertions; the two LUNA-24
evidence tests brought the pre-cleanup total to 184. The F-13 packet's current
full verifier passes with a Release build at zero warnings/errors, 185
discovered tests, and all 428 retained smoke assertions. LUNA-22 evidence also
records the locked restore, 22-package zero-vulnerability audit, zero-finding
source/history secret scan, and normal-package data check. The Windows workflow
in `.github/workflows/verify.yml` invokes the same local verification command
before its additional supply-chain, secret, and performance gates.

## F-13 cleanup packet

The user explicitly delegated the F-13 decision and final closure to Sol on
2026-08-29, and Sol selected the cleanup option. The packet removes exactly 16 files under
`release/ProjectCostForecast/` plus `Temp/data_anonymised.xlsx` from Git
tracking with `git rm --cached`. All 17 local copies remain on disk, the
existing ignore rules match them, and the three source data fixtures remain
tracked. The exact paths, recovery from `85f3b03`/source inputs, and evidence
are recorded in [`F-13-CLEANUP.md`](F-13-CLEANUP.md).

The parent Sol agent owns the independent verification, cleanup commit, push,
and SOL-00 decision. This handoff does not claim final acceptance.

## Sol verification obligations

Sol Ultra must:

1. inspect the complete diff and commit sequence from the baseline;
2. use a clean checkout and clean output directory for locked restore, Release
   build, discovered tests, and the retained smoke command;
3. compare the local command with `.github/workflows/verify.yml`;
4. rerun package, license, vulnerability, secret, tracked-binary, and bundled-
   data checks;
5. exercise dirty-close, New Month failure, migration, stale-write, import,
   backup/restore, diagnostics, binding, interaction, and performance paths;
6. review every row in `LUNA-24-FINDING-MATRIX.md` against source and tests;
7. verify that no generated output or scan report is committed; and
8. verify that the F-13 cleanup packet covers exactly the authorized 17 paths,
   preserves local copies, leaves source fixtures tracked, and is represented
   by a dedicated cleanup commit before deciding final acceptance.

## Deferred and accepted-risk register

| Risk | Severity | Owner | Reason / mitigation | Review or expiry |
|---|---|---|---|---|
| No signed installer/MSIX or deployment rollback channel | P2 | Release/build owner | The repository produces a WPF build, not an installer. Verified backups and a prior approved artifact are the manual rollback path. | Before formal production release |
| Direct original-workbook `.xlsm` project importer is not implemented | P2 | Application owner | The supported boundary is the anonymised startup fixture plus raw transaction CSV/XLSX/XLSM import. | Product decision before replacing the extracted fixture |
| Release-data distribution approval is pending | P1 | Release-data owner | Hash/provenance review is recorded without exposing values; retain only approved anonymised data in a release package. | Before distribution or formal release |
| Broader user acceptance is pending | P2 | Product acceptance owner | Existing workbook-derived and synthetic gates are not a substitute for additional real projects. | Before formal production release |
| NuGet advisory results are point-in-time | P2 | Release/build owner | Locked graph and zero finding inventory are recorded; rerun the advisory query for each release. | Every release candidate |

The release-data row remains a visible formal-release gate, not a hidden waiver;
F-13 itself is resolved by the dedicated cleanup packet. The branch is
prepared for the independent SOL-00 audit, which remains not started here.
