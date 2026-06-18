# Project Cost Forecast WPF UI Element Map

This pack is based on the selected Foresight / Project Cost Forecast mockup. Use the PNG icons for a no-dependency WPF build, or use the SVG versions if your app has an SVG renderer. The PNG files are the safest option for plain C# WPF.

## Layout target

Use a 3-column shell:

1. **Left navigation column**: fixed width about `240px`; white background; subtle right border `#E5EAF3`.
2. **Main content column**: flexible width; contains hero image header, KPI cards, tabs, toolbar, filter panel, and forecast matrix.
3. **Right Resource Drilldown drawer**: fixed width about `360px`; white card; visible when a resource/category is selected.

Recommended WPF root structure:

```text
Window
└─ Grid
   ├─ Column 0: LeftNavigation
   ├─ Column 1: MainContent
   └─ Column 2: ResourceDrilldownDrawer
```

## Icon usage and placement

| UI element | Icon file | Size | Placement |
|---|---:|---:|---|
| App logo | `ic_app_logo_32.png` | 32x32 | Top-left mini rail / Foresight brand block |
| Overview nav | `ic_nav_home_20.png` | 20x20 | Left nav row before `Overview` |
| Forecasts nav selected | `ic_nav_forecasts_20.png` | 20x20 | Left nav row before `Forecasts` |
| Reports nav | `ic_nav_reports_20.png` | 20x20 | Left nav row before `Reports` |
| Resources nav | `ic_nav_resources_20.png` | 20x20 | Left nav row before `Resources` |
| Settings nav | `ic_nav_settings_20.png` | 20x20 | Left footer before `Settings` |
| Collapse nav | `ic_collapse_20.png` | 20x20 | Left footer before `Collapse` |
| Current period | `ic_calendar_18.png` | 18x18 | Inside top-right period pill |
| Planned Cost KPI | `ic_metric_planned_cost_28.png` | 28x28 | Inside purple circular icon well on first card |
| Planned Cost to Date KPI | `ic_metric_cost_to_date_28.png` | 28x28 | Inside green circular icon well on second card |
| Forecast at Completion KPI | `ic_metric_forecast_at_completion_28.png` | 28x28 | Inside blue circular icon well on third card |
| Forecast Variance KPI | `ic_metric_forecast_variance_28.png` | 28x28 | Inside red circular icon well on fourth card |
| Variance % KPI | `ic_metric_variance_percent_28.png` | 28x28 | Inside orange circular icon well on fifth card |
| Budget Remaining KPI | `ic_metric_budget_remaining_28.png` | 28x28 | Inside teal circular icon well on sixth card |
| Forecast tab | `ic_tab_forecast_16.png` | 16x16 | Before `Forecast` tab label |
| Resources tab | `ic_tab_resources_16.png` | 16x16 | Before `Resources` tab label |
| Raw Transactions tab | `ic_tab_raw_transactions_16.png` | 16x16 | Before `Raw Transactions` tab label |
| Summary tab | `ic_tab_summary_16.png` | 16x16 | Before `Summary` tab label |
| Monthly Report tab | `ic_tab_monthly_report_16.png` | 16x16 | Before `Monthly Report` tab label |
| Pivot Builder tab | `ic_tab_pivot_builder_16.png` | 16x16 | Before `Pivot Builder` tab label |
| Contingency tab | `ic_tab_contingency_16.png` | 16x16 | Before `Contingency` tab label |
| Audit tab | `ic_tab_audit_16.png` | 16x16 | Before `Audit` tab label |
| Views dropdown | `ic_view_grid_16.png` | 16x16 | In matrix toolbar before `Views` |
| Group by dropdown | `ic_group_by_16.png` | 16x16 | In matrix toolbar before `Group by: Category` |
| Filters button | `ic_filter_16.png` | 16x16 | In top toolbar and left filter panel headings |
| Save View | `ic_save_view_16.png` | 16x16 | Before `Save view` |
| Export | `ic_export_16.png` | 16x16 | Before `Export`; also use dropdown chevron after text |
| Refresh | `ic_refresh_16.png` | 16x16 | Inside blue `Refresh` button |
| Expand all | `ic_expand_all_16.png` | 16x16 | Top-right of matrix header |
| Drilldown close | `ic_close_16.png` | 16x16 | Top-right of Resource Drilldown drawer |
| Expand/collapse rows | `ic_chevron_down_16.png`, `ic_chevron_right_16.png` | 16x16 | First cell of each category group row |
| Project Management group | `ic_category_project_management_20.png` | 20x20 | Beside `Project Management` row |
| Internal Staff Costs group | `ic_category_internal_staff_20.png` | 20x20 | Beside `Internal Staff Costs` row |
| Design Consultants group | `ic_category_design_consultants_20.png` | 20x20 | Beside `Design Consultants` row |
| Contractors group | `ic_category_contractors_20.png` | 20x20 | Beside `Contractors` row |
| Compliance group | `ic_category_compliance_20.png` | 20x20 | Beside `Compliance` row |
| Close Out group | `ic_category_closeout_20.png` | 20x20 | Beside `Close Out` row |
| Export details | `ic_download_details_16.png` | 16x16 | Left of bottom `Export details` button in drawer |

