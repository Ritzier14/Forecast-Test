# Workbook Analysis: `1.Mar 26.xlsm`

## High-level workbook purpose

The workbook is a project cost-to-complete and financial-reporting workbook. It combines manually entered future forecast values with actual transaction data, then rolls those into project-code/category summaries, variance reports, contingency tracking, and forecast accuracy.

The workbook is not just a simple cost report. It is doing four jobs:

1. **Cost-to-complete forecast entry** - the `CTC` sheet is the main working sheet.
2. **Actual cost import** - the `Raw Data Entry` sheet stores transaction-level cost lines.
3. **Financial reporting** - the `Report` and `Financial Report` sheets aggregate the forecast and actuals.
4. **Control / maintenance tools** - macros add rows and repair formatting.

## Sheets found

| Sheet | Range | Role | Notes |
|---|---:|---|---|
| `CTC` | `A1:BS125` | Main cost-to-complete forecast grid | 1,904 formulas, 41 merged ranges, 13 conditional-format regions. Contains the central forecast rows and budget/variance columns. |
| `Report` | `B2:K77` | Printable/project report | Pulls current-period, FY, contingency, and category summaries. Contains styled report sections. |
| `Contingency` | `B1:G20` | Contingency ledger | Monthly contingency expended, remaining, proposed expenditure, reason, status. |
| `Piviot Table` | `A1:H12` | Pivot view of actuals | Pivot groups raw data by task/manual name and FY-period. Sheet name is misspelled in workbook. |
| `Forecast Accuracy` | `B2:E25` | Forecast vs actual tracking | Excel table with Month, Forecast, Actuals, percentage. |
| `Financial Report` | `A1:H57` | Financial summary | Uses dynamic array formulas and SUMIFS by project code/category. |
| `Check` | `A1:B6` | Unique task/manual-name checker | Uses LET, HSTACK, FILTER, UNIQUE, SORT. |
| `Raw Data Entry` | `A1:V1112` | Transaction source table | 63 populated transaction rows in the uploaded file, though the range allows many more rows. |

## Workbook tables

| Table | Sheet | Range | Columns |
|---|---|---:|---|
| `Table16` | `Report` | `C9:G14` | As at current period, FY26, FY27, FY28, Totals FY26 onwards |
| `Table1` | `Forecast Accuracy` | `B2:E25` | Month, Forecast, Actuals, % |

## Macro/buttons found

The `CTC` sheet has two form-control buttons:

| Button text | Macro | Purpose |
|---|---|---|
| `Fix Formatting` | `FixFormattingFromRow12` | Reapplies formatting from row 12 down to the last used row. |
| `Add New row Above` | `InsertRelativeRow` | Inserts a new row above the active row, copies the row above, and clears constants while leaving formulas. |

The program version should not need these exact macros. Their intent becomes app commands:

- `Add forecast line above/below selected line`
- `Clone line structure`
- `Recalculate totals`
- `Apply display style automatically`

## Key source data: `Raw Data Entry`

The raw transaction columns are:

1. FY-Period
2. Task Number
3. Period
4. Doc Date
5. Units
6. Unit Rate
7. Amount
8. Cost Ledger
9. Cost Account
10. Project Code
11. Parent Project Code
12. Resource Code
13. Resource Description
14. Source
15. PO Number
16. PO Comments
17. Supplier Name
18. Narrative 1
19. Narrative 2
20. Narrative 3
21. Who
22. Manual Name

The uploaded data contains 63 populated transaction lines. Total raw Amount is `27,695`. Total Units is `139`.

### Actual cost source mix

| Source | Meaning inferred | Count |
|---|---|---:|
| `TC` | Time/cost posting/person cost | 59 |
| `AP` | Accounts payable/contractor payment | 4 |

### Resources found in raw data

| Resource / Manual Name | Transactions | Amount | Units | Task(s) | Source(s) |
|---|---:|---:|---:|---|---|
| Stanley Drake | 39 | 15,000 | 100 | WA57102001 | TC |
| Flex Projects L / Contractors Payments | 4 | 7,420 | 4 | WA57102001 | AP |
| Scott Ryan | 14 | 3,875 | 25 | WA57102002 | TC |
| Gregory Parkin | 5 | 1,200 | 8 | WA57102002 | TC |
| Daniel Robert Drummond | 1 | 200 | 2 | WA57102001 | TC |

Important: AP transactions have `Resource Description = Contractors Payments`, but `Manual Name = Flex Projects L`. The program must show these costs under `Flex Projects L`, not under a generic `Contractors Payments` resource.

## Main `CTC` sheet structure

The `CTC` sheet starts with:

- Project title: `WW4378 - NPWTP - FISH SCREENS (LAKE)`
- Current period: `26-09`
- Phase table:
  - Desgin: 2026-01-01 to 2026-08-30
  - Construction Tender: 2026-09-01 to 2027-04-06
  - Construction: 2027-10-01 to 2028-02-29
  - Closeout: 2028-03-01 to 2028-06-01

There is a title inconsistency: `Report` says `WW4378 - NPWTP - River Intake`, while `CTC` says `WW4378 - NPWTP - FISH SCREENS (LAKE)`. The program should store a single project title and flag mismatches during import.

### CTC row layout

Rows 13 to 75 are the main forecast line area. Rows 50 to 75 are mostly blank zero-ready rows. Row 76 is a total row.

Core columns:

