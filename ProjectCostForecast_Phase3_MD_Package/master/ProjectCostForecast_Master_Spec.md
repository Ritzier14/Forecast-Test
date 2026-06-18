# ProjectCostForecast Master Specification

Source: `ProjectCostForecast_Phase2_Spec_With_Alpha_Assignments_v2_Max6.docx`.

This Markdown version is intended for Codex context. Use the Alpha MD files for scoped build instructions.

## How Codex should use this file

- Treat this as the full backlog/specification context.
- Implement only the Alpha file supplied for the current build.
- Do not implement tasks outside that Alpha unless required to satisfy listed acceptance criteria.
- Use screenshots in `../images` as visual references when the Alpha file lists them.

## Screenshot index

See [`../images/image_index.md`](../images/image_index.md).

| Source files | The spec.docx = original raw list; Spec markup - SD.docx = answered working markup. |
| --- | --- |
| Phase | Phase 1 only. No Alpha plan, Alpha assignment, MD files or release schedule included. |
| Numbering rule | Normal build items use SPEC-001 onwards. Excel-style grid items use EXCEL-001 onwards. Grid performance/reusable architecture items use GRID-001 onwards. |
| Date prepared | 2026-06-16 10:16 |

# 1. Phase 1 processing notes

- Stan answers have been converted into requirements, acceptance criteria and resolved decisions where clear.

- Items marked remove have been removed from the active build list but remain visible in the coverage map.

- Duplicates have been merged rather than repeated.

- The Excel-style grid standard and Grid Performance/Reusable Control Architecture sections remain separately numbered as EXCEL and GRID items.

- Second-round answers have been applied. No unanswered second-round questions remain from the supplied Round 2 list.

# 2. Duplicate, merged and removed items

| Original / area | Phase 1 treatment |
| --- | --- |
| Original 16 | Merged into SPEC-003 because it duplicates original P04 / item 4 forecast-delete recalculation bug. |
| Original 30 and 31 | Merged into SPEC-053 because both cover CurrentPeriod and saved-period authority. |
| Original 33 | Merged into GRID-012 because it is a grid architecture guardrail, not a separate user feature. |
| Original P01 / item 1 | Covered by GRID-001 to GRID-008 and EXCEL-013 because column resizing is part of grid performance architecture. |
| Original P07 / item 7 | Covered by EXCEL-002 and EXCEL-006 because resource-cell overwrite is part of global Excel-style grid behaviour. |
| Original 14 | Removed. Stan confirmed it was accidental and not needed. |
| Original 28 | Removed. Stan marked category grouping text issue as removed after the task/code/category rewrite. |
| Original 29 | Removed. Blank placeholder only. |
| CURVE-12 and CURVE-13 | Moved conceptually to Resource Rate tasks, because Stan clarified these are resource-management-tab rate behaviours, not curve-editor behaviours. |
| RDD/SIDE/Schedule detail panel overlap | Separated into resource drilldown panel behaviour, side navigation panel behaviour, and schedule Activity panel behaviour. |

# 3. Cleaned normal specification items

### SPEC-001. Grid body text one size smaller than headers — Alpha 1.8

Origin: Original item 2 / P02 | Status: Active

**Requirement**

All grid body/content text across every tab must render one font-size step smaller than the related grid/header text. Popup windows, mapping dialogs, context menus, tooltips and validation panels are out of scope for this font reduction.

**Acceptance criteria**

- All grid body cells use header font size minus one step.

- Currency and numeric cells use the smaller body size.

- Grid row heights reduce to suit the smaller font without clipping.

- Header rows, tab labels, buttons and app title/header text are not unintentionally reduced.

**Decisions captured from Stan's answers**

- Scope is every tab, but only grid/content text below headers.

- Popup/dialog/menu/tooltip/validation panel body fonts do not change.

### SPEC-002. Header-band expand/collapse icon and border fix — Alpha 1.8

Origin: Original item 3 / P03 | Status: Active

**Requirement**

The forecast header-band expand/collapse control must move directly down one row into the year/header line. Replace the two separate expand/collapse icons with one state-changing icon. The header border must draw continuously with no gap on the left border.

**Acceptance criteria**

- One state-changing icon is used instead of two separate icons.

- The icon is positioned directly down into the header/year band.

- The left header border has no gap around the icon.

- The same placement is used in all grouped forecast views.

- Expand/collapse behaviour remains unchanged.

**Decisions captured from Stan's answers**

- Affected icon is the header band icon, not normal row group expanders.

