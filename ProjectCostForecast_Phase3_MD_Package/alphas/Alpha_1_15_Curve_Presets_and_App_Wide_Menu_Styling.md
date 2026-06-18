# Alpha 1.15 — Curve Presets and App-Wide Menu Styling

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_15_Curve_Presets_and_App_Wide_Menu_Styling.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-042 | Global user curve presets with metadata | Medium-High | The curve preset selector must include a User Presets section separated from built-in presets. Users can save the current curve shape as a global app-level preset with name, note and metadata for project, supplier/resource, total value and month count where a… |
| SPEC-044 | Modern rounded dropdown/menu style app-wide | High | Dropdown menus across the app should follow the modern rounded style used elsewhere, including curve menus and future shared menus. Hover, selected, disabled, separator and section-heading states should be styled consistently. Curve preset dropdowns should su… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-042. Global user curve presets with metadata — Alpha 1.15
Origin: Added CURVE-04 and CURVE-05 | Status: Active
**Requirement**
The curve preset selector must include a User Presets section separated from built-in presets. Users can save the current curve shape as a global app-level preset with name, note and metadata for project, supplier/resource, total value and month count where available. Presets save curve shape only, not exact monthly values. User presets are deletable from the selector and renameable/overwriteable through a preset management dialog; selecting a saved preset previews/applies it correctly.
**Acceptance criteria**
- User Presets section exists and is visually separated.
- Save current curve creates a global app-level user preset.
- Preset captures name, note and available metadata.
- Preset stores curve shape only.
- Saved presets appear later across projects.
- User presets can be deleted from the selector.
- User presets can be renamed/overwritten through a management dialog.
- Built-in presets remain unchanged.
**Decisions captured from Stan's answers**
- User presets are global/app-level.
- Save curve shape only.
- Deleteable from selector; rename/overwrite through management dialog.

### SPEC-044. Modern rounded dropdown/menu style app-wide — Alpha 1.15
Origin: Added CURVE-07; reusable controls overlap | Status: Active
**Requirement**
Dropdown menus across the app should follow the modern rounded style used elsewhere, including curve menus and future shared menus. Hover, selected, disabled, separator and section-heading states should be styled consistently. Curve preset dropdowns should support icons or mini curve thumbnails.
**Acceptance criteria**
- Curve dropdown uses modern rounded styling.
- Same dropdown/menu style is available app-wide through shared controls.
- Hover/selected/disabled/separator/section heading states are consistent.
- Preset menu supports icons or mini curve thumbnails.
- No conflict with reusable control/grid architecture tasks.
**Decisions captured from Stan's answers**
- Applies throughout the app, not just curve menus.


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