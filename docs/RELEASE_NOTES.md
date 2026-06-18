# Project Cost Forecast Release Notes

## Release Candidate - 2026-05-10

This build advances the original Alpha 1 workbook rebuild toward the Alpha 3+ roadmap and a usable release candidate.

### Added

- Calculation service for cost to date, current-period actuals, CTC, FCC, last-month variance, month variance, budget variance, and category summaries.
- Manual Name-first transaction matching so AP contractor payments drill into the correct supplier/resource.
- Editable monthly forecasts in the resource drilldown panel.
- Audit events for forecast edits, line changes, imports, and saves.
- Validation tab for forecast and transaction data quality issues.
- Workbook Report tab matching the spreadsheet's FY spent-to-date, cost-to-complete, planned-cost, AP/LTP budget, and variance block.
- Actuals Pivot tab matching the workbook pivot's task/resource/FY-period grouping.
- Open, save, and save-as project JSON files, with timestamped backups when overwriting.
- CSV import for raw transactions with common header aliases.
- CSV export for raw transactions.
- Forecast line add, duplicate, and delete commands.
- Release-candidate status/header information and unsaved-change warning on close.
- No-dependency command-line acceptance checks in `tests/ProjectCostForecast.Tests`.

### Verified

- The solution builds cleanly.
- Seed data contains 63 raw transactions totalling 27,695.
- Stanley Drake drilldown resolves 39 transaction lines totalling 15,000.
- Flex Projects L drilldown resolves 4 AP transaction lines totalling 7,420.
- Flex Projects L AP rows group by Manual Name rather than the generic `Contractors Payments` resource description.
- Category summaries recalculate from forecast lines.
- FY report values match Excel's cached formulas to two decimal places.
- Actuals pivot values match the workbook pivot totals.
- Seed data has no validation errors.

### Remaining Before A Formal Production Release

- Package/sign an installer or MSIX.
- Build a direct `.xlsm` importer from the workbook instead of relying on extracted JSON seed data.
- Add richer Excel/PDF report export.
- Add persistent multi-project database storage if multiple live projects need to be managed together.
- Run user acceptance testing with more than the supplied workbook.