- Border issue is a missing/gapped left border.

### SPEC-003. Forecast Delete key recalculation and save defect — Alpha 1.1

Origin: Original items 4 and 16 / P04 | Status: Active

**Requirement**

Deleting or clearing a user-entered forecast month value must immediately recalculate all dependent values. Blank forecast cells must calculate as zero. The Delete-key path must commit and recalculate the same way as typing zero and leaving the cell. Add and investigate the related save/persistence bug reported during testing.

**Acceptance criteria**

- Pressing Delete on a user-entered forecast month amount immediately updates CTC, FCC, month forecast variance, last-month variance, budget variance, total budget variance, group totals, category summaries, pivots and reports where applicable.

- Blank forecast month cells calculate as zero.

- Deleting a value and then saving/reopening preserves the cleared value and recalculated totals.

- Multi-cell Delete operations recalculate once after the batch edit.

- Locked months block deletion before recalculation.

**Decisions captured from Stan's answers**

- The deleted value was a user-entered monthly forecast amount, not a calculated field.

- All variance fields should update.

- Immediate recalculation is desired if performance remains acceptable.

- Original item 16 was removed as a duplicate and merged here.

### SPEC-004. Header image seam/border removal — Alpha 1.8

Origin: Original item 5 / P05 | Status: Active

**Requirement**

The top heading picture must appear as one continuous image background across the full header area, with no visible line, seam or border cutting through it. There should be no border around the heading image area.

**Acceptance criteria**

- No visible line or seam cuts through the header image at normal zoom.

- The issue is removed at all tested window sizes.

- Header image and overlay controls remain aligned during resize.

- The fix does not blur icons, controls or text over the header image.

**Decisions captured from Stan's answers**

- Line appears to be a border between containers and is visible all the time.

- The header image should be one continuous background.

- No border should remain around the heading area.

### SPEC-005. Right-click any normal CTC row cell to Add as management task — Alpha 1.5

Origin: Original item 6 / P06; EXCEL-012 overlap | Status: Active

**Requirement**

Right-clicking any cell in an eligible normal CTC forecast row must expose the existing Add as management task action. The action must not depend on right-clicking only the forecast/month area.

**Acceptance criteria**

- Right-clicking task, resource, category or forecast cells in a normal CTC row shows Add as management task when eligible.

- The created/linked management task uses the correct row context.

- Group/header rows do not show this action.

- If already linked, the menu item is disabled and displays Add as management task (already added).

- Locked/closed month cells still allow this row-level action.

**Decisions captured from Stan's answers**

- Action label already exists as Add as management task.

- Scope is only normal CTC forecast rows.

### SPEC-006. Schedule row selection, active row and Gantt selection sync — Alpha 1.19

Origin: Original item 8 / P08; EXCEL-015 overlap | Status: Active

**Requirement**

Schedule rows must select visibly and reliably. Clicking a row makes it the active row. Ctrl+click adds/removes rows from the selection. Clicking a Gantt bar selects the linked schedule row. Selected rows must remain selected while scrolling and must be usable by row commands.

**Acceptance criteria**

- Clicking a schedule row visibly selects it and sets the active row.

- Ctrl+click supports multi-row selection.

- Gantt bar click selects the corresponding row.

- Selection remains visible and stable when scrolling vertically or horizontally.

- Delete, indent, outdent, link, copy, paste and cut commands use the selected rows where applicable.

- The active row feeds the future Activity detail panel.

**Decisions captured from Stan's answers**

- The immediate bug is the selected-row highlight not showing.

- Gantt bar selection should sync with the grid.

- Multi-row support is required.

### SPEC-007. Baseline comparison opens as non-blocking schedule window — Alpha 1.20

Origin: Original item 9 / P09 | Status: Active

**Requirement**

The Baseline Comparison button must not freeze the app. It should open a separate non-blocking window that displays the same schedule-style view broken out from the main window. This is intended so another version of the programme can be opened alongside it later.

**Acceptance criteria**

- Clicking Baseline Comparison opens a separate non-blocking window.

- The main app remains movable and interactive after the comparison window is visible.

- The app does not require force-close on a small schedule of around 15 activities.

- The comparison window gives clear feedback if comparison/baseline data is missing.

**Decisions captured from Stan's answers**

- The freezing control is the Baseline Comparison button.

- Freeze occurs after the popup is visible and currently requires force close.

- Window should be separate and non-blocking.

### SPEC-008. Schedule link clipboard supports repeated source entries and safe linking — Alpha 1.19

