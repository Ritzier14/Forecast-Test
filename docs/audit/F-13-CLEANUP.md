# F-13 tracked-artifact cleanup packet

Recorded: 2026-08-29

## Authorization and decision

The user explicitly delegated the F-13 decision and final closure to Sol on
2026-08-29. Sol selected the cleanup option: remove all 17 listed release and
scratch artifacts from Git tracking in a dedicated reviewable cleanup packet.
This packet does not itself claim SOL-00 final acceptance; the parent Sol agent
subsequently reviewed, verified, committed, pushed, and completed the
independent final audit.

## Exact scope

Only these 17 paths are in scope:

```text
Temp/data_anonymised.xlsx
release/ProjectCostForecast/ClosedXML.Parser.dll
release/ProjectCostForecast/ClosedXML.dll
release/ProjectCostForecast/Data/InitialCostLoad.xlsx
release/ProjectCostForecast/Data/SampleData.json
release/ProjectCostForecast/Data/data_anonymised.xlsx
release/ProjectCostForecast/DocumentFormat.OpenXml.Framework.dll
release/ProjectCostForecast/DocumentFormat.OpenXml.dll
release/ProjectCostForecast/ExcelNumberFormat.dll
release/ProjectCostForecast/ProjectCostForecast.App.deps.json
release/ProjectCostForecast/ProjectCostForecast.App.dll
release/ProjectCostForecast/ProjectCostForecast.App.exe
release/ProjectCostForecast/ProjectCostForecast.App.pdb
release/ProjectCostForecast/ProjectCostForecast.App.runtimeconfig.json
release/ProjectCostForecast/RBush.dll
release/ProjectCostForecast/SixLabors.Fonts.dll
release/ProjectCostForecast/System.IO.Packaging.dll
```

The cleanup uses `git rm --cached` with the exact paths. It removes the paths
from the Git index only and preserves every local copy on disk. The existing
`.gitignore` rules continue to ignore `/release/ProjectCostForecast/` and
`/Temp/`, preventing the local copies from returning as ordinary untracked
changes.

No source fixture is part of the cleanup. The application startup fixture and
source/test fixtures remain tracked:

```text
src/ProjectCostForecast.App/Data/data_anonymised.xlsx
src/ProjectCostForecast.App/Data/SampleData.json
src/ProjectCostForecast.App/Data/InitialCostLoad.xlsx
```

## Recovery and provenance

The pre-cleanup baseline is `85f3b03` (`docs(audit): prepare LUNA-24 Sol
handoff`). If a removed artifact is needed again, it can be recovered from that
Git history by restoring the exact path from the baseline. The three release
data copies and the `Temp` workbook are also reproducible from the retained
source fixtures; generated release binaries and symbols are reproducible from a
Release build. No local file is deleted by this packet.

## Verification evidence

- The successful cleanup command was `git rm --cached --` followed by the exact
  17 paths above; the index recorded 17 deletions and no working-tree file was
  deleted.
- The exact-path invariant check reports `IGNORED_COUNT=17`,
  `TRACKED_CLEANUP_COUNT=0`, `SOURCE_TRACKED_COUNT=3`, and
  `LOCAL_MISSING_COUNT=0`.
- `git ls-files --` for the exact 17 paths returns no paths, while
  `git check-ignore --no-index --` matches all 17 paths.
- The focused command
  `dotnet test tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj -c Debug --no-restore --filter "FullyQualifiedName~Luna22RepositoryHygieneTests|FullyQualifiedName~Luna24ClosureEvidenceTests" /p:BaseOutputPath='artifacts/f13-targeted/'`
  passes all 5 tests.
- The repository verifier
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\verify.ps1' -RunLegacySmoke`
  passes locked restore, a Release build with 0 warnings/errors, 185 discovered
  tests, and all 428 retained smoke assertions.
- `git diff --check` passes. All 17 local paths remain present on disk after
  the index-only operation, and all three source fixture paths remain tracked.

At this packet boundary, SOL-00 remains pending independent parent-agent review.
SOL-00 subsequently completed; see
[`SOL-00-FINAL-AUDIT.md`](SOL-00-FINAL-AUDIT.md).
