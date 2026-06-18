# Codex Start Here

You are continuing the desktop rebuild of the Excel workbook `1.Mar 26.xlsm`.

## First task

Open and read these files in order:

1. `README.md`
2. `docs/REBUILD_SPEC_FOR_CODEX.md`
3. `docs/WORKBOOK_ANALYSIS.md`
4. `src/ProjectCostForecast.App/Data/SampleData.json`

Do not start by redesigning the app. First understand the workbook mapping and the resource drilldown requirement.

## Main product goal

Rebuild the workbook as a PC program for managing project cost-to-complete forecasting. The app must feel like a controlled, auditable version of the spreadsheet rather than a generic database.

The most important extra feature that did not exist properly in the spreadsheet is:

> When the user clicks a resource/person/supplier, the app must show every cost transaction that has come into that resource, with the raw transaction lines visible and summed.

In the uploaded workbook this source data comes from the `Raw Data Entry` sheet. The CTC sheet links a forecast row to raw cost through:

- `Task Number` from CTC column A
- `Who` from CTC column B
- `Raw Data Entry.Task Number`
- `Raw Data Entry.Manual Name`

For AP transactions the workbook's `Resource Description` is often `Contractors Payments`, while `Manual Name` contains the real party to show in the CTC grid. Therefore **Manual Name is the main resource/person/supplier key** for the drilldown.

## Build guardrails

- Keep Alpha 1.0 compiling before adding features.
- Do not introduce NuGet dependencies unless needed.
- Keep formula logic in C# services, not in XAML.
- Treat JSON seed data as a temporary import format. Later versions should import `.xlsx/.xlsm`, CSV, or accounting-system exports.
- Preserve NZ currency and date formatting.
- Make all totals explainable by drilldown.

## Recommended next Codex tasks

1. Build/run the existing app and fix any compile issues.
2. Add a `CalculationService` that recalculates CTC, CTD, FCC, and variance values from source objects rather than trusting JSON totals.
3. Add unit tests for resource matching and totals.
4. Add edit support for monthly forecast values.
5. Add import/export so users can move from the spreadsheet into the program.
6. Add a proper audit trail: old value, new value, user, timestamp, reason.
