# Alpha 1.21 — Shared Grid Framework Foundation

## Recommended Codex Settings

- Model: **Codex GPT-5.5**
- Reasoning: **xhigh planning, then high build**
- Use the master spec for context, but implement only the tasks listed in this Alpha file.
- Do not implement tasks from other Alpha files unless required to satisfy the acceptance criteria here.

## Source Files to Read

- `../master/ProjectCostForecast_Master_Spec.md`
- `../images/image_index.md`
- This file: `alphas/Alpha_1_21_Shared_Grid_Framework_Foundation.md`

## Alpha Scope

| Task ID | Description Title | Complexity | Summary |
|---|---|---|---|
| GRID-009 | Shared grid framework and profiles | Very High | Introduce ProjectDataGrid/ExcelDataGrid-style shared framework with Forecast, ReadOnlyLedger, Pivot, ManagementResource and Schedule profiles. |
| GRID-010 | Shared grid behaviours | Very High | Implement selection, copy/cut/paste/delete, typing overwrite, F2, keyboard nav, summaries, context menus, filters, column state, locked/read-only styles and validation once. |
| GRID-012 | Architecture guardrail | Very High | Do not add more one-off grid/control logic to MainWindow. If behaviour applies to more than one grid/screen, extract to shared framework/control. |

## Out of Scope

- Any task not listed in the Alpha Scope table.
- Major architecture changes unless the Alpha Scope explicitly contains GRID architecture tasks.
- Business-rule changes not described in the included requirements or acceptance criteria.

## Detailed Requirements

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