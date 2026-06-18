# Detailed Rebuild Specification for Codex

## 1. Product name

Working title: **Project Cost Forecast**

Version in this package: **Alpha 1.0**

## 2. Product purpose

Recreate the uploaded Excel macro workbook as a PC program. The program is for project cost forecasting, actual cost tracking, contingency reporting, and financial variance reporting.

The app must be more auditable and easier to maintain than the spreadsheet. It should keep the spreadsheet's working logic but avoid fragile Excel formulas, row-copy macros, hidden ranges, and accidental formula breaks.

## 3. Primary user workflow

1. User opens a project.
2. User sees the CTC forecast grid.
3. User edits future monthly forecast values for each task/resource/category row.
4. App imports or stores actual cost transactions.
5. App calculates:
   - cost to date
   - current month actual cost
   - total forecast CTC
   - planned final cost / FCC
   - last month variance
   - budget variance
6. User clicks a resource line.
7. App shows every raw transaction that has come into that resource.
8. User can explain any total by opening its drilldown.
9. User can generate the report view without copying formulas around.

## 4. Key missing feature requested by the user

The spreadsheet did not provide a clean resource drilldown. The new program must.

### Requirement: Resource cost drilldown

When the user clicks a resource/person/supplier, show:

- selected resource name
- task number(s)
- total raw actual cost
- transaction count
- total units
- average rate
- every transaction line making up the total
- FY-period
- document date
- units
- unit rate
- amount
- source
- PO number
- resource code
- cost ledger/account
- narrative text
- supplier/manual name

### Matching logic

A forecast row matches actual transactions by:

```text
Transaction.TaskNumber == ForecastLine.TaskNumber
AND
normalised(Transaction.ManualName OR Transaction.Who OR Transaction.ResourceDescription) == normalised(ForecastLine.ResourceName)
```

Use `ManualName` first because AP transactions can have generic `Resource Description = Contractors Payments` while `ManualName` contains the real supplier/resource.

### Resource summary mode

The app should also have a Resources view grouped by resource across all tasks:

```text
ResourceSummary = group Transactions by normalised LedgerResourceName
LedgerResourceName = ManualName if present, otherwise ResourceDescription
```

Selecting a resource summary shows all transaction lines for that resource, across all task numbers.

## 5. Initial app architecture

Alpha 1.0 is intentionally single-project WPF:

```text
ProjectCostForecast.App
  Data/SampleData.json
  Models/
  Services/
  ViewModels/
  MainWindow.xaml
```

Later versions should split into:

```text
ProjectCostForecast.Domain
ProjectCostForecast.Application
ProjectCostForecast.Infrastructure.Json
ProjectCostForecast.Infrastructure.ExcelImport
ProjectCostForecast.Wpf
ProjectCostForecast.Tests
```

Do not split too early if it slows feature delivery.

## 6. Core domain model

### ProjectDataset

Represents the whole opened project.

Fields:

- `Header`
- `Phases`
- `ForecastPeriods`
- `ForecastLines`
- `Transactions`
- `ContingencyEntries`
- `CategorySummaries`

### ProjectHeader

Fields:

- `ProjectTitle`
- `ReportTitle`
- `CurrentPeriod`
- `SourceWorkbook`
- `ImportNotes`

Validation rule:

- If imported workbook title differs across sheets, show an import warning.

### ForecastLine

Equivalent to one working CTC row.

Fields:

- `RowNumber`
- `TaskNumber`
- `ResourceName`
- `ProjectCode`
- `FormatGroup`
- `CostToDate`
- `CurrentMonthCost`
- `LastMonthForecast`
- `MonthForecastVariance`
- `TotalForecastCtc`
- `CostToDateSummary`
- `PlannedCostFcc`
- `LastMonthPlannedCost`
- `VarianceLastMonthToDate`
- `Budget`
- `TotalBudgetVariance`
- comments
- `MonthlyForecasts`

### MonthlyForecast

Fields:

- `PeriodLabel`
- `PeriodStartDate`
- `Amount`

Future enhancement:

- Add `IsLocked`, `Source`, `LastEditedBy`, `LastEditedAt`, `Reason`.

### CostTransaction

Equivalent to a row from Raw Data Entry.

Fields:

