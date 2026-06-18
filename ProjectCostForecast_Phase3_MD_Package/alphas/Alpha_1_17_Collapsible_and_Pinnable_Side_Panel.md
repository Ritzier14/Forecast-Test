# Alpha 1.17 — Collapsible and Pinnable Side Panel

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **medium**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_17_Collapsible_and_Pinnable_Side_Panel.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-049 | Collapsible/pinnable side panel with icon rail and hover overlay | Medium | The side panel must collapse to an icon-only rail whose width can be resized by the user and persists between sessions. Hovering an icon after a delay opens the full panel as an overlay without shifting main content. The user can pin the expanded panel open;… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-049. Collapsible/pinnable side panel with icon rail and hover overlay — Alpha 1.17
Origin: Added SIDE-01 to SIDE-04 | Status: Active
**Requirement**
The side panel must collapse to an icon-only rail whose width can be resized by the user and persists between sessions. Hovering an icon after a delay opens the full panel as an overlay without shifting main content. The user can pin the expanded panel open; when pinned it shifts/resizes the main content. Pin state is global and pinning is available only when the panel is expanded. Collapse/Expand text and arrows change according to state, with normal/hover/click visual states.
**Acceptance criteria**
- Panel collapses to icon rail and icons remain usable.
- Collapsed width is user-resizable and persists.
- Hovering an icon opens the full panel as overlay after delay.
- Overlay closes on mouse leave unless pinned.
- Pin control pins expanded panel and shifts content.
- Pin state is remembered globally.
- Collapsed rail shows the word Expand and correct arrow direction.
- Tooltips/accessibility text reflect action.
**Decisions captured from Stan's answers**
- Hover opens full panel, not tooltip only.
- Pinning only when expanded.


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