Origin: Original item 10 / P10; EXCEL-016 overlap | Status: Active

**Requirement**

The schedule link clipboard must allow the same source activity to be added more than once as separate clipboard entries so it can be linked to separate target tasks. Each used clipboard entry is removed after a link is made while other entries remain. Duplicate identical links are ignored. Circular links are not allowed and must warn the user.

**Acceptance criteria**

- The same source activity can appear multiple times in the link clipboard as separate entries.

- Using one clipboard entry to create a link removes only that entry.

- Remaining entries stay available for other links.

- Duplicate identical links are ignored.

- Circular dependency attempts show a warning popup and do not create the link.

**Decisions captured from Stan's answers**

- One link entry can be used once and is removed after use.

- Multiple source activities and repeated same-activity entries are allowed.

- Applying the same type/lag to all targets is not relevant.

### SPEC-009. Lead/lag adjustment arrow controls in link editor — Alpha 1.19

Origin: Original item 11 / P11 | Status: Active

**Requirement**

Lead/lag adjustment in the schedule link editor popup must provide up/down arrow controls and keyboard up/down support. Each click adjusts by one working day based on the activity calendar. Holding the arrow repeats slowly at first, then faster. Negative values represent lead and positive values represent lag, matching MS Project-style behaviour.

**Acceptance criteria**

- Lead/lag field in the link editor popup has usable up/down controls.

- Each click changes lag by one working day based on the activity calendar.

- Holding an arrow repeats the adjustment with acceleration.

- Keyboard up/down also adjusts the value.

- No minimum or maximum limit is imposed unless required by schedule validation.

- Schedule recalculates and link visuals update after changes.

**Decisions captured from Stan's answers**

- Arrow controls are only required in the link editor popup.

- Negative values are lead, positive values are lag.

### SPEC-010. Current period dropdown and month-roll workflow — Alpha 1.1

Origin: Original header item 1 | Status: Active

**Requirement**

The current period dropdown should provide a clear new-month workflow using the existing month close controls where available. New month creates/saves a month snapshot and rolls the active period forward by one. Viewing an old month remains based on saved snapshots and should open locked historical data rather than silently changing the active project period.

**Acceptance criteria**

- Dropdown clearly exposes the new-month workflow without silently changing data.

- New month creates a snapshot, saves it and rolls forward one period.

- The rolled-forward period uses current values as the new previous values for variance reference.

- Saved snapshots only are used for old-month viewing.

- Old-month popout data is locked and watermarked as previous forecast locked for editing.

- A future unlock-open-saved-month action is recorded for the saved-month tab.

**Decisions captured from Stan's answers**

- Existing new-month control is mostly correct and should be reused.

- Old-month viewing already exists under View Saved Month in the left panel.

- Old months are saved snapshots only.

### SPEC-011. KPI pill context menu and user preference state — Alpha 1.8

Origin: Original header item 2 | Status: Active

**Requirement**

Right-clicking a KPI pill or blank KPI pill strip area must show a useful KPI pill management menu. The old generic add-pill action should be removed. The menu must list all available KPI pills with ticks beside active ones so hidden/inactive pills can be re-enabled. The menu must let the user toggle KPI pills on/off, remove a pill, change pill icon and change pill colour. Reorder and rename are out of scope. Pill state is saved as a user preference.

**Acceptance criteria**

- Right-clicking blank KPI pill strip opens the pill management menu without pill-specific actions such as Remove or Change colour.

- Right-clicking an existing KPI pill opens pill management actions for that pill.

- The menu lists all available KPI pills.

- Active KPI pills show a tick.

- Hidden/inactive KPI pills can be re-enabled from this menu.

- The user can toggle KPI pill visibility.

- Pill visibility/icon/colour choices persist as user preferences.

**Decisions captured from Stan's answers**

- Pills are KPI pill boxes.

- Rename and reorder are not required.

- Right-clicking open space in the pill box area lists all available KPI pills with ticks for active ones.

### SPEC-012. Header gradient layering must not blur UI — Alpha 1.8

Origin: Original header item 3 | Status: Active

**Requirement**

The page/header gradient must remain behind the header image and UI elements. It must not draw over or blur KPI pills, icons, controls or the top of the resource drilldown panel/border.

**Acceptance criteria**

- Gradient remains visible as a header/background fade effect.

- KPI pill boxes and resource drilldown top border are sharp and not blurred by the gradient.

- Gradient layering remains correct at larger window sizes.

- Header image remains visible.

**Decisions captured from Stan's answers**

