# Alpha 1.4 — Grid Render, Selection and Resource Performance

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_4_Grid_Render_Selection_and_Resource_Performance.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| GRID-005 | Forecast group header render optimisation | High | Group header totals must be precomputed or cached, not recalculated inside visual render paths. |
| GRID-006 | Selection and hover visual performance | High | Avoid full visual-tree scans on every mouse move/selection/current-cell change. Use shared states/styles/targeted updates. |
| GRID-007 | Management resource grid performance | High | Stop repeated Auto sizing/UpdateLayout; use shared width model and fast scrolling/editing. |
| GRID-008 | Schedule performance profile | High | Schedule table/Gantt must be tested with large schedules and remain responsive. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### GRID-005. Forecast group header render optimisation — Alpha 1.4
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Group header totals must be precomputed or cached, not recalculated inside visual render paths.
**Acceptance criteria**
- GRID-005-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-006. Selection and hover visual performance — Alpha 1.4
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Avoid full visual-tree scans on every mouse move/selection/current-cell change. Use shared states/styles/targeted updates.
**Acceptance criteria**
- GRID-006-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-007. Management resource grid performance — Alpha 1.4
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Stop repeated Auto sizing/UpdateLayout; use shared width model and fast scrolling/editing.
**Acceptance criteria**
- GRID-007-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-008. Schedule performance profile — Alpha 1.4
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Schedule table/Gantt must be tested with large schedules and remain responsive.
**Acceptance criteria**
- GRID-008-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4


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