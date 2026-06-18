# Alpha 1.10 — CTC Forecast Formatting and Header Boundaries

## Recommended Codex Settings

- Model: **Codex GPT-5.4**
- Reasoning: **medium-high**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_10_CTC_Forecast_Formatting_and_Header_Boundaries.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| SPEC-023 | Forecast group header icon change | Medium | The icon next to forecast group headers should support right-click change using the shared built-in icon picker. It is not for every resource row. Reset-to-default must be available. Newly imported resources do not inherit icons automatically. |
| SPEC-024 | Currency display options and plain-number exports | Medium-High | Forecast and calculated money columns should display without dollar symbols by default. A column context option should allow dollar symbols to be turned on. Thousands separators remain. Negative values show brackets, and existing red/green value colouring con… |
| SPEC-025 | Forecast month column width, compact million display and m-input parsing | Medium-High | Forecast month columns must use consistent width and display 100,000 without clipping. Values over 1,000,000 display using m notation with two decimals by default, e.g. 1,260,000 as 1.26m and exactly 1,000,000 as 1.00m. 999,999 displays in full. A column cont… |
| SPEC-026 | Month/FY header clipping and alternating light-theme colours | Medium-High | Increase/adjust header height so the bottom of month names is not clipped. Use existing light-theme colours to alternate every calendar-year band in the month row and every FY-year band in the FY row. Locked/closed month shading overrides alternating colours.… |
| SPEC-027 | Calendar/FY boundary borders aligned to grid content only | Medium-High | Calendar-year boundary borders should be black, crisp, thicker than normal month borders but thinner than currently. FY boundaries should be dashed and the same visual thickness as calendar-year borders. Boundary styles appear in headers and body cells, move… |
| SPEC-030 | Grid header gradient changed to very light grey | Medium | Grid header row gradient across all grid objects should change from dark blue to a very light grey gradient with a slightly more intense fade. This refers to grid headers only, not the top page/header image gradient. Blue accents for selected/active states re… |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

### SPEC-023. Forecast group header icon change — Alpha 1.10
Origin: Original CTC item 15 | Status: Active
**Requirement**
The icon next to forecast group headers should support right-click change using the shared built-in icon picker. It is not for every resource row. Reset-to-default must be available. Newly imported resources do not inherit icons automatically.
**Acceptance criteria**
- Right-clicking the group-header icon opens the shared icon picker.
- Icon applies immediately to the specific target.
- Built-in icons only; reset option available.
- Future imported resources do not automatically inherit the icon.
- Group-header icon choices are saved with the project.
**Decisions captured from Stan's answers**
- This is the icon for group headers, not every normal resource row.
- Use same picker as items 4 and 7.
- No automatic inheritance for later imports.
- Group-header icons are project-specific, not global user preferences.

### SPEC-024. Currency display options and plain-number exports — Alpha 1.10
Origin: Original CTC item 17 | Status: Active
**Requirement**
Forecast and calculated money columns should display without dollar symbols by default. A column context option should allow dollar symbols to be turned on. Thousands separators remain. Negative values show brackets, and existing red/green value colouring continues where applicable. Raw data and report tabs should also avoid dollar symbols. Exports should contain plain numeric values, not dollar symbols.
**Acceptance criteria**
- Forecast month input columns do not show dollar signs by default.
- Calculated money columns do not show dollar signs by default.
- Column context option can show dollar symbols.
- Thousands separators remain.
- Negative values display in brackets.
- Exports produce plain numbers.
**Decisions captured from Stan's answers**
- Applies broadly, including raw data and reports.
- Existing red/green display option remains.

### SPEC-025. Forecast month column width, compact million display and m-input parsing — Alpha 1.10
Origin: Original CTC item 18 | Status: Active
**Requirement**
Forecast month columns must use consistent width and display 100,000 without clipping. Values over 1,000,000 display using m notation with two decimals by default, e.g. 1,260,000 as 1.26m and exactly 1,000,000 as 1.00m. 999,999 displays in full. A column context option should allow changing the decimal places. Tooltips/edit mode show the full value. In forecast month cells, users may type a compact million value such as 1.26 and the app converts it to the full numeric value for calculation while displaying it as 1.26m/mil according to the column format.
**Acceptance criteria**
- All forecast month columns use consistent default width unless user changes them.
- 100,000 is readable without clipping.
- 1,260,000 displays as 1.26m.
- 1,000,000 displays as 1.00m.
- 999,999 displays in full.
- Tooltip or edit mode shows the full value.
- Column context can change m decimal places.
- Typing 1.26 in a forecast month cell can convert to 1,260,000 for calculations and display as 1.26m/mil.
**Decisions captured from Stan's answers**
- Compact format applies only to forecast month columns, not all calculated cells.
- Typing the decimal value without an m suffix is supported in forecast month cells.

### SPEC-026. Month/FY header clipping and alternating light-theme colours — Alpha 1.10
Origin: Original CTC item 19 | Status: Active
**Requirement**
Increase/adjust header height so the bottom of month names is not clipped. Use existing light-theme colours to alternate every calendar-year band in the month row and every FY-year band in the FY row. Locked/closed month shading overrides alternating colours. Schedule views do not use this colour rule.
**Acceptance criteria**
- Month-name text is fully visible.
- Header height/padding prevents bottom clipping.
- Calendar-year bands alternate across Jan-Dec groups.
- FY-year bands alternate across FY xx-01 to xx-12 groups.
- Month row and FY row are visually distinct.
- Locked month shading takes priority.
- Schedule is unaffected.
**Decisions captured from Stan's answers**
- Fix by adjusting header height.
- Use existing light theme.

### SPEC-027. Calendar/FY boundary borders aligned to grid content only — Alpha 1.10
Origin: Original CTC items 20 and 21 | Status: Active
**Requirement**
Calendar-year boundary borders should be black, crisp, thicker than normal month borders but thinner than currently. FY boundaries should be dashed and the same visual thickness as calendar-year borders. Boundary styles appear in headers and body cells, move correctly when columns are hidden, remain visible while horizontally scrolled, continue through group/summary rows, and stop at the last visible row rather than extending into the horizontal scrollbar.
**Acceptance criteria**
- Calendar-year borders look like a single thin black line, not double-drawn.
- FY borders are dashed and same size as calendar-year borders.
- Borders appear in headers and body cells.
- Hidden columns adjust the boundary correctly.
- Borders do not run through or over the horizontal scrollbar.
- Borders stop at the last visible row and stay aligned during scroll.
**Decisions captured from Stan's answers**
- Problem is visible in bottom scrollbar area at all zoom/window sizes.

### SPEC-030. Grid header gradient changed to very light grey — Alpha 1.10
Origin: Original CTC item 24 | Status: Active
**Requirement**
Grid header row gradient across all grid objects should change from dark blue to a very light grey gradient with a slightly more intense fade. This refers to grid headers only, not the top page/header image gradient. Blue accents for selected/active states remain.
**Acceptance criteria**
- All grid object headers use very light grey gradient.
- Header text/icons remain readable.
- Selected/active blue accents remain.
- Top page/header image gradient from SPEC-012 is unaffected.
**Decisions captured from Stan's answers**
- Apply across grid headers only.


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