- The gradient itself is wanted; the problem is that it appears drawn over existing UI elements.

- The header image remains the main feature behind the gradient.

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

### SPEC-014. Current period dropdown arrow visual style — Alpha 1.8

Origin: Original header item 5 | Status: Active

**Requirement**

The current period dropdown arrow shape must match the dropdown arrows used in forecast tab headers and the app’s modern right-click menu style. It should not rotate when opened.

**Acceptance criteria**

- Arrow shape matches the forecast header dropdown style.

- Arrow is aligned and does not overlap text or border.

- Hover/open/closed states look intentional.

- Arrow does not rotate on open.

**Decisions captured from Stan's answers**

- Problem is arrow shape.

- Use same style as right-click menu / forecast header dropdowns.

### SPEC-015. Compact red warning bar for current active period mismatch — Alpha 1.1

Origin: Original forecast tab item 6; items 30/31 warning overlap | Status: Active

**Requirement**

Replace the legacy dropdown/reset area under the forecast views with a compact warning bar about 70 percent of the current height. Remove the two legacy filter dropdowns and reset button. The warning bar must show non-dismissible red warnings when the saved current active period is not the expected working period based on the PC calendar. July is FY01 of the next FY year; users normally work one month behind. If the PC month is June/FY11, FY10 is acceptable, FY9 or lower is a warning, and FY11/FY12 are warnings. The warning should not appear when viewing an older saved month snapshot. If multiple warnings are active, the bar shows all active warnings and expands downward as needed.

**Acceptance criteria**

- Legacy dropdowns and reset button are removed.

- Warning bar is around 70 percent of current height.

- Warning compares against PC calendar month and saved project active period.

- No warning appears for the accepted previous-month working period.

- Warnings appear when the project is too old, current month or future month.

- Warning text is red and non-dismissible.

- All active warnings are shown.

- Warning bar expands downward to fit multiple warnings.

- Warning does not appear when viewing a locked old saved month.

**Decisions captured from Stan's answers**

- Suggested warning text: Current active period is incorrect; please save and create a new month.

- Warning severity does not vary.

- Warning appears in the warning bar, not in the header current-period area.

- Warnings should feed a future warning tab/list.

- The bar shows all active warnings and expands downward to fit them.

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

### SPEC-031. Project task code editor — Alpha 1.12

Origin: Original CTC item 25 | Status: Active

**Requirement**

Right-clicking task-column content should offer Edit project task codes. The task-code editor is project-level, populated from raw data and user-created codes. Each task code has a system code and a task name. Raw-data task codes cannot be deleted or edited. Manually added task codes can be edited until raw data later uses the same code, at which point they become non-editable. Manually added codes are allowed even before raw data exists. Changes update existing forecast rows immediately.

**Acceptance criteria**

- Right-click task-code cell shows Edit project task codes.

- Editor lists raw-data task codes and manually added project task codes.

- Raw-data codes cannot be deleted and their code value cannot be edited.

- Manual codes can be added above/below and edited until raw data uses them.

- Duplicate attempted task names are handled with a (1) style suffix on the name and a duplicate-warning message.

- Editor allows visual drag/drop reorder and A-Z sort, but order affects only the editor view.

- Existing forecast rows update immediately when task name/code metadata changes.

**Decisions captured from Stan's answers**

- Task codes are stored at project level.

- User cannot delete task codes that are used by raw data.

- Visual order only; no reporting effect.

- Duplicate suffix is applied to the task name, not the system task code.

### SPEC-032. Task name, category editor and grouped heading behaviour — Alpha 1.12

Origin: Original CTC item 26 plus later rewrite | Status: Active

**Requirement**

Task codes have a System Code and user-editable Task Name. When grouped by task code, the group heading shows the system task code in the Task column and the Task Name in the Resource column. If no Task Name exists, show Unnamed task, with Unnamed task (1), etc. for duplicates. Entering a Task Name creates a matching project Category if needed and uses Task Name as the default category. Users can override Category at row level by typing directly in the Category cell; the cell should autocomplete existing project categories or create a new category by typed text. Category names are project-specific and managed in a Category Name Editor. Task Code Editor and Category Name Editor are the same popup with different tabs, opening to the relevant tab depending on trigger.

**Acceptance criteria**

- Grouped-by-task row displays task code in Task column and Task Name in Resource column.

- Category column shows the row-level Reporting Category.

- Task Name is default category when no row-level override exists.

- Row-level category override takes priority over default Task Name category.

- User can type category directly in grid with autocomplete/dropdown and create new category by typing.

