# Alpha 1.2 — Import Auto-Create Preview and Locked Month Protection

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_2_Import_Auto_Create_Preview_and_Locked_Month_Protection.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-054 | Import auto-create forecast line preview and locked month protection | High | Before import auto-creates forecast lines, show a preview controlled by a saved preference. Preview lists new task/resource combinations with task code, resource/manual name, project code, amount, transaction count, category and source. User can edit Manual N… |
| EXCEL-007 | Locked and read-only protection across all edit paths | High | Closed forecast months and protected cells are protected from typing, paste, cut, delete, curve tools, drilldown editing and bulk edit paths. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-054. Import auto-create forecast line preview and locked month protection — Alpha 1.2
Origin: Original ChatGPT item 32 | Status: Active
**Requirement**
Before import auto-creates forecast lines, show a preview controlled by a saved preference. Preview lists new task/resource combinations with task code, resource/manual name, project code, amount, transaction count, category and source. User can edit Manual Name only. If the user cancels auto-create, raw transactions are not imported and new combinations go to an unmatched list. Locked months are all months before CurrentPeriod and cannot be changed by typing, deleting, paste, drilldown changes or bulk/monthly commands. Paste operations involving locked cells reject the entire paste.
**Acceptance criteria**
- Saved preference controls whether auto-create preview appears.
- Preview shows required columns.
- User can edit Manual Name only in preview.
- Cancel stops raw transaction import and routes new combinations to unmatched list.
- Locked months are all months before CurrentPeriod.
- Locked months block all edit paths.
- Paste including locked cells rejects the entire paste.
**Decisions captured from Stan's answers**
- Admin override and locked-month edit message are future features.

### EXCEL-007. Locked and read-only protection across all edit paths — Alpha 1.2
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Closed forecast months and protected cells are protected from typing, paste, cut, delete, curve tools, drilldown editing and bulk edit paths.
**Acceptance criteria**
- EXCEL-007-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.2


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