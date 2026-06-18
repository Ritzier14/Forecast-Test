# Alpha 1.6 — Excel Data Operations, Undo and Fill

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_6_Excel_Data_Operations_Undo_and_Fill.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| EXCEL-005 | Excel-compatible copy/paste, Ctrl+Enter and protected-cell warning | Medium-High | Copy/paste supports Excel tab-delimited data. One copied cell fills a selected range. Ctrl+Enter fills selected editable cells. If paste includes protected/locked/read-only cells, warn the user and ask whether to proceed. If the user proceeds, paste into unlo… |
| EXCEL-006 | Delete/Backspace clear cells and recalculate | Medium-High | Delete and Backspace clear selected editable cells using zero-as-blank display where configured and trigger normal recalculation/dirty-state updates. |
| EXCEL-008 | Recalculate after every edit path | Medium-High | Single edits, type overwrite, paste, cut, delete, row add/delete, curve changes, resource edits and schedule edits refresh dependent totals and state. |
| EXCEL-009 | Fill handle / drag-fill | High | Include Excel-style fill handle/drag-fill now. Pattern-based fill can be future unless needed for basic operation. |
| EXCEL-010 | Undo/redo for grid edits | High | Include undo/redo for common grid actions: single-cell edit, paste, cut, delete, row add/delete, and potentially curve grid edits. |
| EXCEL-014 | Validation feedback inside grid | Medium-High | Invalid edits do not fail silently. Show clear validation for invalid numbers/dates, text in numeric cells, locked edits, calculated cells and paste issues. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### EXCEL-005. Excel-compatible copy/paste, Ctrl+Enter and protected-cell warning — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Copy/paste supports Excel tab-delimited data. One copied cell fills a selected range. Ctrl+Enter fills selected editable cells. If paste includes protected/locked/read-only cells, warn the user and ask whether to proceed. If the user proceeds, paste into unlocked/editable cells only and show a skipped protected-cell count; protected cells must not be silently changed.
**Acceptance criteria**
- EXCEL-005-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-006. Delete/Backspace clear cells and recalculate — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Delete and Backspace clear selected editable cells using zero-as-blank display where configured and trigger normal recalculation/dirty-state updates.
**Acceptance criteria**
- EXCEL-006-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-008. Recalculate after every edit path — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Single edits, type overwrite, paste, cut, delete, row add/delete, curve changes, resource edits and schedule edits refresh dependent totals and state.
**Acceptance criteria**
- EXCEL-008-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-009. Fill handle / drag-fill — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Include Excel-style fill handle/drag-fill now. Pattern-based fill can be future unless needed for basic operation.
**Acceptance criteria**
- EXCEL-009-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-010. Undo/redo for grid edits — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Include undo/redo for common grid actions: single-cell edit, paste, cut, delete, row add/delete, and potentially curve grid edits.
**Acceptance criteria**
- EXCEL-010-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-014. Validation feedback inside grid — Alpha 1.6
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Invalid edits do not fail silently. Show clear validation for invalid numbers/dates, text in numeric cells, locked edits, calculated cells and paste issues.
**Acceptance criteria**
- EXCEL-014-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6


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