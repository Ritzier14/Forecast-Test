# SOL-00 independent final audit

Recorded: 2026-08-29

Audited candidate: `2c6cd67ff16f12a7f2703099ffb113645d0c038b`
(`perf(audit): add GitHub Windows CI baseline`)

Remediation chain: F-13 cleanup commit `7505c80`; SOL-FAIL-1 remediation
commit `2c6cd67`.

Decision: **accepted for codebase-audit closure**

This is not approval for formal production distribution. Signing/installer,
release-data distribution, deployment rollback, and broader user acceptance
remain release gates described below.

## Acceptance decision

- The candidate derives from checkpoint `4f1fc24`; the checkpoint is an
  ancestor and the reviewed sequence contains 33 commits including that
  checkpoint, the dedicated F-13 cleanup, and LUNA-25 remediation.
- All 21 finding rows, F-00 through F-20, are present. No P0 or P1 finding row
  remains unresolved; the separate release-data distribution P1 is explicitly
  accepted only under the mitigation and review date below.
- F-13 is fixed in dedicated commit `7505c80`: exactly 16 legacy release paths
  and one `Temp` path are absent from `git ls-files`, all 17 match ignore rules,
  and all three source fixtures remain tracked.
- SOL-FAIL-1 exposed a P1 CI-gate defect: GitHub Actions used a performance
  baseline captured on a different machine profile. LUNA-25 preserved the
  uploaded measurements, separated CI and local baseline selection, and passed
  the corrected GitHub gate in commit `2c6cd67`.
- The remaining release-data distribution P1 is accepted for this codebase
  audit only under the user's explicit delegation of final closure to Sol. The
  mitigation is a hard prohibition on formal distribution until release-data
  owner approval. Owner: user / release-data owner. Review: 2026-11-29 or
  before any distribution, whichever occurs first.

## Independent evidence

