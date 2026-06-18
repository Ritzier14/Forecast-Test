# Alpha 1.22 — Reusable UI Control Extraction

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_22_Reusable_UI_Control_Extraction.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| GRID-011 | Reusable UI controls | Very High | Create reusable column headers, filter menu, context provider, view strip, icon picker, warning bar, period column factory, band overlay, dialog shell, metric card, command groups, pan behaviour, add-row control, validation indicator and code-mapping editor. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### GRID-011. Reusable UI controls — Alpha 1.22
Origin: Grid Performance and Reusable Control Architecture | Status: Active
**Requirement**
Create reusable column headers, filter menu, context provider, view strip, icon picker, warning bar, period column factory, band overlay, dialog shell, metric card, command groups, pan behaviour, add-row control, validation indicator and code-mapping editor.
**Acceptance criteria**
- GRID-011-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.22


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