- Raw imported task codes appear in Task Code Editor but do not create categories until user enters Task Name or Category.

- Category editor supports rename, delete, merge, colour and icon; active/inactive is future.

- Deleting a category that is in use clears row-level category overrides that use that category and returns affected rows to their default Task Name category.

- Reports, pivots, filters and group summaries default to Task Name but let the user choose Category where applicable.

- Task code group header icon remains and is linked to the task code.

- Category group headers show category only with resource/forecast lines beneath.

**Decisions captured from Stan's answers**

- Category is project-specific.

- Category override is row-level.

- Changing Task Name updates only rows that have no manual category override.

- Changing a category name updates rows currently using that category.

- Multiple task codes do not share category through task code relationship; category is simply a row/reporting field.

- Category active/inactive and used-count are future features.

- Deleting an in-use category clears overrides and removes the category relationship rather than blocking deletion.

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

### SPEC-051. Schedule Gantt bar and baseline sizing — Alpha 1.20

Origin: Added SCH-N01 | Status: Active

**Requirement**

Normal and critical path task bars in the schedule/Gantt view should be slightly larger. Baseline indicators should be slightly smaller and shown as slightly grey thick lines so they remain secondary to the current schedule bars. Milestones and summary bars are not affected unless their existing style requires alignment cleanup.

**Acceptance criteria**

- Normal and critical path task bars are slightly larger.

- Baseline indicators are slightly smaller than current bars.

- Baseline indicators appear as slightly grey thick lines.

- Date alignment remains correct.

- Milestones/summary bars are not unintentionally changed.

**Decisions captured from Stan's answers**

- Only normal tasks and critical path tasks.

### SPEC-052. Schedule Activity button and editable Activity panel — Alpha 1.20

Origin: Added SCH-N02 to SCH-N04 | Status: Active

**Requirement**

In the Schedule context, replace the generic Detail button with an Activity button visible all the time. The user chooses when it is open or closed. If no activity is selected and the Activity panel is opened, it shows an empty state. When an activity is selected, the Activity panel shows one selected activity only and all supported activity information: activity number, name, dates, duration, predecessors, successors, calendar, imposed dates/constraints, baseline/progress and other supported schedule properties. Any field editable in the main schedule window is editable here and changes reflect both ways immediately. Predecessor and successor tables use the shared grid control, allow add/delete, and invalid links/constraints show inline validation before recalculation.

**Acceptance criteria**

- Schedule shows Activity button instead of Detail button.

- Activity button is always visible in schedule context.

- Opening Activity with no selected activity shows an empty state.

- Panel supports one selected activity only.

- All main-window editable fields can be edited in Activity panel.

- Changes update schedule table and Gantt immediately.

- Predecessor/successor tables use shared grid control.

- User can add/delete predecessor and successor links.

- Invalid links/constraints show inline validation before schedule recalculation.

**Decisions captured from Stan's answers**

- Resource drilldown collapses when entering Schedule; Activity panel is the detail mechanism.

- No Apply/Save button; immediate updates.

- No selected activity shows empty state.

### SPEC-053. Saved CurrentPeriod authority and no silent auto-overwrite — Alpha 1.1

Origin: Original ChatGPT items 30 and 31 | Status: Active

**Requirement**

The app must not silently overwrite the saved project CurrentPeriod based only on DateTime/PC date. Opening any project uses the saved CurrentPeriod for locks, current-month actuals, reports and snapshots. The app may calculate expected period from PC date for warnings only. Changing CurrentPeriod happens only through explicit user workflows such as month rollover. The month-rollover popup should confirm that the user is ready to save the project file and set up a new month. Imported raw data must never advance CurrentPeriod automatically.

**Acceptance criteria**

- Opening an old project does not change saved CurrentPeriod.

- Warnings may appear but no period changes occur without user action.

- All period-sensitive behaviour uses saved/confirmed CurrentPeriod.

- New blank, sample and imported projects all follow the rule.

- Raw data period values are ignored for period advancement until user performs next-month workflow.

- Month rollover confirmation asks the user to confirm they are ready to save the project file and set up a new month.

- If saved CurrentPeriod is not found, show a warning.

**Decisions captured from Stan's answers**

- Merge original items 30 and 31.

- Warning appears in warning bar.

- Month-close snapshots are read-only when viewed from old months.

- Separate viewing period is a future feature.

- Rollover popup text/action: confirm you are ready to save the project file and set up a new month.

### SPEC-054. Import auto-create forecast line preview and locked month protection — Alpha 1.2

