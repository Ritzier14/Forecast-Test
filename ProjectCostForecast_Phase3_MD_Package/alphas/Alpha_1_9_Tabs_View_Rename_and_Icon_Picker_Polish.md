# Alpha 1.9 — Tabs, View Rename and Icon Picker Polish

## Recommended Codex Settings

- Model: **Codex GPT-5.3**
- Reasoning: **medium**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_9_Tabs_View_Rename_and_Icon_Picker_Polish.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-013 | Built-in icon picker for KPI pill, tab and group header icons | Medium | Right-click icon changing must use one shared built-in icon picker. Users can change KPI pill box icons, the specified forecast selection-tab icons, and group-header icons. Icon choices apply immediately and can reset to default. KPI pill and forecast selecti… |
| SPEC-016 | Forecast selection-tab icon changes | Medium | The icons next to the specified forecast selection tabs should support right-click icon change using the shared built-in icon picker. This scope is only these forecast selection tabs for now. |
| SPEC-017 | Selection tab strip light border and selected underline | Medium | The specified forecast selection tab row should have a light border underneath it. The border extends a fixed length past the last tab. The selected tab blue underline must appear in front of that border. Border does not appear when only one tab exists. |
| SPEC-018 | Compact Sort / Filter / Group controls | Medium | The + Sort, + Filter and + Group controls in the forecast tab area should keep their current visual style but be slightly tighter: one font-size step smaller and a tighter height/padding. |
| SPEC-021 | New view enters rename mode automatically | Medium-High | When the user adds a new view, it should create the next default name based on active view count and immediately enter rename mode with the whole default name selected. Enter commits, Escape cancels rename but keeps the new view with default name. Duplicate v… |
| SPEC-022 | View rename commit, minimum pill width and name rules | Medium-High | Clicking away from a new or existing view rename field commits the name. View pills have a stable minimum width and expand only when the typed text exceeds that width. Empty names restore previous/default name. Names trim leading/trailing spaces. No maximum l… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-013. Built-in icon picker for KPI pill, tab and group header icons — Alpha 1.9
Origin: Original header item 4, forecast tab item 7, CTC item 15 | Status: Active
**Requirement**
Right-click icon changing must use one shared built-in icon picker. Users can change KPI pill box icons, the specified forecast selection-tab icons, and group-header icons. Icon choices apply immediately and can reset to default. KPI pill and forecast selection-tab icon choices are saved as global user preferences. Forecast group-header icon choices are saved with the project because they are specific to that project/group header.
**Acceptance criteria**
- KPI pill icons can be changed through the shared icon picker.
- The specified tab icons can be changed through the same picker.
- Group-header icons can be changed through the same picker.
- Only built-in icons are available; custom/imported icons are out of scope.
- Changes apply immediately and include reset-to-default.
- KPI/tab icon user preferences restore after restart.
- Group-header icon choices restore when the project is reopened.
**Decisions captured from Stan's answers**
- Built-in icons only.
- Use the same picker for header, tab and group/header icons.
- KPI/tab icon persistence is global user preference.
- Group-header icon applies to the specific group header identity and is saved in the project.

### SPEC-016. Forecast selection-tab icon changes — Alpha 1.9
Origin: Original forecast tab item 7 | Status: Active
**Requirement**
The icons next to the specified forecast selection tabs should support right-click icon change using the shared built-in icon picker. This scope is only these forecast selection tabs for now.
**Acceptance criteria**
- Right-clicking a forecast selection-tab icon opens the shared icon picker.
- Built-in icons only are shown.
- The selected icon applies immediately.
- A reset option is available.
- Icon choice saves globally as a user preference.
**Decisions captured from Stan's answers**
- Only the indicated selection tabs are in scope for now.
- Same icon picker as KPI pill icons.

### SPEC-017. Selection tab strip light border and selected underline — Alpha 1.9
Origin: Original forecast tab item 8 | Status: Active
**Requirement**
The specified forecast selection tab row should have a light border underneath it. The border extends a fixed length past the last tab. The selected tab blue underline must appear in front of that border. Border does not appear when only one tab exists.
**Acceptance criteria**
- Light border appears under the specified tab row only.
- Border uses the screenshot/reference style and does not change other tab visuals.
- Border extends a fixed length past the last tab.
- Selected underline remains above/in front of the border.
- Border does not appear when there is only one tab.
**Decisions captured from Stan's answers**
- Scope is these forecast selection tabs only.
- Fixed extension length, not dynamic half-tab width.
- No overflow-under-hidden-tabs behaviour.

### SPEC-018. Compact Sort / Filter / Group controls — Alpha 1.9
Origin: Original forecast tab item 9 | Status: Active
**Requirement**
The + Sort, + Filter and + Group controls in the forecast tab area should keep their current visual style but be slightly tighter: one font-size step smaller and a tighter height/padding.
**Acceptance criteria**
- Controls remain recognisable with the plus sign visible.
- Only these forecast-tab controls are affected.
- Height wraps more tightly while hit targets remain usable.
- Controls align cleanly with the surrounding tab/filter bar.
**Decisions captured from Stan's answers**
- Same control style as now; only font/height/padding tightened.

### SPEC-021. New view enters rename mode automatically — Alpha 1.9
Origin: Original forecast tab item 12 | Status: Active
**Requirement**
When the user adds a new view, it should create the next default name based on active view count and immediately enter rename mode with the whole default name selected. Enter commits, Escape cancels rename but keeps the new view with default name. Duplicate view names are not allowed.
**Acceptance criteria**
- If three views are active, the next default name is View 4.
- Rename mode selects the whole default name immediately.
- Typing replaces the default name.
- Enter commits and Escape cancels rename while leaving the view with its default name.
- Duplicate names are rejected.
**Decisions captured from Stan's answers**
- Default naming uses View 2, View 3, View 4 based on active view count.

### SPEC-022. View rename commit, minimum pill width and name rules — Alpha 1.9
Origin: Original forecast tab item 13 | Status: Active
**Requirement**
Clicking away from a new or existing view rename field commits the name. View pills have a stable minimum width and expand only when the typed text exceeds that width. Empty names restore previous/default name. Names trim leading/trailing spaces. No maximum length is required.
**Acceptance criteria**
- Clicking outside rename commits valid text.
- Pill does not shrink below minimum width after focus loss.
- Pill grows only when text requires more width.
- Empty name restores previous/default.
- Leading/trailing spaces are trimmed.
- Same behaviour applies to existing view renames.
**Decisions captured from Stan's answers**
- No maximum view-name length.


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