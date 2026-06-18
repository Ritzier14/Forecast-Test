# Alpha 1.13 — Resource Drilldown Panel Refresh

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **medium-high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_13_Resource_Drilldown_Panel_Refresh.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-034 | Resource drilldown panel new style, tabs and tighter border | Medium-High | Restyle only the resource drilldown/details panel to match the newer visual style shown in the provided reference image/general forecast style. Add a light border under Cost into Resource, Monthly Forecast and Spend Curve tabs, extending past the final tab si… |
| SPEC-035 | Resource drilldown grids use shared grid style/profile | Medium-High | All grids in the resource drilldown/details panel should use the same shared grid style/control/profile where applicable. Cost into Resource is read-only. Previous forecast months are read-only. Spend curve grid, monthly forecast grid and transaction grid use… |
| SPEC-036 | Resource spend curve graph month labels | Medium-High | The Spend Curve graph in the resource drilldown panel must show both calendar month and FY period labels where space allows. Labels should attempt to fit every month while remaining readable. |
| SPEC-037 | Resource detail panel collapse, Detail button toggle and hover overlay | Medium-High | Restore resource detail panel hide/collapse behaviour. Collapsed state persists. Collapse behaviour should match the side panel pattern: the panel can remain available as an overlay on hover. Clicking the Detail button toggles collapsed/expanded state and the… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-034. Resource drilldown panel new style, tabs and tighter border — Alpha 1.13
Origin: Added RDD-01, RDD-02, RDD-07 | Status: Active
**Requirement**
Restyle only the resource drilldown/details panel to match the newer visual style shown in the provided reference image/general forecast style. Add a light border under Cost into Resource, Monthly Forecast and Spend Curve tabs, extending past the final tab similar to the main forecast tab request. Tighten the horizontal border/padding around the detail panel to around one-third of the current excessive horizontal spacing without clipping content.
**Acceptance criteria**
- Resource drilldown panel visually matches the new style reference.
- No resource drilldown totals/data/calculations change.
- Light border appears under the three drilldown tabs.
- Selected tab underline remains clear and in front.
- Horizontal border wraps closer to content without clipping.
- Only resource drilldown panel is affected.
**Decisions captured from Stan's answers**
- Resource drilldown only, not every detail panel.
- Tab border can be completed as part of the panel restyle.
- Start horizontal padding at one-third of existing.

### SPEC-035. Resource drilldown grids use shared grid style/profile — Alpha 1.13
Origin: Added RDD-03 | Status: Active
**Requirement**
All grids in the resource drilldown/details panel should use the same shared grid style/control/profile where applicable. Cost into Resource is read-only. Previous forecast months are read-only. Spend curve grid, monthly forecast grid and transaction grid use the same shared grid profile unless a later implementation proves a separate profile is required.
**Acceptance criteria**
- Resource drilldown grids match the main grid style for headers, body cells, selection, borders and typography.
- Implementation reuses shared grid styles/behaviours and does not create resource-drilldown-only one-off grids.
- Read-only cells have a clear non-editable style.
- Copy, selection and right-click behaviour follow the Excel-style grid standard where applicable.
**Decisions captured from Stan's answers**
- All resource drilldown grids should use the same profile.
- Cost into Resource and previous forecast months are read-only.

### SPEC-036. Resource spend curve graph month labels — Alpha 1.13
Origin: Added RDD-04 | Status: Active
**Requirement**
The Spend Curve graph in the resource drilldown panel must show both calendar month and FY period labels where space allows. Labels should attempt to fit every month while remaining readable.
**Acceptance criteria**
- Graph shows both month and FY period references on/near horizontal axis.
- Every month label is attempted where space allows.
- Labels remain readable at normal panel size.
- Graph resizes correctly with panel collapse/expand.
**Decisions captured from Stan's answers**
- Show both calendar months and FY periods.

### SPEC-037. Resource detail panel collapse, Detail button toggle and hover overlay — Alpha 1.13
Origin: Added RDD-05, RDD-06; PIVOT-03/SCH-N02 overlap | Status: Active
**Requirement**
Restore resource detail panel hide/collapse behaviour. Collapsed state persists. Collapse behaviour should match the side panel pattern: the panel can remain available as an overlay on hover. Clicking the Detail button toggles collapsed/expanded state and the button text/icon changes to make the current action clear. Pivot Builder tab should collapse the resource drilldown every time it is entered. Schedule tab must not show the resource drilldown at all; resource drilldown is completely unavailable in Schedule context and the separate Activity panel is used instead.
**Acceptance criteria**
- Visible hide/collapse control exists.
- Detail button collapses visible panel and restores collapsed panel.
- Button text/icon changes between Detail/Hide Detail/Show Detail as agreed in UI.
- Collapsed state persists when switching tabs/reopening.
- Hover overlay is available while collapsed.
- Pivot Builder tab collapses detail every time.
- Schedule tab does not make the resource drilldown available; Activity panel is the detail mechanism.
**Decisions captured from Stan's answers**
- Collapsed state persists.
- Hover overlay remains available.
- Pivot Builder collapse is every time and not user-preference overridden.
- Schedule tab makes resource drilldown completely unavailable.


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