Origin: Original ChatGPT item 32 | Status: Active

**Requirement**

Before import auto-creates forecast lines, show a preview controlled by a saved preference. Preview lists new task/resource combinations with task code, resource/manual name, project code, amount, transaction count, category and source. User can edit Manual Name only. If the user cancels auto-create, raw transactions are not imported and new combinations go to an unmatched list. Locked months are all months before CurrentPeriod and cannot be changed by typing, deleting, paste, drilldown changes or bulk/monthly commands. Paste operations involving locked cells reject the entire paste.

**Acceptance criteria**

- Saved preference controls whether auto-create preview appears.

- Preview shows required columns.

- User can edit Manual Name only in preview.

- Cancel stops raw transaction import and routes new combinations to unmatched list.

- Locked months are all months before CurrentPeriod.

- Locked months block all edit paths.

- Paste including locked cells rejects the entire paste.

**Decisions captured from Stan's answers**

- Admin override and locked-month edit message are future features.

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

### EXCEL-001. Global Excel-style grid standard — Alpha 1.5

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

All editable and read-only grids should behave consistently like an Excel-style spreadsheet unless a documented exception exists. Stan clarified all grids should be Excel-like, including schedule where practical.

**Acceptance criteria**

- EXCEL-001-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.5

### EXCEL-002. Single-click active cell, typing overwrite and F2/double-click edit — Alpha 1.5

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Single click selects/activates only. Typing replaces the current value. Double-click or F2 edits inside the value.

**Acceptance criteria**

- EXCEL-002-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.5

### EXCEL-003. Standard keyboard navigation — Alpha 1.5

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Arrow, Enter, Shift+Enter, Tab, Shift+Tab, Esc, Delete, Backspace, Ctrl+C/X/V/A, Home/End and F2 should behave consistently where applicable.

**Acceptance criteria**

- EXCEL-003-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.5

### EXCEL-004. Active row, range selection and Alt-hover temporary active cell — Alpha 1.5

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

First click sets the active row/cell; active row feeds forecast/resource/schedule side panels. Shift/Ctrl/click-drag selection should work. Preserve existing Alt-hover behaviour: while Alt is held, hovering updates the detail panel only as a temporary active context; releasing Alt restores the previous active row/cell.

**Acceptance criteria**

- EXCEL-004-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.5

### EXCEL-005. Excel-compatible copy/paste, Ctrl+Enter and protected-cell warning — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Copy/paste supports Excel tab-delimited data. One copied cell fills a selected range. Ctrl+Enter fills selected editable cells. If paste includes protected/locked/read-only cells, warn the user and ask whether to proceed. If the user proceeds, paste into unlocked/editable cells only and show a skipped protected-cell count; protected cells must not be silently changed.

**Acceptance criteria**

- EXCEL-005-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-006. Delete/Backspace clear cells and recalculate — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Delete and Backspace clear selected editable cells using zero-as-blank display where configured and trigger normal recalculation/dirty-state updates.

**Acceptance criteria**

- EXCEL-006-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-007. Locked and read-only protection across all edit paths — Alpha 1.2

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Closed forecast months and protected cells are protected from typing, paste, cut, delete, curve tools, drilldown editing and bulk edit paths.

**Acceptance criteria**

- EXCEL-007-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.2

### EXCEL-008. Recalculate after every edit path — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Single edits, type overwrite, paste, cut, delete, row add/delete, curve changes, resource edits and schedule edits refresh dependent totals and state.

**Acceptance criteria**

- EXCEL-008-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-009. Fill handle / drag-fill — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Include Excel-style fill handle/drag-fill now. Pattern-based fill can be future unless needed for basic operation.

**Acceptance criteria**

- EXCEL-009-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-010. Undo/redo for grid edits — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Include undo/redo for common grid actions: single-cell edit, paste, cut, delete, row add/delete, and potentially curve grid edits.

**Acceptance criteria**

- EXCEL-010-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-011. Active cell/range visual state — Alpha 1.5

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Active cell, active row, selected range, current row, locked, read-only and calculated cells must have distinct visual states.

**Acceptance criteria**

- EXCEL-011-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.5

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

### EXCEL-014. Validation feedback inside grid — Alpha 1.6

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Invalid edits do not fail silently. Show clear validation for invalid numbers/dates, text in numeric cells, locked edits, calculated cells and paste issues.

**Acceptance criteria**

- EXCEL-014-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.6

### EXCEL-015. Schedule grid follows Excel-like selection model — Alpha 1.19