## Main UI elements for Codex

### 1. LeftNavigation

- `BrandPanel`
  - `AppLogoImage`: `ic_app_logo_32.png`
  - `BrandText`: `Foresight`
- `NavigationItems`
  - `OverviewNavItem`: icon `ic_nav_home_20.png`
  - `ForecastsNavItem`: selected; icon `ic_nav_forecasts_20.png`
    - child scenario links: `Default`, `Scenario A`, `Scenario B`
  - `ReportsNavItem`: icon `ic_nav_reports_20.png`
  - `ResourcesNavItem`: icon `ic_nav_resources_20.png`
  - `SettingsNavItem`: icon `ic_nav_settings_20.png`
- `LeftFilterPanel`
  - `MonthCostSortCombo`: default `Month Cost Asc`
  - `BudgetVarianceCombo`: default `All`
  - `CategoryCombo`: default `All`
  - `ActualCostOnlyCheckBox`
  - `CostThisMonthOnlyCheckBox`
  - `RemainingForecastOnlyCheckBox`: checked
  - `MonthlyVarianceCheckBox`
  - `ApplyFiltersButton`: primary blue
- `WorkbookInfoPanel`
  - `SeedDataText`: `Seed data from 1 May 2025 10:24 AM`
  - `ChangeWorkbookLink`
- `CollapseButton`: icon `ic_collapse_20.png`

### 2. HeaderHero

- `HeroBackgroundImage`: lake / dam panorama with a white fade overlay on the left.
- `PageTitle`: `Project Cost Forecast`
- `ProjectSubtitle`: `WW4378 – PNWP – Fish Screens (Lake)` with a dropdown chevron.
- `LoadedWorkbookText`: `Loaded workbook: 01 May 2025, 10:24 AM`
- `ValidatedStatus`: `All data validated`, icon `ic_badge_validation_ok_16.png`
- `CurrentPeriodPill`: icon `ic_calendar_18.png`, text `Current period`, value `26-11`, dropdown chevron.

### 3. KpiCardRow

Each KPI is a rounded white card with subtle border/shadow, icon circle on the left, label, value, and small status text.

- `PlannedCostCard`: icon `ic_metric_planned_cost_28.png`; value `$4,265,995`
- `PlannedCostToDateCard`: icon `ic_metric_cost_to_date_28.png`; value `$27,695`; subtext `63 transactions`
- `ForecastAtCompletionCard`: icon `ic_metric_forecast_at_completion_28.png`; value `$4,335,210`; red subtext `↑ 1.6% over budget`
- `ForecastVarianceCard`: icon `ic_metric_forecast_variance_28.png`; value `$69,215`; red link text `Over`
- `VariancePercentCard`: icon `ic_metric_variance_percent_28.png`; value `1.6%`; subtext `Over budget`
- `BudgetRemainingCard`: icon `ic_metric_budget_remaining_28.png`; value `$0`; green subtext `0 validation issues`

### 4. TopTabs

Horizontal tab bar under the KPIs.

- Selected tab: `ForecastTab`, icon `ic_tab_forecast_16.png`, blue underline.
- Other tabs: `ResourcesTab`, `RawTransactionsTab`, `SummaryTab`, `MonthlyReportTab`, `PivotBuilderTab`, `ContingencyTab`, `AuditTab`.

### 5. ForecastMatrixToolbar

