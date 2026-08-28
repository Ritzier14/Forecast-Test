# Audit baseline

- Recorded: 2026-08-29
- Branch: `alpha/1.13-1.22`
- Parent commit before checkpoint: `417240b` (`Expand forecasting UI and reporting workflows`)
- Checkpoint commit: the commit titled `chore(audit): checkpoint pre-audit working tree`; its SHA is recorded below after commit.
- Scope: all modifications and untracked files present before the audit checkpoint are preserved as pre-audit work. They are not attributed to LUNA-01.

## Repository state before the checkpoint

The worktree contained 41 modified tracked files and four untracked files. The modified files were existing application, model, view-model, and acceptance-harness changes. The untracked files were:

- `docs/VIBE_CODEBASE_AUDIT_AND_REMEDIATION_PLAN.md`
- `docs/Vibe_Coded_Codebase_Common_Problems_and_Audit_Guide_EXPANDED.pdf`
- `src/ProjectCostForecast.App/MainWindow.ForecastColumnReorder.cs`
- `src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.Presentation.cs`

No existing work was discarded, reset, stashed, or deleted.

## Toolchain

- OS: Windows 10.0.19045, x64
- .NET SDKs: 8.0.423 and 10.0.202
- Selected SDK for the baseline command: 10.0.202
- Target framework: `net8.0-windows`
- .NET 8 Windows Desktop runtime installed: 8.0.26 and 8.0.29
- `global.json`: not present

## Package inventory

Application direct package:

- `ClosedXML` 0.105.0

Resolved transitive packages:

- `ClosedXML.Parser` 2.0.0
- `DocumentFormat.OpenXml` 3.1.1
- `DocumentFormat.OpenXml.Framework` 3.1.1
- `ExcelNumberFormat` 1.1.0
- `RBush.Signed` 4.0.0
- `SixLabors.Fonts` 1.0.0
- `System.IO.Packaging` 8.0.1

The test project references the application and resolves the same dependency set transitively.

## Baseline verification

Commands were run from the repository root before the checkpoint:

| Command | Exit code | Result |
|---|---:|---|
| `dotnet restore ProjectCostForecast.sln` | 0 | Both projects restored successfully. |
| `dotnet build ProjectCostForecast.sln -c Release --no-restore` | 0 | Build succeeded; 0 warnings, 0 errors. |
| `dotnet run --project tests/ProjectCostForecast.Tests/ProjectCostForecast.Tests.csproj -c Release --no-build` | 0 | Console harness completed with `All Project Cost Forecast checks passed.` |
| `git diff --check` | 0 | No whitespace errors; Git reported expected LF-to-CRLF conversion warnings. |

The first restore attempt was blocked by the sandbox's NuGet TLS/network restriction and was successfully repeated with approved network access. The successful command above is the authoritative baseline result.

## Checkpoint identity

- Checkpoint commit SHA: to be filled immediately after the checkpoint commit.
- Checkpoint push result: to be filled after push.