Origin: Excel-style grid behaviour standard | Status: Active

**Requirement**

Schedule grid should use the same Excel-like selection principles with activity-row context, visible selection, multi-row selection and Gantt sync.

**Acceptance criteria**

- EXCEL-015-AC1: Behaviour is testable from the UI and consistent across all in-scope grids unless a documented exception exists. — Alpha 1.19

# 5. Grid performance and reusable architecture tasks

These tasks are kept separate from normal SPEC numbering. They cover grid performance, shared grid controls, reusable UI controls and anti-duplication architecture rules.

### GRID-001. Performance diagnostics and counters — Alpha 1.3

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Add diagnostics for grid first draw, resize duration, overlay rebuilds, selection update duration, group-header render count, paste duration and schedule comparison load time.

**Acceptance criteria**

- GRID-001-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-002. Forecast grid first draw, resize and scroll performance — Alpha 1.3

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Forecast grid must draw quickly and resize/scroll without freezing, especially with all forecast month columns visible. Column resize slowness is confirmed as affecting the forecast grid.

**Acceptance criteria**

- GRID-002-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-003. Column sizing and virtualization — Alpha 1.3

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Stop broad Auto sizing during normal operation. Use fixed/user-controlled widths. Enable/test column virtualization and document exceptions.

**Acceptance criteria**

- GRID-003-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-004. Forecast year/month overlay rebuild control — Alpha 1.3

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Overlays rebuild only when geometry changes, not every layout update. Avoid flicker and unnecessary child recreation.

**Acceptance criteria**

- GRID-004-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.3

### GRID-005. Forecast group header render optimisation — Alpha 1.4

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Group header totals must be precomputed or cached, not recalculated inside visual render paths.

**Acceptance criteria**

- GRID-005-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-006. Selection and hover visual performance — Alpha 1.4

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Avoid full visual-tree scans on every mouse move/selection/current-cell change. Use shared states/styles/targeted updates.

**Acceptance criteria**

- GRID-006-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-007. Management resource grid performance — Alpha 1.4

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Stop repeated Auto sizing/UpdateLayout; use shared width model and fast scrolling/editing.

**Acceptance criteria**

- GRID-007-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-008. Schedule performance profile — Alpha 1.4

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Schedule table/Gantt must be tested with large schedules and remain responsive.

**Acceptance criteria**

- GRID-008-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.4

### GRID-009. Shared grid framework and profiles — Alpha 1.21

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Introduce ProjectDataGrid/ExcelDataGrid-style shared framework with Forecast, ReadOnlyLedger, Pivot, ManagementResource and Schedule profiles.

**Acceptance criteria**

- GRID-009-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.21

### GRID-010. Shared grid behaviours — Alpha 1.21

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Implement selection, copy/cut/paste/delete, typing overwrite, F2, keyboard nav, summaries, context menus, filters, column state, locked/read-only styles and validation once.

**Acceptance criteria**

- GRID-010-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.21

### GRID-011. Reusable UI controls — Alpha 1.22

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Create reusable column headers, filter menu, context provider, view strip, icon picker, warning bar, period column factory, band overlay, dialog shell, metric card, command groups, pan behaviour, add-row control, validation indicator and code-mapping editor.

**Acceptance criteria**

- GRID-011-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.22

### GRID-012. Architecture guardrail — Alpha 1.21

Origin: Grid Performance and Reusable Control Architecture | Status: Active

**Requirement**

Do not add more one-off grid/control logic to MainWindow. If behaviour applies to more than one grid/screen, extract to shared framework/control.

**Acceptance criteria**

- GRID-012-AC1: Outcome is verifiable by smoke test, code review or consistent reuse across affected grids. — Alpha 1.21

# 6. Original spec coverage map

Use this table to confirm every item from the original raw list is covered, merged, removed or moved to EXCEL/GRID numbering.

