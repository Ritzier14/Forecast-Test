# Alpha 1.14 — Curve Editor Interaction and Safety

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_14_Curve_Editor_Interaction_and_Safety.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-040 | Apply Curve to Selection menu with previews | High | When selected monthly forecast cells are in a single row, the context menu should show Apply Curve to Selection. The menu lists curve types with a modern preview. Clicking a preset applies it immediately. Hovering a preset previews it without permanent applic… |
| SPEC-041 | Curve graph node range, live bars and smooth clamp | High | Curve node adjustment range scaling must work symmetrically upward and downward with no maximum upward cap. Dragging nodes updates bars on every mouse movement. Locked cells/bars remain fixed during live updates. The curve must clamp visually and internally a… |
| SPEC-043 | Curve month padlocks and selection locks | High | In the curve adjustment UI, each month should have a padlock indicator. Clicking a padlock toggles that month locked/unlocked for the current curve edit. Manually entering a value locks that cell for the current edit. Curve adjustments do not change locked ce… |
| SPEC-045 | Curve window spacing and lower grid style | High | Improve spacing between Hide Monthly Bars and preset curve dropdown so the row is compact but not cramped. Keep controls on the same row at smaller widths. The lower grid in the curve window must use the modern shared grid style. Only the New Value column is… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-040. Apply Curve to Selection menu with previews — Alpha 1.14
Origin: Added CURVE-01 and CURVE-10 | Status: Active
**Requirement**
When selected monthly forecast cells are in a single row, the context menu should show Apply Curve to Selection. The menu lists curve types with a modern preview. Clicking a preset applies it immediately. Hovering a preset previews it without permanent application and updates both curve line and bars; moving away restores prior state unless applied. A red warning in the preview indicates when the selection includes locked months. At the bottom of the curve menu, include a separator and Adjust Curve option that opens the full curve graph/editor.
**Acceptance criteria**
- Apply Curve to Selection appears only for valid single-row monthly forecast selections.
- Menu lists available curve types.
- Hover previews built-in and user presets without applying.
- Hover preview updates curve line and bars; keyboard focus also previews.
- Click applies the preset immediately to unlocked cells only.
- Locked months are skipped and warning text is shown.
- Separator and Adjust Curve option are at bottom.
**Decisions captured from Stan's answers**
- Preset click applies immediately.
- Only monthly forecast cells in one row are supported.
- Keyboard focus preview is supported.

### SPEC-041. Curve graph node range, live bars and smooth clamp — Alpha 1.14
Origin: Added CURVE-02, CURVE-03, CURVE-09 | Status: Active
**Requirement**
Curve node adjustment range scaling must work symmetrically upward and downward with no maximum upward cap. Dragging nodes updates bars on every mouse movement. Locked cells/bars remain fixed during live updates. The curve must clamp visually and internally at top/bottom chart boundaries and flatten smoothly as it reaches the boundary so it never displays values/curves beyond the top or bottom line. This applies to every preset curve type.
**Acceptance criteria**
- Upward and downward range scaling are symmetrical.
- Bars update on every mouse movement while dragging.
- Locked bars remain visually fixed.
- Curve never renders beyond top/bottom boundaries.
- Intermediate values and final values are clamped during drag.
- No snap-back-only behaviour after release.
- All preset types use the same boundary rules.
**Decisions captured from Stan's answers**
- Every mouse movement update is required.
- Both visual and intermediate calculated values are clamped.

### SPEC-043. Curve month padlocks and selection locks — Alpha 1.14
Origin: Added CURVE-06 | Status: Active
**Requirement**
In the curve adjustment UI, each month should have a padlock indicator. Clicking a padlock toggles that month locked/unlocked for the current curve edit. Manually entering a value locks that cell for the current edit. Curve adjustments do not change locked cells. Locks do not persist after the curve window/edit session closes. Provide Unlock All and allow locking a selected set of months where practical, such as shift-clicking several months.
**Acceptance criteria**
- Each month has a clear lock indicator.
- Click toggles lock/unlock for the current edit.
- Manual entry locks the corresponding month for current edit.
- Locked months are not changed by curve adjustments.
- Locks are visually clear.
- Unlock All is available.
- Locks are cleared after closing the curve window/edit session.
**Decisions captured from Stan's answers**
- Locks are not saved to project or app settings.
- Lock All is not required; selected-month locking is desirable.

### SPEC-045. Curve window spacing and lower grid style — Alpha 1.14
Origin: Added CURVE-08 and CURVE-11 | Status: Active
**Requirement**
Improve spacing between Hide Monthly Bars and preset curve dropdown so the row is compact but not cramped. Keep controls on the same row at smaller widths. The lower grid in the curve window must use the modern shared grid style. Only the New Value column is editable; Current Value should have a greyed-out read-only look similar to closed months. The grid does not show rates.
**Acceptance criteria**
- Spacing between Hide Monthly Bars and preset dropdown is visually balanced.
- Controls remain on the same row at smaller widths.
- Lower grid matches modern main forecast grid style.
- Current Value/read-only columns are clearly greyed out.
- New Value is editable and follows Excel-style grid behaviour.
- Rates are not shown in this grid.
**Decisions captured from Stan's answers**
- Do not use shared form spacing for this row.
- Not rates in lower grid.


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