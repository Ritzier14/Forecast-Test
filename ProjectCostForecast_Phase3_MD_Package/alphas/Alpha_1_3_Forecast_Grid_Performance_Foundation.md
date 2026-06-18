# Alpha 1.3 — Forecast Grid Performance Foundation

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_3_Forecast_Grid_Performance_Foundation.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| GRID-001 | Performance diagnostics and counters | High | Add diagnostics for grid first draw, resize duration, overlay rebuilds, selection update duration, group-header render count, paste duration and schedule comparison load time. |
| GRID-002 | Forecast grid first draw, resize and scroll performance | High | Forecast grid must draw quickly and resize/scroll without freezing, especially with all forecast month columns visible. Column resize slowness is confirmed as affecting the forecast grid. |
| GRID-003 | Column sizing and virtualization | High | Stop broad Auto sizing during normal operation. Use fixed/user-controlled widths. Enable/test column virtualization and document exceptions. |
| GRID-004 | Forecast year/month overlay rebuild control | High | Overlays rebuild only when geometry changes, not every layout update. Avoid flicker and unnecessary child recreation. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### GRID-001. Performance diagnostics and counters — Alpha 1.3
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Add diagnostics for grid first draw, resize duration, overlay rebuilds, selection update duration, group-header render count, paste duration and schedule comparison load time.
**Acceptance criteria**
- GRID-001-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-002. Forecast grid first draw, resize and scroll performance — Alpha 1.3
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Forecast grid must draw quickly and resize/scroll without freezing, especially with all forecast month columns visible. Column resize slowness is confirmed as affecting the forecast grid.
**Acceptance criteria**
- GRID-002-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-003. Column sizing and virtualization — Alpha 1.3
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Stop broad Auto sizing during normal operation. Use fixed/user-controlled widths. Enable/test column virtualization and document exceptions.
**Acceptance criteria**
- GRID-003-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-004. Forecast year/month overlay rebuild control — Alpha 1.3
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Overlays rebuild only when geometry changes, not every layout update. Avoid flicker and unnecessary child recreation.
**Acceptance criteria**
- GRID-004-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3


## Required Smoke Tests

- Run the acceptance criteria for every task in this Alpha.
- Confirm no unrelated UI workflows are changed.
- Confirm project open/save still works after changes, where applicable.
- Confirm no new build errors are introduced.
- For grid-related Alphas, test resize, selection, copy/paste, right-click menu, and locked/read-only behaviour where applicable.

## Codex Guardrails

- Preserve existing working behaviour unless this Alpha explicitly changes it.
- Do not rename public user-facing concepts unless the requirement says to.
- Do not silently change calculation, period, save/load, or import behaviour outside the included tasks.
- If implementation requires a broader refactor, keep the visible behaviour equivalent and document the reason in the commit/summary.