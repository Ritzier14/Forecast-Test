# Alpha 1.7 — Column State, Filtering and Forecast View Controls

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **medium-high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_7_Column_State_Filtering_and_Forecast_View_Controls.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-019 | Sort/filter/group field list and forecast-year hide options | Medium-High | Sort, Filter and Group field lists must not include individual dynamic forecast month columns. Calculated columns such as CTC, FCC, variance, budget and totals remain sortable/filterable. Hidden columns can still appear in sort/filter lists. Hide functions mu… |
| SPEC-020 | Quick filters submenu under Filter button | Medium-High | Add a Quick Filters submenu under the forecast tab Filter button. It should include all quick filters currently available from the CTC right-click panel. Only one quick filter may be active at a time. Active quick filters remain indicated by a pill box that t… |
| SPEC-029 | Show zero as blank option for forecast entry cells | Medium-High | Forecast entry cells should display zero values as blank by default while calculating/exporting as zero. Add a per-view context option labelled Show zero as blank. Dashes should be removed for forecast entry cells only. Calculated columns such as month varian… |
| SPEC-033 | Right-click panning smooth across grouped tables | Medium-High | Right-click drag panning over the entire data table must remain smooth with any group/filter state active. It must not snap vertically to the bottom during drag for collapsed or expanded groups. |
| EXCEL-012 | Right-click context from current selection | Medium-High | Right-clicking while multiple cells are selected acts on the whole selected range. Right-clicking a non-selected cell first makes it active/contextual before opening the menu. |
| EXCEL-013 | Column layout persistence per project | High | Column widths and order persist per project. Hidden columns, frozen boundary and zero/blank preferences persist according to relevant task rules. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-019. Sort/filter/group field list and forecast-year hide options — Alpha 1.7
Origin: Original forecast tab item 10 | Status: Active
**Requirement**
Sort, Filter and Group field lists must not include individual dynamic forecast month columns. Calculated columns such as CTC, FCC, variance, budget and totals remain sortable/filterable. Hidden columns can still appear in sort/filter lists. Hide functions must allow hiding the entire Forecast View or individual financial years such as FY26 and FY27.
**Acceptance criteria**
- Individual forecast month columns are removed from Sort, Filter and Group field lists.
- Calculated total/variance columns remain available.
- Hidden columns can still appear in Sort/Filter lists.
- Hide menu includes Forecast View and individual FY group options.
- No special month filter is added.
**Decisions captured from Stan's answers**
- Exclusion applies to sort, filter, group and hide for individual months.
- Financial year hide options are required.

### SPEC-020. Quick filters submenu under Filter button — Alpha 1.7
Origin: Original forecast tab item 11 | Status: Active
**Requirement**
Add a Quick Filters submenu under the forecast tab Filter button. It should include all quick filters currently available from the CTC right-click panel. Only one quick filter may be active at a time. Active quick filters remain indicated by a pill box that the user can close. Quick filters combine with normal filters using AND logic once that future AND-filter function exists.
**Acceptance criteria**
- Filter button has a Quick Filters submenu.
- All existing CTC right-click quick filters are included.
- Selecting a quick filter produces the same result as the existing right-click quick filter.
- Only one quick filter is active at once.
- The active quick filter is shown as a closable pill.
- AND combination with normal filters is recorded as a future dependency if not yet built.
**Decisions captured from Stan's answers**
- Submenu under Filter, not separate button.
- Multiple quick filters are not allowed.

### SPEC-029. Show zero as blank option for forecast entry cells — Alpha 1.7
Origin: Original CTC item 23 | Status: Active
**Requirement**
Forecast entry cells should display zero values as blank by default while calculating/exporting as zero. Add a per-view context option labelled Show zero as blank. Dashes should be removed for forecast entry cells only. Calculated columns such as month variance and budget variance are not in scope for this setting. Negative zero/rounding-to-zero should not show as blank unless the stored value is true zero.
**Acceptance criteria**
- Forecast entry zeros display blank by default.
- The setting is per view.
- Editing a zero-as-blank cell shows blank as the edit value.
- Exports output zeros, not blanks.
- Filtering/searching treats blank-displayed zeros as zero.
- Calculated columns are not affected by this item.
**Decisions captured from Stan's answers**
- Label is Show zero as blank.
- Scope is forecast entry area only.

### SPEC-033. Right-click panning smooth across grouped tables — Alpha 1.7
Origin: Original CTC item 27 | Status: Active
**Requirement**
Right-click drag panning over the entire data table must remain smooth with any group/filter state active. It must not snap vertically to the bottom during drag for collapsed or expanded groups.
**Acceptance criteria**
- Right-click panning works over the whole data table.
- No vertical snap-to-bottom occurs during drag.
- Behaviour is smooth with all grouping/filter modes.
- Collapsed and expanded groups both work.
**Decisions captured from Stan's answers**
- Issue occurs with all group filters, during drag, vertical only.

### EXCEL-012. Right-click context from current selection — Alpha 1.7
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Right-clicking while multiple cells are selected acts on the whole selected range. Right-clicking a non-selected cell first makes it active/contextual before opening the menu.
**Acceptance criteria**
- EXCEL-012-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.7

### EXCEL-013. Column layout persistence per project — Alpha 1.7
Origin: Excel-style grid behaviour standard | Status: Active
**Requirement**
Column widths and order persist per project. Hidden columns, frozen boundary and zero/blank preferences persist according to relevant task rules.
**Acceptance criteria**
- EXCEL-013-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.7


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