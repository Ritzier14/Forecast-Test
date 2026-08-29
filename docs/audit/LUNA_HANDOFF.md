# Luna handoff to Sol Ultra

Handoff state: prepared, final acceptance pending the F-13 approval decision

Candidate before this handoff: `3dcd291` (`docs(audit): complete LUNA-23 release truth`)

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

The working tree was clean at the candidate boundary. A fresh isolated
Release build was produced under `artifacts/luna24-clean-build-final/`; it completed
with zero warnings and errors, and the final isolated UnitTest assembly passes
all 184 discovered tests. The authoritative local command is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -RunLegacySmoke
```

The final LUNA-23 run passed with a Release build at zero warnings/errors, 182
discovered tests, and all 428 retained smoke assertions; the two LUNA-24
evidence tests bring the current total to 184. LUNA-22 evidence
also records the locked restore, 22-package zero-vulnerability audit,
zero-finding source/history secret scan, and normal-package data check. The
Windows workflow in `.github/workflows/verify.yml` invokes the same local
verification command before its additional supply-chain, secret, and
performance gates.

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
8. stop final approval if F-13 has not been either explicitly approved and
   completed or explicitly accepted by the user with a concrete mitigation and
   review date.

## Deferred and accepted-risk register

| Risk | Severity | Owner | Reason / mitigation | Review or expiry |
|---|---|---|---|---|
| Tracked generated release and `Temp` artifacts remain | P1 | User / release owner | No destructive index/file removal was performed without explicit approval. Future paths are ignored; normal package content is filtered; exact scope is 16 release files plus one `Temp` file. | Must resolve before Sol final approval or formal release |
| No signed installer/MSIX or deployment rollback channel | P2 | Release/build owner | The repository produces a WPF build, not an installer. Verified backups and a prior approved artifact are the manual rollback path. | Before formal production release |
| Direct original-workbook `.xlsm` project importer is not implemented | P2 | Application owner | The supported boundary is the anonymised startup fixture plus raw transaction CSV/XLSX/XLSM import. | Product decision before replacing the extracted fixture |
| Release-data distribution approval is pending | P1 | Release-data owner | Hash/provenance review is recorded without exposing values; retain only approved anonymised data in a release package. | Before distribution or formal release |
| Broader user acceptance is pending | P2 | Product acceptance owner | Existing workbook-derived and synthetic gates are not a substitute for additional real projects. | Before formal production release |
| NuGet advisory results are point-in-time | P2 | Release/build owner | Locked graph and zero finding inventory are recorded; rerun the advisory query for each release. | Every release candidate |

The F-13 and release-data rows are visible blockers to final Sol acceptance,
not hidden waivers. The branch is otherwise ready for the independent audit.