- `FyPeriod`
- `TaskNumber`
- `Period`
- `DocDate`
- `Units`
- `UnitRate`
- `Amount`
- `CostLedger`
- `CostAccount`
- `ProjectCode`
- `ParentProjectCode`
- `ResourceCode`
- `ResourceDescription`
- `Source`
- `PoNumber`
- `PoComments`
- `SupplierName`
- `Narrative1`
- `Narrative2`
- `Narrative3`
- `Who`
- `ManualName`

Computed:

```text
LedgerResourceName = ManualName if not blank else ResourceDescription
```

### ContingencyEntry

Fields:

- `Date`
- `ContingencyExpended`
- `RemainingContingency`
- `ProposedExpenditure`
- `Reason`
- `Status`

### CategorySummary

Fields:

- `ProjectCode`
- `TotalForecast`
- `CostToDate`
- `PlannedCost`
- `Budget`
- `TotalBudgetVariance`
- `MonthForecastVariance`

## 7. Calculation service specification

Add a `CalculationService` after Alpha 1.0 compiles.

### RecalculateForecastLine(ForecastLine line, IEnumerable<CostTransaction> transactions, string currentPeriod)

Rules:

```csharp
var matchingTransactions = transactions.Where(t => MatchesForecastLine(t, line));
line.CostToDate = matchingTransactions.Sum(t => t.Amount);
line.CostToDateSummary = line.CostToDate;
line.CurrentMonthCost = matchingTransactions.Where(t => t.FyPeriod == currentPeriod).Sum(t => t.Amount);
line.TotalForecastCtc = line.MonthlyForecasts.Sum(m => m.Amount);
line.PlannedCostFcc = line.TotalForecastCtc + line.CostToDate;
line.VarianceLastMonthToDate = line.LastMonthPlannedCost - line.PlannedCostFcc;
line.MonthForecastVariance = line.LastMonthForecast - line.CurrentMonthCost;
line.TotalBudgetVariance = line.Budget - line.PlannedCostFcc;
```

### RecalculateCategorySummaries

Group forecast lines by `ProjectCode`.

```csharp
TotalForecast = sum(line.TotalForecastCtc)
CostToDate = sum(line.CostToDateSummary)
PlannedCost = sum(line.PlannedCostFcc)
Budget = sum(line.Budget)
TotalBudgetVariance = sum(line.TotalBudgetVariance)
MonthForecastVariance = sum(line.MonthForecastVariance)
```

### RecalculateReportSummary

The report needs the following values:

- Spent to date by FY
- Cost to complete by FY
- Planned cost by FY
- Budget by FY
- Variance by FY
- Contingency total, expended, proposed, remaining
- Cost vs original budget by category
- Comments by category
- Active risks/financial events
- Plugged rates

Do not implement this as fixed spreadsheet coordinates. Use domain objects.

## 8. UI specification

### Shell

Desktop layout:

- dark top header with app title, project title, current period
- KPI cards across the top
- main tabbed working area
- right-side resource drilldown panel

### Main tabs

#### CTC Forecast

Primary working grid.

Columns:

- Task
- Resource
- Category
- CTD
- Month Cost
- Last Forecast
- Month Var
- CTC
- FCC
- Budget
- Budget Var

Interactions:

- Click a row: updates resource drilldown panel.
- Search box filters task/resource/category.
- Project code/category filter.
- Actual cost only checkbox.
- Later: editable monthly forecast cells.

#### Resources

Grouped resource ledger view.

Columns:

- Resource
- Resource codes
- Task list
- Sources
- Transaction count
- Units
- Amount
- Average rate

Interactions:

- Click a resource: right panel shows all transaction lines for that resource across all tasks.

#### Raw Transactions

Flat imported transaction table.

Columns:

- FY
- Task
- Date
- Resource
- Units
- Rate
- Amount
- Source
- Resource Code
- Narrative
- Narrative 2

Interactions:

- Use the global search and period filter.
- Later: import, validate, error highlights.

#### Category Report

Spreadsheet `Financial Report` equivalent.

Columns:

- Category
- Total Forecast
- Cost to Date
- Planned Cost
- Budget
- Budget Var
- Month Var

Interactions:

- Later: click category to drill into forecast lines and transactions.

#### Contingency

Spreadsheet `Contingency` equivalent.

Columns:

- Date
- Expended
- Remaining
- Proposed
- Reason
- Status

### Right-side resource drilldown panel

Always visible.

Shows:

- resource title
- transaction count
- total raw cost
- total units
- average rate
- table of matching transactions
- monthly forecast table for selected forecast row

