# LUNA-25 GitHub Actions performance baseline remediation

Recorded: 2026-08-29

Status: remediation packet prepared for independent Sol review; this document
does not claim final SOL acceptance.

## SOL-FAIL-1

- Candidate: `7505c80ea491a075cd287ee9d57e2d89b696802f`
- GitHub Actions run/job: `33243036341` / `99075529370`
- Source report:
  `artifacts/sol00-ci-evidence-9711966844/performance-run.json`
- Source report SHA-256:
  `6744238A153A2D14359EC8F2EF1403D29C930BBB0B052650ACE0A12403AE33CE`

Release verification, dependency audit, secret scanning, and all 21 performance
correctness scenarios passed in the GitHub job. Baseline enforcement then
failed because the workflow compared the 4-core GitHub Actions Windows result
with the local/developer baseline captured on a different 16-core machine.

## Reproduction and observed result

The failing workflow command used the local/developer baseline:

```powershell
.\scripts\verify-performance.ps1 -OutputPath 'artifacts/luna22/performance-run.json' -BaselinePath 'docs/audit/LUNA-20-PERFORMANCE-BASELINE.json' -EnforceRegression
```

The three reported failures were:

| Scenario | CI median | Local-baseline limit |
|---|---:|---:|
| `small/project-save` | 68.656 ms | 65.316 ms |
| `normal/project-save` | 1,177.630 ms | 292.518 ms |
| `stress/project-save` | 6,790.275 ms | 1,487.748 ms |

Expected: the GitHub workflow compares measurements with a baseline captured
for the same GitHub Actions Windows profile, while local/developer runs retain
their existing local baseline.

Actual: the workflow used the 16-core local baseline for a report whose runtime
evidence records `.NET 8.0.30`, `win-x64`, Windows `10.0.26100`, X64, 4 logical
processors, Release configuration, 3 iterations, and 5 memory cycles. Sol's
isolated local run passed all 21 scenarios against the existing local baseline,
which reproduces the cross-machine baseline mismatch rather than an application
regression on that local profile.

## Root cause and bounded fix

Performance timings are profile-sensitive, particularly the project-save
workloads. The workflow supplied a baseline captured on different hardware even
though the generated report already recorded the runner profile. The regression
algorithm and tolerance were behaving as implemented; the baseline selection
was wrong.

`LUNA-20-PERFORMANCE-CI-BASELINE.json` is derived directly from the uploaded CI
report named above. Every captured timestamp, commit, runtime value, workload,
dataset size, sample, timing statistic, refresh diagnostic, and memory value is
preserved. The sole metadata relabel is top-level `Mode`, from `post-change` to
`ci-baseline`, so the file identifies itself as the GitHub Actions Windows
baseline without inventing measurements.

The workflow performance step now supplies that CI baseline explicitly. The
existing `LUNA-20-PERFORMANCE-BASELINE.json` remains the local/developer
baseline, and the default value and comparison behavior in
`scripts/verify-performance.ps1` are unchanged.

## Acceptance commands

Focused contract:

```powershell
dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Luna20PerformanceContractTests" /p:BaseOutputPath='artifacts/luna25-focused/'
```

Full repository verification:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -RunLegacySmoke
```

Local structural/correctness exercise of the CI baseline:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify-performance.ps1' -OutputPath 'artifacts/luna25/performance-local-against-ci.json' -BaselinePath 'docs/audit/LUNA-20-PERFORMANCE-CI-BASELINE.json' -EnforceRegression
```

That last command proves that the CI baseline parses, contains every scenario,
and participates in the unchanged comparison path. Its timing pass or failure
on different local hardware is not authoritative for the GitHub Actions
profile. The authoritative timing acceptance is a rerun on the intended GitHub
Actions Windows runner profile.

## Packet verification results

- A normalized JSON comparison reports
  `SEMANTIC_EQUAL_AFTER_MODE_RELABEL=True`; the CI baseline records mode
  `ci-baseline`, commit `7505c80ea491a075cd287ee9d57e2d89b696802f`,
  `win-x64`, 4 processors, 3 datasets, and 21 scenarios.
- The focused `Luna20PerformanceContractTests` command passes 2 tests with 0
  failures.
- The full verifier completed locked restore and a Release build with 0
  warnings/errors, then ran 186 discovered tests: 184 passed and 2 failed in
  pre-existing parent-owned closure-document assertions outside this packet's
  write set. It exited 1 before the retained legacy smoke stage, so the full
  verifier did not complete.
- The local CI-baseline exercise built with 0 warnings/errors, generated all 21
  scenarios, and passed every workload correctness check. Timing enforcement
  then exited 1 at `normal/startup-view-model`: local median 621.01 ms exceeded
  the CI baseline 479.836 ms plus 119.959 ms tolerance (599.795 ms limit).
  This different-machine timing failure is not authoritative for the GitHub
  runner profile.

## CI baseline recapture policy

1. Recapture this baseline only from an attached GitHub Actions Windows report
   for the intended workflow profile, not from a developer machine.
2. Recapture only after an approved performance change or a material runner
   image/runtime/processor-profile change; do not replace the baseline merely
   to make one regression failure pass.
3. Require all 3 deterministic datasets and all 21 correctness scenarios to
   complete, preserving the report's raw samples and runtime/workload metadata.
4. Use repeated comparable CI runs to investigate runner variance before
   accepting materially shifted timing values.
5. Record the source run, job, candidate commit, artifact path, and SHA-256 in
   the review. Relabel only `Mode` to `ci-baseline`; do not alter measurements.
6. Keep the local/developer baseline separate and continue using it through the
   verifier script's unchanged default `BaselinePath`.

Final acceptance remains an independent SOL-00 responsibility.
