# Discovered test coverage map

LUNA-11 migrates the logical checks in `tests/ProjectCostForecast.Tests/Program.cs` into the discovered xUnit project. The console harness is retained as an opt-in legacy smoke executable. It is not the authoritative automated-test gate.

## Verification paths

The authoritative automated path is:

```powershell
dotnet test .\tests\ProjectCostForecast.UnitTests\ProjectCostForecast.UnitTests.csproj -c Release
```

`scripts/verify.ps1` restores and builds the solution, then invokes that same discovered test project. Use `-RunLegacySmoke` only when the retained executable smoke check is specifically required:

```powershell
.\scripts\verify.ps1 -RunLegacySmoke
```

The smoke executable remains useful for comparing the legacy sequential characterization run with the discovered suite and for exercising its packaged workbook path. It is not included in the default verifier result and does not replace discovered tests.

## Harness-to-test mapping

The harness contains 428 logical assertion invocations. The following map accounts for every invocation; the line ranges are anchored to the current harness so future changes can be reviewed explicitly.

| Harness section | Lines | Assertions | Discovered test(s) | Status |
|---|---:|---:|---|---|
| Initial cost-load workbook, fiscal periods, and startup defaults | 24-98 | 18 | `Luna11CalculationCoverageTests.Initial_cost_load_builds_the_deterministic_startup_dataset_from_the_packaged_workbook` | Ported |
| Clipboard matrix, accounting formatter, and KPI formatter | 102-139 | 17 | `Luna11CalculationCoverageTests.Clipboard_accounting_and_kpi_formatters_preserve_the_workbook_display_contract` | Ported |
| Seed totals, resource drilldown, fiscal reports, pivots, and project attribution | 141-239 | 33 | `Luna11CalculationCoverageTests.Seed_calculation_resource_drilldown_reports_and_project_attribution_remain_consistent` | Ported |
| Batch calculation performance | 241-270 | 2 | `Luna11CalculationCoverageTests.Batch_forecast_recalculation_keeps_large_transaction_work_within_the_legacy_budget` | Ported |
| Clearing a monthly forecast and persistence round trip | 274-296 | 6 | `Luna11CalculationCoverageTests.Clearing_a_monthly_forecast_recalculates_and_round_trips_derived_totals` | Ported |
| Atomic project persistence and rapid backup identity | 299-339 | 7 | `Luna11CalculationCoverageTests.Atomic_project_save_preserves_attribution_and_rapid_backup_identity` | Ported |
| Active period rules, saved-month viewing, locking, and restoration | 342-414 | 26 | `Luna11ViewModelCoverageTests.Saved_period_history_locks_edits_and_restores_the_current_forecast_view` | Ported |
| Budget lines, chart geometry, and ledger chart zoom/series controls | 418-444 | 14 | `Luna11ViewModelCoverageTests.Budget_and_ledger_chart_interactions_recalculate_the_selected_views` | Ported |
| Monthly forecast presentation, calendar-year context, freeze, and detail rail | 448-490 | 20 | `Luna11ViewModelCoverageTests.Workspace_presentation_context_and_detail_rail_preferences_are_independent_and_clamped` | Ported |
| Contingency collection dirty tracking and totals | 496-512 | 7 | `Luna11ViewModelCoverageTests.Contingency_collection_edits_refresh_totals_and_dirty_state` | Ported |
| Metadata period repair, categories, and task-code identity | 514-587 | 11 | `Luna11ViewModelCoverageTests.Loading_metadata_repairs_periods_and_keeps_category_and_task_identity_rules` | Ported |
| Management allocation, rates, and batch notifications | 589-677 | 19 | `Luna11ViewModelCoverageTests.Management_resource_allocations_sync_with_forecast_values_and_persist`; `Luna11ViewModelCoverageTests.Management_resource_default_rates_and_batch_notifications_are_deterministic` | Ported |
| Comment history, curve presets, and resource ledger drilldown | 678-760 | 36 | `Luna11ViewModelCoverageTests.Comment_history_curve_presets_and_ledger_drilldown_keep_the_selected_resource_context` | Ported |
| Auto-import, forecast-line attribution, preview grouping, and unmatched persistence | 762-842 | 16 | `Luna11ImportCoverageTests.View_model_import_and_preview_preserve_resource_and_project_attribution` | Ported |
| Resource workspace layouts and persisted header colours | 850-888 | 12 | `Luna11ImportCoverageTests.Workspace_views_and_header_colours_keep_independent_persisted_layouts` | Ported |
| Forecast column width, row height, virtualization, and WPF grid contracts | 894-923 | 14 | `Luna11WpfGridCoverageTests.Wpf_grid_layout_and_shared_selection_regressions_run_on_a_dedicated_sta_thread` | Ported; `[Trait("Category", "Wpf")]` |
| Forecast/detail workspace isolation, pivots, and pivot-builder actions | 929-984 | 29 | `Luna11WorkspaceCoverageTests.Forecast_and_detail_workspace_views_preserve_independent_display_and_pivot_state` | Ported |
| CSV/XLSX field mapping, multiline records, duplicate/name keys | 986-1162 | 27 | `Luna11ImportCoverageTests.Csv_and_workbook_imports_preserve_field_mapping_and_multiline_records` | Ported; LUNA-09 boundary hardening remains additive |
| Cost-centre association scoring and candidate suggestions | 1164-1231 | 6 | `Luna11ImportCoverageTests.Cost_centre_association_suggestions_prioritise_real_mapped_names` | Ported |
| Predecessor parsing, calendar lag, CPM, baselines, and deadlines | 1233-1335 | 32 | `Luna11SchedulingCoverageTests.Scheduling_parser_calendars_constraints_baselines_and_cpm_dates_match_the_characterization` | Ported |
| Schedule editor insertion, links, clipboard, reorder, and baseline edit | 1337-1392 | 25 | `Luna11SchedulingCoverageTests.Schedule_view_model_editing_supports_links_clipboard_reordering_and_baseline_dates` | Ported |
| Large schedule link rebuild and timing | 1394-1419 | 2 | `Luna11SchedulingCoverageTests.Large_schedule_recalculation_rebuilds_all_links_within_the_legacy_budget` | Ported |
| Forecast curve profiles and locked-month application | 1421-1455 | 16 | `Luna11CurveCoverageTests.Forecast_curve_profiles_preserve_totals_and_expected_shapes`; `Luna11CurveCoverageTests.Forecast_curve_application_respects_locked_months_and_preserves_open_total` | Ported |
| Shared `ProjectDataGrid` profiles, row geometry, and modifier selection | 1550-1819 | 33 | `Luna11WpfGridCoverageTests.Wpf_grid_layout_and_shared_selection_regressions_run_on_a_dedicated_sta_thread` | Ported; `[Trait("Category", "Wpf")]` |

