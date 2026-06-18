# Alpha 1.18 — Pivot Builder Drag-and-Drop Layout

## Recommended Codex Settings

- Model: **Codex GPT-5.3**
- Reasoning: **medium**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_18_Pivot_Builder_Drag_and_Drop_Layout.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-050 | Pivot builder drag/drop and two-by-two layout | Medium | Pivot fields can be dragged from the field list to Filters, Rows, Columns or Values and moved between areas after placement. Dragging a field to Values defaults to Sum. Duplicate fields are not allowed. Pivot drop areas are arranged as two rows of two with Fi… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-050. Pivot builder drag/drop and two-by-two layout — Alpha 1.18
Origin: Added PIVOT-01 to PIVOT-03 | Status: Active
**Requirement**
Pivot fields can be dragged from the field list to Filters, Rows, Columns or Values and moved between areas after placement. Dragging a field to Values defaults to Sum. Duplicate fields are not allowed. Pivot drop areas are arranged as two rows of two with Filters/Columns on top and Rows/Values below. Field list sits above the drop area. Resource drilldown collapses every time the Pivot Builder tab is entered and user preference does not override this.
**Acceptance criteria**
- Fields drag to all four pivot areas.
- Fields can move between areas.
- Values defaults to Sum.
- Duplicate fields are rejected with clear feedback.
- Drop areas show two rows of two with clear titles/boundaries.
- Field list sits above.
- Entering Pivot Builder collapses resource drilldown every time without losing pivot config.
**Decisions captured from Stan's answers**
- No duplicate fields.
- Top row Filters/Columns, bottom row Rows/Values.


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