| Column | Workbook header | Program field |
|---|---|---|
| A | Task | `TaskNumber` |
| B | Who | `ResourceName` |
| C | Project Code | `ProjectCode` / category |
| D | Format helper | `FormatGroup` / group order |
| E | Cost to date (B) | `CostToDate` from raw actuals |
| F | Total budget variance (J) | quick variance link |
| G | Planned cost vs budget (F) | quick variance link |
| H | Total cost for month | current-period actual cost |
| I | Last Month Forecast | last-period forecast |
| J | Var Actual vs forecast (MO) | variance between actual and forecast for month |
| K:AT | Monthly forecast values | month-by-month CTC forecast |
| AU | TOTAL Forecast (CTC) / A | total future forecast amount |
| AV | Cost to Date / B | actual cost to date |
| AW | Planned cost (FCC) / C=A+B | forecast final cost |
| AY | Last Month Planned Cost / D | last snapshot planned cost |
| AZ | Last Month Forecast / E | last snapshot forecast |
| BA | F = D - C | variance from last planned cost |
| BB | G | month forecast variance |
| BC | Budget / H | current budget |
| BD | J = H - C | total budget variance |
| BE:BG | Comments | comments for variances |

### CTC calculations to translate into C#

The program should implement these in services instead of in spreadsheet formulas.

#### Cost to date

Spreadsheet pattern:

```text
SUMIFS('Raw Data Entry'!Amount, 'Raw Data Entry'!Task Number, CTC.Task, 'Raw Data Entry'!Manual Name, CTC.Who)
```

Program rule:

```text
ForecastLine.CostToDate = sum(Transaction.Amount)
where Transaction.TaskNumber == ForecastLine.TaskNumber
and normalised(Transaction.ManualName or Who or ResourceDescription) == normalised(ForecastLine.ResourceName)
```

#### Current month cost

Spreadsheet pattern:

```text
SUMIFS('Raw Data Entry'!Amount, Task, CTC.Task, Manual Name, CTC.Who, FY-Period, CurrentPeriod)
```

Program rule:

```text
ForecastLine.CurrentMonthCost = sum matching transactions where FYPeriod == Project.CurrentPeriod
```

#### CTC total forecast

Spreadsheet pattern:

```text
SUM(K:AT for the forecast line)
```

Program rule:

```text
ForecastLine.TotalForecastCtc = sum(ForecastLine.MonthlyForecasts.Amount)
```

#### Planned cost / FCC

Spreadsheet pattern:

```text
AW = AU + AV
```

Program rule:

```text
ForecastLine.PlannedCostFcc = ForecastLine.TotalForecastCtc + ForecastLine.CostToDate
```

#### Budget variance

Spreadsheet pattern:

```text
BD = BC - AW
```

Program rule:

```text
ForecastLine.TotalBudgetVariance = ForecastLine.Budget - ForecastLine.PlannedCostFcc
```

#### Last month / to-date variance

Spreadsheet pattern:

```text
BA = AY - AW
```

Program rule:

```text
ForecastLine.VarianceLastMonthToDate = ForecastLine.LastMonthPlannedCost - ForecastLine.PlannedCostFcc
```

#### Month forecast variance

Spreadsheet pattern:

```text
BB = AZ - H
```

Program rule:

```text
ForecastLine.MonthForecastVariance = ForecastLine.LastMonthForecast - ForecastLine.CurrentMonthCost
```

## Project categories found in CTC

The main category values are:

- Close Out
- Compliance
- Construction
- Contigency (misspelled in workbook)
- Design Consultants
- Internal Staff Costs
- Iwi Engagement
- Project Management

The program should keep the workbook spelling during import for auditability, but provide a category alias table later so `Contigency` can display as `Contingency` without losing the original imported value.

## Report sheet logic

The report sheet produces:

1. Spend vs AP/LTP budget by FY.
2. Total project contingency, total expended, proposed expenditure, total remaining.
3. Cost vs original budget by category.
4. Budget variance table and comments.
5. Active risks / financial events.
6. Plugged rates.

The desktop app should rebuild these as live report views, not printed spreadsheet blocks. The report needs drilldown links from every number back to source forecast lines and transactions.

## Pivot table logic

The pivot table uses raw data and groups by:

- Row fields: `Task Number`, `Manual Name`
- Column field: `FY-Period`
- Value field: Sum of `Amount`

The desktop equivalent should be a resource/task actuals pivot in the Resources module.

## Known workbook issues to fix in the program

1. Some workbook formulas render as `#VALUE!` in parts of the report/CTC view depending on formula engine and dynamic array handling.
2. Project title differs between `CTC` and `Report`.
3. `Piviot Table`, `Desgin`, `Planed`, `Contigency`, and other spelling issues should be corrected in the app UI while preserving imported raw text.
4. Monthly period labels and Excel serial dates should be modelled explicitly as `ForecastPeriod` objects. Do not rely on column positions.
5. Raw Data Entry range extends to row 1112, but only 63 rows are populated in this file.
6. AP cost lines need special handling because `Resource Description` is generic; `Manual Name` is the practical resource key.

## What the app must improve beyond Excel

The key improvement is traceability:

- In Excel the user sees a rolled-up resource line.
- In the app the user clicks the resource line and immediately sees every raw transaction that makes up the actual cost.
- Every reported total should answer: `what lines make up this number?`

That traceability is the reason for moving from spreadsheet to program.