This is the main requested improvement. Do not remove it.

## 9. Data import/export roadmap

### Alpha 1.0

- Load `SampleData.json` extracted from workbook.

### Alpha 1.1

- Save edited project back to JSON.
- Add save-as and open project file.

### Alpha 1.2

- Import raw transaction CSV.
- Map columns manually if names differ.
- Preview import before applying.

### Alpha 1.3

- Import `.xlsx/.xlsm` using a dedicated importer.
- Detect sheets and columns by header names, not by absolute coordinates only.
- Import warnings for misspelled headings and mismatched project titles.

### Alpha 1.4

- Export report to Excel and PDF.

## 10. Validation rules

### ForecastLine

- Task Number required.
- Resource Name required.
- Project Code required.
- Monthly forecast values must be numeric.
- Budget may be zero but should trigger warning if category requires budget.

### Transaction

- FY-Period required.
- Task Number required.
- Amount required.
- Resource key warning if both Manual Name and Resource Description are blank.
- Source should be one of known source codes, initially AP or TC.

### Import warnings

- Mismatched project titles.
- Unknown project category.
- Duplicate resource codes pointing to multiple names.
- Manual Name blank on AP lines.
- Date serials that cannot be converted.
- Negative or zero units with non-zero amount.

## 11. Audit trail specification

Every user edit should eventually create an audit event.

Fields:

- `AuditId`
- `EntityType`
- `EntityId`
- `FieldName`
- `OldValue`
- `NewValue`
- `ChangedBy`
- `ChangedAt`
- `Reason`
- `Source`

Required audited changes:

- monthly forecast value changes
- budget changes
- resource name changes
- task number changes
- project category changes
- contingency entries
- manual overrides
- imported transactions accepted/deleted

## 12. Version roadmap

### Alpha 1.0 - Included in this zip

- Starter WPF app.
- JSON seed data.
- CTC forecast grid.
- Resource drilldown.
- Resources tab.
- Raw transactions tab.
- Category report tab.
- Contingency tab.

### Alpha 1.1 - Make it reliable

- Fix compile/runtime issues.
- Add calculation service.
- Add unit tests.
- Make all totals recalculate from source data.
- Add save/load JSON.

### Alpha 1.2 - Make it editable

- Editable monthly forecast grid.
- Add row / duplicate row / delete row.
- Manual comments.
- Recalculate totals live.
- Dirty-state warning before close.

### Alpha 1.3 - Import actuals

- CSV import wizard.
- Column mapping screen.
- Import validation.
- Duplicate detection.
- Import history.

### Alpha 1.4 - Report module

- Rebuild Report sheet as a live report view.
- Export report to Excel/PDF.
- Add active risks/financial events and plugged rates sections.

### Alpha 2.0 - Excel-like forecasting grid

- Frozen columns.
- Dynamic month columns.
- Grouped headers like the CTC sheet.
- Conditional formatting for categories/variance.
- Inline editing.
- Copy/paste from Excel.

### Alpha 2.1 - Resource intelligence

- Resource master list.
- Merge duplicate resource names.
- Resource aliases.
- Cost rate history.
- Source split by AP/TC.
- Click any total to show the source records.

### Alpha 3.0 - Multi-project and database

- SQLite project storage.
- Multi-project dashboard.
- User settings.
- Audit trail browser.
- Backups and snapshots.

## 13. Acceptance criteria

Alpha 1.0 is acceptable when:

- The app opens from Visual Studio.
- It loads `SampleData.json`.
- It displays the CTC forecast lines.
- Clicking `Stanley Drake` shows 39 transaction lines totalling 15,000.
- Clicking `Flex Projects L` shows the AP contractor payments totalling 7,420, even though raw `Resource Description` says `Contractors Payments`.
- The Resources tab groups resources correctly.
- Raw Transactions tab shows 63 transactions.
- The category report totals broadly match the workbook seed data.
- No Excel formulas are required at runtime.

## 14. Important implementation caution

Do not make the app depend on cell references like `AU`, `BD`, or row 76 internally. Those are import mapping details only. The app domain should use meaningful names:

- `TotalForecastCtc`
- `CostToDate`
- `PlannedCostFcc`
- `Budget`
- `TotalBudgetVariance`

The app should be able to survive a changed spreadsheet layout as long as the source concepts are still present.