Total: 428/428 harness assertion invocations mapped to discovered tests.

## Integrated packet coverage

The following checks are intentionally not duplicated by LUNA-11. They live in the integrated packet-specific test files and run through the same discovered xUnit project:

- LUNA-09: formula-injection export fixtures, file/worksheet/row/cell/character limits, malformed and unsupported boundaries, cancellation/partial-commit behavior, duplicate-import policy, and workbook handle disposal. The LUNA-11 import rows cover the pre-existing field-mapping and supported round-trip characterization.
- LUNA-10: daylight-saving transitions, ambient-culture independence, durable timestamp/clock seams, and legacy timestamp migration. The LUNA-11 fiscal-period checks use fixed dates and preserve the existing period-semantics characterization.

## Negative control

`Luna11NegativeControlTests.Discovered_assertions_report_a_representative_broken_expectation` deliberately invokes a false xUnit equality assertion inside `Record.Exception` and verifies the resulting `EqualException`. This proves the discovered adapter observes a broken assertion. Packet verification also introduced a temporary unhandled false `[Fact]`, confirmed the authoritative `dotnet test` command exited non-zero, removed the temporary file, and reran the clean suite. To reproduce manually, make an unhandled assertion false, run the authoritative command, and restore the intentional control afterward.

## Isolation rules

- Tests construct fresh datasets/view-models and use unique temporary directories; no test relies on another test's state or order.
- The only desktop-dependent migration is the WPF grid regression. It runs on a dedicated STA thread, uses a hidden `HwndSource`, has the `Wpf` trait, and does not open an interactive window.
- Performance checks use the existing fixed synthetic sizes and five-second characterization budgets. They report elapsed time but do not persist machine-specific paths or values.