- `ViewsButton`: icon `ic_view_grid_16.png`, text `Views`, dropdown chevron.
- `GroupByButton`: icon `ic_group_by_16.png`, text `Group by: Category`, dropdown chevron.
- `FiltersButton`: icon `ic_filter_16.png`, text `Filters`, badge count `2`.
- Active filter chips: `Remaining forecast only`, `Budget variance: All`, plus `Clear all` link.
- Right aligned controls:
  - `BudgetViewCombo`: `Budget`
  - `MonthsCombo`: `6`
  - `SaveViewButton`: icon `ic_save_view_16.png`
  - `ExportButton`: icon `ic_export_16.png`, dropdown chevron.
  - `RefreshButton`: blue primary, icon `ic_refresh_16.png`

### 6. ForecastMatrix

Table title: `Forecast by Resource (Monthly)`.

Columns:

1. `Category / Resource`
2. `Budget`
3. `CTD`
4. `Last Forecast`
5. `May '25 / 26-11`
6. `Jun '25 / 26-12`
7. `Jul '25 / 26-01`
8. `Aug '25 / 26-02`
9. `Sep '25 / 26-03`
10. `Oct '25 / 26-04`
11. `Total Forecast`

Rows:

- `Project Management` group row: icon `ic_category_project_management_20.png`; expanded.
  - `PMO Services`
- `Internal Staff Costs` group row: icon `ic_category_internal_staff_20.png`; expanded.
  - `Business Analysts`
  - `Project Coordinators`
- `Design Consultants` group row: icon `ic_category_design_consultants_20.png`; expanded.
  - `UX / UI Design`
  - `Technical Architecture`
- `Contractors` group row: icon `ic_category_contractors_20.png`; expanded.
  - `Software Developers`
  - `QA / Testers`
  - `DevOps Engineers`
- `Compliance` group row: icon `ic_category_compliance_20.png`; expanded.
  - `Security & Audit`
- `Close Out` group row: icon `ic_category_closeout_20.png`; expanded.
  - `Project Closeout`
- `GrandTotalRow`: fixed at bottom if possible.

Visual rules:

- Group rows use very light tinted backgrounds matching their icon colour.
- Child rows are indented 28–36px.
- Numeric values are right aligned.
- `Total Forecast` values are blue and semibold.
- Bottom status: `Showing 6 of 24 resources`, `View all resources`, `Amounts in USD`, settings gear.

### 7. ResourceDrilldownDrawer

- `DrawerTitle`: `Resource Drilldown`
- `DrawerCloseButton`: icon `ic_close_16.png`
- Selected resource summary:
  - icon `ic_category_project_management_20.png` or category-specific icon
  - name: `Software Developers` or selected row label
  - task code: `WAS7102001`
- Four stat cards in a 2x2 grid:
  - `Transactions`: `8`
  - `Raw total`: `$1,401,320`
  - `Units`: `2,540.00`
  - `Average rate`: `$100.00`
- `DrawerTabs`
  - selected `Costs into this resource`
  - `Monthly forecast`
  - `Spend curve`
- `TransactionsTable`
  - columns: `Period`, `Task`, `Units`, `Rate`, `Amount`
- `ExportDetailsButton`: full-width outline button, icon `ic_download_details_16.png`

## Suggested WPF naming conventions

Use these names for controls so Codex can wire logic clearly:

```text
ShellGrid
LeftNavigationColumn
MainContentColumn
ResourceDrilldownColumn
HeroHeader
KpiCardRow
TopTabBar
MatrixToolbar
ForecastMatrixGrid
ResourceDrilldownDrawer
```

Use these model names:

```csharp
ForecastKpi
ForecastCategoryGroup
ForecastResourceRow
MonthlyForecastValue
ResourceDrilldownSummary
CostTransactionRow
FilterState
SavedView
```

## Suggested colours

```text
PrimaryBlue: #2563EB
NavyText: #0F172A
MutedText: #64748B
Border: #E5EAF3
PanelBackground: #FFFFFF
AppBackground: #F8FAFC
SuccessGreen: #16A34A
WarningOrange: #F97316
ErrorRed: #EF4444
Purple: #7C3AED
Teal: #0F766E
```

## Notes for WPF implementation

- Use a `DataGrid` for the forecast matrix if you need sorting/editing quickly.
- Use a custom `TreeGrid` / grouped `ItemsControl` if the expand/collapse hierarchy matters more than spreadsheet editing.
- Use `GridSplitter` between the matrix and the right drawer if you want the drawer width to be adjustable.
- For icons, use PNG assets first. Later you can replace them with XAML `Path` icons for theme colouring.
