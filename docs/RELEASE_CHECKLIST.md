# Release checklist

This checklist records release gates that must be completed for the artifact
under review. LUNA-21 owns the dependency and supply-chain section, while
LUNA-22 owns the CI, bundled-data, and repository-hygiene controls. Packaging,
signing, and recovery remain later release packets.

## Dependency and supply-chain gate

- [ ] Run `dotnet restore ProjectCostForecast.sln --locked-mode` from the
  repository root.
- [ ] Run
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
  '.\scripts\audit-dependencies.ps1' -NoRestore -FailOnVulnerability
  -OutputPath 'docs/audit/LUNA-21-DEPENDENCY-AUDIT.json'` with access to the
  configured NuGet advisory source.
- [ ] Review the generated direct/transitive inventory and confirm that
  `VulnerabilityCount` is zero, or record owner, rationale, mitigation, and
  expiry for every approved exception.
- [ ] Review the package license metadata below and preserve the applicable
  notices in the release channel.

The current LUNA-21 inventory was captured on 2026-08-29 with .NET SDK
10.0.202. All packages use the package-declared license metadata from the
resolved `.nuspec` files in the local global-packages cache; the two packages
without an SPDX expression retain their declared license URL for manual
review.

| Package | Version | Use | License metadata |
|---|---:|---|---|
| ClosedXML | 0.105.1 | Production, direct | MIT |
| ClosedXML.Parser | 2.0.0 | Production, transitive | MIT |
| DocumentFormat.OpenXml | 3.1.1 | Production, transitive | MIT |
| DocumentFormat.OpenXml.Framework | 3.1.1 | Production, transitive | MIT |
| ExcelNumberFormat | 1.1.0 | Production, transitive | MIT |
| RBush.Signed | 4.0.0 | Production, transitive | MIT |
| SixLabors.Fonts | 1.0.0 | Production, transitive | Apache-2.0 |
| System.IO.Packaging | 8.0.1 | Production, transitive | MIT |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test, direct | MIT |
| Microsoft.CodeCoverage | 17.11.1 | Test, transitive | MIT |
| Microsoft.TestPlatform.ObjectModel | 17.11.1 | Test, transitive | MIT |
| Microsoft.TestPlatform.TestHost | 17.11.1 | Test, transitive | MIT |
| Newtonsoft.Json | 13.0.1 | Test, transitive | MIT |
| System.Reflection.Metadata | 1.6.0 | Test, transitive | See declared license URL |
| xunit | 2.9.2 | Test, direct | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Test, transitive | See declared license URL |
| xunit.analyzers | 1.16.0 | Test, transitive | Apache-2.0 |
| xunit.assert | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.core | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.extensibility.core | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.extensibility.execution | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Test, direct | Apache-2.0 |

The machine-readable package paths, resolved versions, direct/transitive
project usage, NuGet source response, repository commit, and vulnerability
result are retained in
[`docs/audit/LUNA-21-DEPENDENCY-AUDIT.json`](audit/LUNA-21-DEPENDENCY-AUDIT.json).

## CI and repository-hygiene gate

- [ ] Run the Windows workflow in [`.github/workflows/verify.yml`](../.github/workflows/verify.yml)
  on the commit under review.
- [ ] Confirm the workflow uses only `contents: read`, does not persist checkout
  credentials, and passes locked restore, Release verification, the retained
  smoke gate, dependency audit, secret scan, and performance gate.
- [ ] Review [`docs/audit/LUNA-22-BUNDLED-DATA-REVIEW.md`](audit/LUNA-22-BUNDLED-DATA-REVIEW.md)
  and obtain release-data owner approval for the anonymised startup workbook
  and archived source workbook policy.
- [ ] Confirm a normal application build copies only
  `src/ProjectCostForecast.App/Data/data_anonymised.xlsx`; `SampleData.json` and
  `InitialCostLoad.xlsx` must not be in the application package.
- [ ] Review the redacted source/history secret-scan report. It must contain
  zero findings; the scanner must never print matched values.
- [x] Treat tracked `release/ProjectCostForecast/` and `Temp/` cleanup as a
  separate reviewable change. The exact 16 release files plus one `Temp` file
  were authorized on 2026-08-29 and removed from Git tracking with
  `git rm --cached`; local copies remain on disk and ignored. See
  [`F-13-CLEANUP.md`](audit/F-13-CLEANUP.md).
- [ ] Retain generated CI reports as short-lived workflow artifacts rather than
  committing build output or scan results.

The current LUNA-22 implementation records the controls and review evidence,
and the separately authorized F-13 tracked-artifact cleanup is complete. The
release-data distribution decision remains a separate unchecked release gate.

## Release truth and readiness (LUNA-23)

Use [`docs/audit/LUNA-23-RELEASE-TRUTH.md`](audit/LUNA-23-RELEASE-TRUTH.md) as
the as-built boundary when reviewing this checklist. The following items are
the current status for the candidate; unchecked items are genuine release
limitations or approvals, not undocumented work.

- [x] Version identity is `1.0.1`; application version and project format
  version are recorded independently.
- [x] Project JSON format v1, legacy unversioned format v0 normalization, and
  rejection of future versions are covered by migration tests.
- [x] Raw transaction import support is limited to `.csv`, `.xlsx`, and `.xlsm`
  with bounded, validated, staged handling; direct original-workbook project
  import is not claimed.
- [x] Backup verification, restore-to-new-path, explicit overwrite protection,
  and recovery instructions are tested and documented.
- [x] Dirty close behavior is Save / Discard / Cancel; Cancel and failed Save
  keep the window open.
- [x] Negative product constraints are documented: no server/auth/database
  subsystem, no silent migration, no destructive restore default, and no
  financial row values in default diagnostics.
- [ ] Release-data owner approves the anonymised startup workbook, archived
  source workbook, and any distribution policy.
  SOL-00 accepts this P1 for codebase-audit closure only with no formal
  distribution, owner `user / release-data owner`, and review on 2026-11-29 or
  before any distribution, whichever occurs first.
- [ ] A signed installer or MSIX, upgrade path, and deployment rollback channel
  are implemented and tested.
- [ ] Product acceptance is completed against additional real projects.
- [x] Any tracked `release/ProjectCostForecast/` and `Temp/` cleanup is approved
  and executed as its own reviewable change; the F-13 packet records the
  2026-08-29 authorization and index-only/local-copy-preservation behavior.

## Closure and independent handoff (LUNA-24)

- [x] Review [`docs/audit/LUNA-24-FINDING-MATRIX.md`](audit/LUNA-24-FINDING-MATRIX.md)
  to confirm every F-00 through F-20 finding has a packet and disposition.
- [x] Produce [`docs/audit/LUNA_HANDOFF.md`](audit/LUNA_HANDOFF.md) with the
  clean-build evidence, independent Sol Ultra procedure, and deferred-risk
  register.
- [x] Confirm no unresolved P0 is hidden and that the F-13 P1 disposition is
  explicitly resolved by the dedicated cleanup packet.
- [x] Complete independent SOL-00 codebase-audit acceptance. See
  [`docs/audit/SOL-00-FINAL-AUDIT.md`](audit/SOL-00-FINAL-AUDIT.md); this does
  not complete the unchecked formal-production gates above.