| Original item | Original / supplied topic | New ID(s) | Status / note |
| --- | --- | --- | --- |
| P01 / original pre-list 1 | Column resizing slow | GRID-001 to GRID-008; EXCEL-013 | Covered by grid performance architecture |
| P02 / original pre-list 2 | Body text smaller | SPEC-001 | Active |
| P03 / original pre-list 3 | Header expand/collapse icon/border | SPEC-002 | Active |
| P04 / original pre-list 4 | Deleted forecast variance not recalculating | SPEC-003; EXCEL-006/008 | Merged with original 16 |
| P05 / original pre-list 5 | Line through heading picture | SPEC-004 | Active |
| P06 / original pre-list 6 | Right-click add management task anywhere | SPEC-005; EXCEL-012 | Active |
| P07 / original pre-list 7 | Resource tab type overwrite | EXCEL-002/006; SPEC-048 | Covered by Excel grid standard |
| P08 / original pre-list 8 | Schedule selected rows | SPEC-006; EXCEL-015 | Active |
| P09 / original pre-list 9 | Schedule comparison freeze | SPEC-007 | Active |
| P10 / original pre-list 10 | Link clipboard same activity | SPEC-008; EXCEL-016 legacy | Active |
| P11 / original pre-list 11 | Lead/lag arrows | SPEC-009 | Active |
| 1 | Current period dropdown | SPEC-010; SPEC-053 | Active |
| 2 | Header KPI pill menu | SPEC-011 | Active |
| 3 | Gradient blurring UI | SPEC-012 | Active |
| 4 | Right-click header icons | SPEC-013 | Active |
| 5 | Current period arrow | SPEC-014 | Active |
| 6 | Warning bar / wrong period | SPEC-015 | Active |
| 7 | Right-click tab icons | SPEC-016; SPEC-013 | Active |
| 8 | Light border under tabs | SPEC-017 | Active |
| 9 | Sort/filter/group compact | SPEC-018 | Active |
| 10 | Sort/filter excludes forecast headers | SPEC-019 | Active |
| 11 | Quick filter under Filter | SPEC-020 | Active |
| 12 | New view rename mode | SPEC-021 | Active |
| 13 | Commit new view name / pill size | SPEC-022 | Active |
| 14 | Blank item | Removed | Stan confirmed accidental/not needed |
| 15 | Right-click resource icon | SPEC-023; SPEC-013 | Reworded as group-header icon |
| 16 | Forecast deletion updates columns | SPEC-003 | Duplicate of P04 |
| 17 | Dollar symbols | SPEC-024 | Active |
| 18 | Column width/million abbreviation | SPEC-025 | Active |
| 19 | Month/FY headers | SPEC-026 | Active |
| 20 | Calendar borders | SPEC-027 | Merged with 21 |
| 21 | Borders over scrollbar | SPEC-027 | Merged with 20 |
| 22 | Add row option | SPEC-028 | Active |
| 23 | Show zeros as blank | SPEC-029 | Active |
| 24 | Header gradient light grey | SPEC-030 | Active |
| 25 | Task code editor | SPEC-031 | Active |
| 26 | Task/category default override | SPEC-032 | Rewritten; active |
| 27 | Right-click panning snap | SPEC-033 | Active |
| 28 | Category wrapping issue | Removed | Stan marked remove |
| 29 | Blank item | Removed | Stan marked remove |
| 30 | Stop CurrentPeriod overwrite | SPEC-053 | Merged with 31 |
| 31 | Saved period controls behaviour | SPEC-053 | Merged with 30 |
| 32 | Import auto-create/locked months | SPEC-054 | Active |
| 33 | Avoid MainWindow tangling | GRID-012 | Architecture guardrail |
| RDD items | Resource drilldown/detail panel | SPEC-034 to SPEC-037 | Active |
| MFG new items | Forecast group menu/indent | SPEC-038 to SPEC-039 | Active |
| CURVE items | Curve workflow and UI | SPEC-040 to SPEC-045 | Active |
| CURVE-12/13 clarified | Resource rate logic | SPEC-046 | Moved to resource management |
| RT items | Resource tab automation | SPEC-047 to SPEC-048 | Active |
| SIDE items | Side panel collapse/pin | SPEC-049 | Active |
| PIVOT items | Pivot builder | SPEC-050 | Active |
| Schedule new items | Gantt sizing / Activity panel | SPEC-051 to SPEC-052 | Active |
| New bugs | Blue column border / drilldown artifact | SPEC-055 to SPEC-056 | Active |

# 7. Second-round questions status

All second-round questions supplied by Stan have been answered and folded into the relevant requirements, acceptance criteria and decisions. No open second-round questions remain in this Phase 1 spec.

# 8. Future-feature notes captured but not active for this round

- Warning tab/list fed by forecast warning bar.

- Separate viewing period that does not change saved CurrentPeriod.

- Unlock/open saved historical month for editing from Saved Month tab.

- Admin/override mode for locked-month editing.

- Locked-month edit warning message content, if not covered by global validation messaging.

- Category active/inactive state and used-count display.

- AND filter engine for combining quick filters with normal filters, if not already implemented.

- Pattern-based fill beyond basic fill handle.