| Area | Independent result |
|---|---|
| History and scope | Baseline ancestry passed; complete diff and 33-commit sequence reviewed; disposable clone was clean before and after verification |
| Local/CI parity | `.github/workflows/verify.yml` invokes `scripts/verify.ps1 -RunLegacySmoke`, then the same dependency and secret gates; performance uses the same verifier with a GitHub-Windows baseline while the unchanged default remains the local/developer baseline |
| Clean verification | Exact candidate `2c6cd67` passed locked restore; Release build passed with 0 warnings and 0 errors; 186/186 discovered tests and all 428 retained smoke assertions passed |
| Failure propagation | A disposable failing xUnit fact made both the default verifier and the exact CI-mode verifier exit 1; the fact was removed and the full clean gate returned to green |
| Critical failure paths | 105 focused tests passed across dirty close, New Month, migration/version fixtures, stale writes, diagnostics, import boundaries, backup/restore, file/import workflows, lifecycle/async ownership, binding, panning, report canvas, and curve preview/application |
| WPF smoke | The focused run included the STA MainWindow path for forecast, resources/ledger, schedule, monthly report, saved month, return/current month, and close, plus context-menu lifecycle, panning, canvas, curve, and binding-diagnostic contracts |
| Dependencies | 22 locked direct/transitive packages inventoried; zero NuGet vulnerability records; committed license review remains current |
| Secrets | Redacted scan covered 411 tracked files and 60 reachable commits; zero findings |
| Repository hygiene | Zero tracked release/`Temp` cleanup paths and zero tracked generated DLL/EXE/PDB/runtime/deps files; normal build did not recreate release/`Temp` paths |
| Bundled data | Built package contains only `Data/data_anonymised.xlsx`; non-value inspection found no email, phone, credential, macro, or external-link indicators in that packaged fixture; archived `.xlsm` is not packaged |
| Performance | The isolated local/developer gate passed all 21 scenarios; SOL-FAIL-1 identified the invalid cross-machine CI comparison, and LUNA-25's measurement-faithful GitHub-Windows baseline passed all 21 scenarios under the unchanged tolerance policy |
| Release truth | Built executable reports product `1.0.1` / file `1.0.1.0`; it is unsigned; no installer is tracked; recovery and manual rollback limitations match the documentation |
| GitHub Actions | Initial run [`33243036341`](https://github.com/Ritzier14/Forecast-Test/actions/runs/33243036341/job/99075529370) exposed SOL-FAIL-1; corrected run [`33243769987`](https://github.com/Ritzier14/Forecast-Test/actions/runs/33243769987/job/99077522610) passed Release verification/smoke, dependency audit, secret scan, performance regression, and evidence upload |

## Reproduced commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -RunLegacySmoke
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\audit-dependencies.ps1 -NoRestore -FailOnVulnerability -OutputPath artifacts/sol00/dependency-audit.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\scan-secrets.ps1 -OutputPath artifacts/sol00/secret-scan.json -FailOnMatch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-performance.ps1 -OutputPath artifacts/sol00/performance-isolated.json -BaselinePath docs/audit/LUNA-20-PERFORMANCE-BASELINE.json -EnforceRegression
```

The first restore attempt was correctly blocked by sandbox TLS policy and was
repeated with approved network access. An elevated clean-checkout run then met
Git's Windows dubious-ownership guard because restore and tests used different
sandbox identities; the same checkout passed as its owning identity. A
performance run started concurrently with dependency and secret scanning
missed one median limit by 6.13 ms; the required isolated rerun passed. These
were environmental/methodological observations, not product defects, and none
was used to waive a gate.

The LUNA-25 worker's full-worktree verifier also encountered two assertions in
parent-owned draft closure documents outside its four-file write scope. The
focused remediation contracts passed, and the exact committed candidate from a
disposable clone subsequently passed all 186 tests and the retained smoke gate.

## SOL-FAIL-1 disposition

- Severity: P1 final-gate infrastructure defect.
- Reproduction: GitHub Actions run `33243036341`, job `99075529370`.
- Expected: compare CI measurements with a baseline captured for the same
  GitHub Actions Windows profile.
- Actual: the workflow compared a 4-core `win-x64` report with a local baseline
  captured on a 16-core developer machine; three project-save medians failed
  although every workload correctness assertion passed.
- Affected packet: LUNA-20 performance gate as wired by LUNA-22 CI.
- Remediation: [`LUNA-25-CI-PERFORMANCE.md`](LUNA-25-CI-PERFORMANCE.md) and
  commit `2c6cd67`; the source report is preserved exactly apart from relabeling
  top-level `Mode` to `ci-baseline`, local defaults remain unchanged, and both
  profiles are contract-tested.
- Acceptance: clean local verification passed, and corrected GitHub Actions run
  `33243769987`, job `99077522610`, completed successfully on 2026-08-29.

## Residual release risks

| Risk | Severity / disposition | Owner | Mitigation | Review / expiry |
|---|---|---|---|---|
| Release-data distribution is not owner-approved | P1, accepted for audit closure only | User / release-data owner | Do not formally distribute; normal package is limited to the reviewed anonymised fixture | 2026-11-29 or before any distribution |
| No signed installer/MSIX or deployment rollback channel | P2, deferred | Release/build owner | Keep the unsigned build internal; use verified project backups and a prior approved artifact | Before formal production release |
| Direct original-workbook project importer is absent | P2, deferred | Application owner | Continue using the extracted startup fixture and supported raw transaction imports | Before replacing the fixture |
| Broader project/user acceptance is pending | P2, deferred | Product acceptance owner | Exercise additional real projects and record findings | Before formal production release |
| NuGet advisory results are point-in-time | P2, recurring | Release/build owner | Repeat locked restore and advisory audit | Every release candidate |

## Final verdict

SOL-00 accepts candidate `2c6cd67` for completion of the codebase audit. The
Luna sequence, F-13 cleanup, recorded SOL-FAIL-1 remediation, local gates,
independent negative control, and successful GitHub workflow satisfy the audit
acceptance rule. Formal production release remains prohibited until its
separately listed gates are completed.
