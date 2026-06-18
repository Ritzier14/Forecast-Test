# Alpha 1.11 — CTC Row Controls, Group Menus and Visual Defects

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **medium-high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_11_CTC_Row_Controls_Group_Menus_and_Visual_Defects.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-028 | Add-row pill at bottom of CTC forecast grid | Medium-High | At the bottom of the whole CTC forecast grid, provide a + Add row affordance styled like the app’s + New pattern. Clicking it creates a blank forecast line. Users can add while filters are active; after editing and unselecting the line, normal grouping/filter… |
| SPEC-038 | Right-click group/header expand-collapse menu | Medium-High | Right-clicking any collapsible group/header row across the CTC grid should show Expand Group or Collapse Group plus Expand All and Collapse All. The command should work across the category row/header area and all current/future group types. |
| SPEC-039 | Task/resource/category header and body indent alignment | Medium-High | For Task, Resource and Category columns only, body content should align with the header text indent created by header icons/images. Numeric columns remain right-aligned even if their headers have icons. |
| SPEC-055 | Blue column colour should use darker grid borders | Medium | When a blue column colour/highlight is applied, the grey grid borders should adjust darker enough to avoid excessive contrast and look intentional against the blue fill. |
| SPEC-056 | Resource drilldown top-right rounded border artifact | Medium | Fix the visual artifact at the top-right of the border around the resource drilldown panel so the rounded edge renders cleanly. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-028. Add-row pill at bottom of CTC forecast grid — Alpha 1.11
Origin: Original CTC item 22 | Status: Active
**Requirement**
At the bottom of the whole CTC forecast grid, provide a + Add row affordance styled like the app’s + New pattern. Clicking it creates a blank forecast line. Users can add while filters are active; after editing and unselecting the line, normal grouping/filter rules may move or hide it based on its values.
**Acceptance criteria**
- Add-row pill/plus appears at bottom of the whole grid.
- Click creates a blank forecast line.
- No required fields are assumed unless existing model validation requires them.
- Grouping is determined by task name/resource/category values after entry, not by current group location.
- Adding while filters are active is allowed and then normal filter rules apply.
**Decisions captured from Stan's answers**
- Bottom of whole grid only, not end of each group.
- Create blank forecast line.

### SPEC-038. Right-click group/header expand-collapse menu — Alpha 1.11
Origin: Added MFG-01 | Status: Active
**Requirement**
Right-clicking any collapsible group/header row across the CTC grid should show Expand Group or Collapse Group plus Expand All and Collapse All. The command should work across the category row/header area and all current/future group types.
**Acceptance criteria**
- Right-click group/header row shows Expand Group/Collapse Group.
- Menu also includes Expand All and Collapse All.
- Targeted command changes only the clicked group.
- All-group commands change all groups.
- Group totals and visible rows remain correct.
**Decisions captured from Stan's answers**
- Applies to all group types, not category only.

### SPEC-039. Task/resource/category header and body indent alignment — Alpha 1.11
Origin: Added MFG-02 | Status: Active
**Requirement**
For Task, Resource and Category columns only, body content should align with the header text indent created by header icons/images. Numeric columns remain right-aligned even if their headers have icons.
**Acceptance criteria**
- Task, Resource and Category body content aligns visually with header text.
- Numeric columns remain right-aligned.
- Indent does not cause clipping or excessive lost space.
- Issue shown in screenshot is resolved.
**Decisions captured from Stan's answers**
- Only Task, Resource and Category columns are affected.

### SPEC-055. Blue column colour should use darker grid borders — Alpha 1.11
Origin: New bug from markup | Status: Active
**Requirement**
When a blue column colour/highlight is applied, the grey grid borders should adjust darker enough to avoid excessive contrast and look intentional against the blue fill.
**Acceptance criteria**
- Blue highlighted columns no longer have harsh light-grey border contrast.
- Border colour remains readable and consistent with theme.
- Other column colours are not degraded.

### SPEC-056. Resource drilldown top-right rounded border artifact — Alpha 1.11
Origin: New bug from markup | Status: Active
**Requirement**
Fix the visual artifact at the top-right of the border around the resource drilldown panel so the rounded edge renders cleanly.
**Acceptance criteria**
- Top-right border shows a clean rounded edge.
- No stray artifact remains at normal zoom/window sizes.
- Fix does not alter panel data or collapse behaviour.
# 4. Excel-style grid behaviour tasks
These tasks are kept separate from normal SPEC numbering. They define shared spreadsheet behaviour to be reused across grids. They should not duplicate the normal feature tasks; instead, normal tasks should reference these behaviours where applicable.


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