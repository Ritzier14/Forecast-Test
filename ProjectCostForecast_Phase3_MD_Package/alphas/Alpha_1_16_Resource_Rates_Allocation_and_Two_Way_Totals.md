# Alpha 1.16 — Resource Rates, Allocation and Two-Way Totals

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_16_Resource_Rates_Allocation_and_Two_Way_Totals.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-046 | Resource management rate default and override | High | In the resource management tab, default rate is calculated from the raw-data Rate column for the resource only. Use the most frequently occurring exact raw-data rate from the last two months. Do not round rates before frequency comparison. If there is a tie,… |
| SPEC-047 | Resource allocation percentage calculated from forecast values | High | When a resource is added to the resource tab, the app should calculate existing percentage allocations from matching forecast values using the resource rate and a monthly hours basis that is configurable per resource per project. For example, if Scott has for… |
| SPEC-048 | Resource tab and CTC forecast two-way total transfer | High | Resource tab and CTC forecast values must stay in sync both ways. If the user enters or edits a percentage in the resource tab, the corresponding CTC forecast cell value is added or adjusted. If the user edits a value in the forecast sheet, the corresponding… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-046. Resource management rate default and override — Alpha 1.16
Origin: Added CURVE-12/CURVE-13 clarified as Resource tab | Status: Active
**Requirement**
In the resource management tab, default rate is calculated from the raw-data Rate column for the resource only. Use the most frequently occurring exact raw-data rate from the last two months. Do not round rates before frequency comparison. If there is a tie, choose the highest frequency in the latest month. The user can override the calculated rate for that resource, the UI indicates overridden vs calculated, dependent hours/totals update, and a reset-to-calculated-rate option exists.
**Acceptance criteria**
- Rate default uses resource-only grouping.
- Only last two months of raw-data Rate values are considered.
- Exact values are compared without rounding.
- Most frequent value wins; tie resolves by highest frequency in latest month.
- One-off outliers do not override the most common value.
- User override persists for that resource.
- UI shows overridden status and can reset to calculated.
**Decisions captured from Stan's answers**
- This belongs to Resource tab/resource management, not curve editor.

### SPEC-047. Resource allocation percentage calculated from forecast values — Alpha 1.16
Origin: Added RT-01 plus markup explanation | Status: Active
**Requirement**
When a resource is added to the resource tab, the app should calculate existing percentage allocations from matching forecast values using the resource rate and a monthly hours basis that is configurable per resource per project. For example, if Scott has forecast money in a month and his rate is 150, the app should calculate monthly capacity value as rate × configured monthly hours, then percentage = forecast value / monthly capacity value. Hours and totals are calculated from the percentage allocation.
**Acceptance criteria**
- Adding a resource pulls existing monthly forecast amounts for that resource where available.
- If rate is 150 and configured monthly hours are 160, monthly capacity value is 24,000.
- Percentage allocation is forecast value divided by monthly capacity value.
- Hours and totals update from the percentage.
- User can still override editable values where allowed.
**Decisions captured from Stan's answers**
- Existing initial questions were not relevant; markup clarified the formula with Scott example.
- Monthly hours basis is configurable per resource per project.

### SPEC-048. Resource tab and CTC forecast two-way total transfer — Alpha 1.16
Origin: Added RT-02 | Status: Active
**Requirement**
Resource tab and CTC forecast values must stay in sync both ways. If the user enters or edits a percentage in the resource tab, the corresponding CTC forecast cell value is added or adjusted. If the user edits a value in the forecast sheet, the corresponding resource tab month recalculates the percentage. Values are not locked and transfer happens immediately on commit/edit.
**Acceptance criteria**
- Editing resource percentage updates corresponding forecast value.
- Editing forecast value recalculates corresponding resource percentage.
- No duplicate or stale totals remain after edits.
- Values are not locked solely because they transferred.
- Updates occur instantly/after normal edit commit.
**Decisions captured from Stan's answers**
- Transfer is both directions.
- No lock after transfer.
- Instant behaviour desired.


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