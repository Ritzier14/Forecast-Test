using System.Windows.Media;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11ViewModelCoverageTests
{
    [Fact]
    public void Saved_period_history_locks_edits_and_restores_the_current_forecast_view()
    {
        var seed = Luna11TestSupport.LoadSeedDataset();
        Assert.Equal(
            "26-10",
            MainWindowViewModel.DetermineClosedForecastPeriod(seed.ForecastPeriods, new DateOnly(2026, 5, 17))?.Label);
        Assert.Equal(
            "26-11",
            MainWindowViewModel.DetermineExpectedWorkingPeriod(seed.ForecastPeriods, new DateOnly(2026, 6, 17))?.Label);
        Assert.Empty(MainWindowViewModel.BuildActivePeriodWarnings(seed.ForecastPeriods, "26-11", new DateOnly(2026, 6, 17)));
        Assert.Single(MainWindowViewModel.BuildActivePeriodWarnings(seed.ForecastPeriods, "26-10", new DateOnly(2026, 6, 17)));
        Assert.Single(MainWindowViewModel.BuildActivePeriodWarnings(seed.ForecastPeriods, "26-12", new DateOnly(2026, 6, 17)));
        Assert.Single(MainWindowViewModel.BuildActivePeriodWarnings(seed.ForecastPeriods, "99-99", new DateOnly(2026, 6, 17)));

        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        Assert.Equal("26-09", viewModel.Header.CurrentPeriod);
        Assert.Equal(2026, viewModel.SelectedCtcMonthForecastYear);
        var currentPeriodForecast = viewModel.ForecastLines
            .Single(line => line.TaskNumber == "WA57102001" && line.ResourceName == "Stanley Drake")
            .MonthlyForecasts
            .Single(forecast => forecast.PeriodLabel == viewModel.Header.CurrentPeriod);
        var previousPeriodForecast = viewModel.ForecastLines
            .Single(line => line.TaskNumber == "WA57102001" && line.ResourceName == "Stanley Drake")
            .MonthlyForecasts
            .Single(forecast => forecast.PeriodLabel == "26-08");
        Assert.False(currentPeriodForecast.IsLocked);
        Assert.True(previousPeriodForecast.IsLocked);

        var workingForecastLine = viewModel.ForecastLines.First();
        var workingForecastAmount = workingForecastLine.MonthlyForecasts.First().Amount;
        var historicalSnapshot = new SavedMonthSnapshot
        {
            Period = "26-08",
            SavedAt = new DateTime(2026, 4, 1, 8, 0, 0),
            ForecastLines =
            [
                new SavedMonthForecastLine
                {
                    RowNumber = 9001,
                    TaskNumber = "HIST-1",
                    ResourceName = "Historical resource",
                    ProjectCode = "Historical category",
                    CostToDate = 100m,
                    CurrentPeriodForecast = 10m,
                    CostToComplete = 30m,
                    FinalForecast = 130m,
                    Budget = 200m,
                    TotalBudgetVariance = 70m,
                    VarianceFromPreviousMonth = 5m,
                    MonthlyForecasts =
                    [
                        new SavedMonthPeriodAmount { PeriodLabel = "26-08", PeriodStartDate = new DateOnly(2026, 3, 1), Amount = 10m },
                        new SavedMonthPeriodAmount { PeriodLabel = "26-09", PeriodStartDate = new DateOnly(2026, 4, 1), Amount = 20m }
                    ]
                }
            ]
        };

        viewModel.SearchText = "filter that matches nothing";
        viewModel.ShowOnlyLinesWithRemainingForecast = true;
        viewModel.ForecastLinesView.Refresh();
        Assert.Empty(viewModel.ForecastLinesView.Cast<object>());
        viewModel.ViewSavedMonthSnapshot(historicalSnapshot);
        Assert.True(viewModel.IsViewingSavedMonth);
        Assert.True(viewModel.IsSavedMonthViewLocked);
        Assert.Single(viewModel.ForecastLinesView.Cast<object>());
        Assert.False(viewModel.ShowOnlyLinesWithRemainingForecast);
        Assert.True(string.IsNullOrEmpty(viewModel.SearchText));
        Assert.All(viewModel.ForecastLines.SelectMany(line => line.MonthlyForecasts), month => Assert.True(month.IsLocked));
        Assert.Equal("26-09", viewModel.Header.CurrentPeriod);
        Assert.True(viewModel.SetSavedMonthViewLocked(false, confirmUnlock: false));
        Assert.All(viewModel.ForecastLines.SelectMany(line => line.MonthlyForecasts), month => Assert.False(month.IsLocked));

        viewModel.ForecastLines.Single().MonthlyForecasts.First().Amount = 15m;
        Assert.Equal(15m, historicalSnapshot.ForecastLines.Single().MonthlyForecasts.First().Amount);
        Assert.Equal(35m, historicalSnapshot.CostToComplete);
        Assert.Equal(135m, historicalSnapshot.FinalForecast);

        viewModel.CloseSavedMonthView();
        Assert.False(viewModel.IsViewingSavedMonth);
        Assert.True(viewModel.ShowOnlyLinesWithRemainingForecast);
        Assert.Equal("filter that matches nothing", viewModel.SearchText);
        Assert.Same(workingForecastLine, viewModel.ForecastLines.First());
        Assert.Equal(workingForecastAmount, viewModel.ForecastLines.First().MonthlyForecasts.First().Amount);
    }

    [Fact]
    public void Budget_and_ledger_chart_interactions_recalculate_the_selected_views()
    {
        var dataset = Luna11TestSupport.LoadSeedDataset();
        var viewModel = Luna11TestSupport.CreateSeedViewModel();

        Assert.Equal(2, viewModel.BudgetLines.Count);
        Assert.NotEmpty(viewModel.BudgetFiscalYears);
        var p3mBudgetLine = viewModel.BudgetLines.Single(line => line.Key == MainWindowViewModel.P3mBudgetLineKey);
        var ltpApBudgetLine = viewModel.BudgetLines.Single(line => line.Key == MainWindowViewModel.LtpApBudgetLineKey);
        Assert.True(ltpApBudgetLine.IsActive);
        Assert.Equal(dataset.FiscalYearBudgets.Sum(budget => budget.Budget), ltpApBudgetLine.Total);

        var firstBudgetYear = p3mBudgetLine.Amounts.First();
        firstBudgetYear.Amount = 123456m;
        Assert.Equal(123456m, p3mBudgetLine.Total);
        viewModel.SetActiveBudgetLine(p3mBudgetLine);
        Assert.True(p3mBudgetLine.IsActive);
        Assert.False(ltpApBudgetLine.IsActive);
        Assert.Equal(123456m, viewModel.FiscalYearReportLines.Single(line => line.FiscalYear == firstBudgetYear.FiscalYear).Budget);
        Assert.True(viewModel.BudgetActualChartGeometry != Geometry.Empty);
        Assert.True(viewModel.BudgetForecastChartGeometry != Geometry.Empty);
        Assert.True(viewModel.BudgetPlanChartGeometry != Geometry.Empty);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ForecastFreezeColumnKey));

        var chartViewModel = Luna11TestSupport.CreateSeedViewModel();
        chartViewModel.SelectedForecastLine = chartViewModel.ForecastLines.First();
        var monthlyChartTickCount = chartViewModel.LedgerChartXAxisLabels.Count;
        chartViewModel.ZoomLedgerChart(zoomIn: false);
        Assert.Equal(LedgerChartTimeScale.Quarter, chartViewModel.LedgerChartTimeScale);
        Assert.True(chartViewModel.LedgerChartXAxisLabels.Count <= monthlyChartTickCount);
        chartViewModel.ShowLedgerActualSeries = false;
        chartViewModel.ShowLedgerForecastSeries = false;
        chartViewModel.ShowLedgerBudgetSeries = true;
        Assert.True(!chartViewModel.ShowLedgerActualSeries
            && !chartViewModel.ShowLedgerForecastSeries
            && chartViewModel.ShowLedgerBudgetSeries);
    }

    [Fact]
    public void Workspace_presentation_context_and_detail_rail_preferences_are_independent_and_clamped()
    {
        var presentationViewModel = Luna11TestSupport.CreateSeedViewModel();
        presentationViewModel.ActiveDetailWorkspaceKey = "Ledger Monthly Forecast";
        Assert.Contains(presentationViewModel.CurrentDetailWorkspaceViews, view => view.ContentKey == "MonthsDown");
        Assert.Contains(presentationViewModel.CurrentDetailWorkspaceViews, view => view.ContentKey == "MonthsAcross");
        presentationViewModel.SelectedForecastLine = presentationViewModel.ForecastLines.First();
        presentationViewModel.ToggleMonthlyForecastOrientation();
        Assert.True(presentationViewModel.IsMonthlyForecastMonthsAcross);
        Assert.NotEmpty(presentationViewModel.MonthlyForecastAcrossRows);
        Assert.All(presentationViewModel.MonthlyForecastAcrossRows, row => Assert.NotEmpty(row.Values));
        var acrossForecastRow = presentationViewModel.MonthlyForecastAcrossRows.First(row => row.Metric == "Forecast");
        var acrossPeriod = acrossForecastRow.Values.Keys.First();
        Assert.Equal(
            presentationViewModel.SelectedForecastLine.MonthlyForecasts.Single(forecast => forecast.PeriodLabel == acrossPeriod).Amount,
            acrossForecastRow[acrossPeriod]);
        presentationViewModel.ToggleMonthlyForecastOrientation();
        Assert.False(presentationViewModel.IsMonthlyForecastMonthsAcross);
        presentationViewModel.TaskCodeReviewDisplayMode = "Both";
        Assert.NotEmpty(presentationViewModel.TaskCodeReviewRows);

        var calendarYearViewModel = Luna11TestSupport.CreateSeedViewModel();
        var nextCalendarYear = calendarYearViewModel.AvailableCtcMonthForecastYears.Max() + 1;
        var selectedCalendarYearsBeforeAdd = calendarYearViewModel.SelectedCtcMonthForecastYears.ToHashSet();
        calendarYearViewModel.AddNewCalendarYear();
        Assert.Contains(nextCalendarYear, calendarYearViewModel.AvailableCtcMonthForecastYears);
        Assert.Equal(nextCalendarYear, calendarYearViewModel.SelectedCtcMonthForecastYear);
        Assert.All(selectedCalendarYearsBeforeAdd, year => Assert.True(calendarYearViewModel.IsCtcMonthForecastYearSelected(year)));
        Assert.Contains(nextCalendarYear, calendarYearViewModel.SelectedCtcMonthForecastYears);

        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        viewModel.ResetForecastFreezeColumn();
        Assert.Equal(MainWindowViewModel.DefaultForecastFreezeColumnKey, viewModel.ForecastFreezeColumnKey);
        viewModel.SetForecastFreezeColumn("MONTH:26-11");
        Assert.Equal("MONTH:26-11", viewModel.ForecastFreezeColumnKey);
        viewModel.ResetForecastFreezeColumn();
        Assert.Equal(MainWindowViewModel.DefaultForecastFreezeColumnKey, viewModel.ForecastFreezeColumnKey);
        viewModel.SetDetailPanelRailWidth(72);
        Assert.Equal(72d, viewModel.DetailPanelRailWidth);
        viewModel.SetDetailPanelRailWidth(8);
        Assert.Equal(36d, viewModel.DetailPanelRailWidth);
        viewModel.SetDetailPanelRailWidth(160);
        Assert.Equal(92d, viewModel.DetailPanelRailWidth);
        viewModel.SetDetailPanelPinned(true);
        Assert.True(viewModel.IsDetailPanelPinned);
        viewModel.SetDetailPanelPinned(false);
        Assert.False(viewModel.IsDetailPanelPinned);
    }

    [Fact]
    public void Contingency_collection_edits_refresh_totals_and_dirty_state()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        viewModel.ContingencyEntries.Clear();
        viewModel.IsDirty = false;
        var trackedContingency = new ContingencyEntry
        {
            RemainingContingency = 500m,
            ContingencyExpended = 100m,
            ProposedExpenditure = 50m
        };

        viewModel.ContingencyEntries.Add(trackedContingency);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(500m, viewModel.TotalContingencyRemaining);
        Assert.Equal(100m, viewModel.ContingencyExpendedTotal);
        viewModel.IsDirty = false;
        trackedContingency.RemainingContingency = 425m;
        Assert.True(viewModel.IsDirty);
        Assert.Equal(425m, viewModel.TotalContingencyRemaining);
        viewModel.IsDirty = false;
        viewModel.ContingencyEntries.Remove(trackedContingency);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(0m, viewModel.TotalContingencyRemaining);
    }

    [Fact]
    public void Loading_metadata_repairs_periods_and_keeps_category_and_task_identity_rules()
    {
        var metadataDataset = new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Metadata fixture", CurrentPeriod = "26-11" },
            ForecastPeriods = [new ForecastPeriod { Label = "26-11", StartDate = new DateOnly(2026, 6, 1) }],
            Transactions =
            [
                new CostTransaction { TaskNumber = "RAW-001", ManualName = "Imported", FyPeriod = "26-11", Amount = 10m }
            ],
            ForecastLines =
            [
                new ForecastLine { TaskNumber = "RAW-001", ResourceName = "Imported", ProjectCode = "Legacy Category" },
                new ForecastLine { TaskNumber = "MAN-001", ResourceName = "Manual", ProjectCode = "Manual task" }
            ],
            ProjectTaskCodes =
            [
                new ProjectTaskCode { SystemCode = "RAW-001", TaskName = "Imported task", IsRawDataCode = true },
                new ProjectTaskCode { SystemCode = "MAN-001", TaskName = "Manual task", IsManualCode = true }
            ],
            ProjectCategories =
            [
                new ProjectCategory { Name = "Legacy Category" },
                new ProjectCategory { Name = "Manual Override" }
            ]
        };

        var metadataViewModel = Luna11TestSupport.CreateSeedViewModel();
        Luna11TestSupport.InvokeLoadDataset(metadataViewModel, metadataDataset);
        Assert.Equal(new DateOnly(2026, 5, 1), metadataDataset.ForecastPeriods.Single().StartDate);

        var sparseLoadedDataset = new ProjectDataset
        {
            Header = new ProjectHeader { CurrentPeriod = "23-06" },
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "22-07", StartDate = new DateOnly(2022, 1, 1) },
                new ForecastPeriod { Label = "23-06", StartDate = new DateOnly(2022, 12, 1) }
            ],
            ForecastLines =
            [
                new ForecastLine
                {
                    TaskNumber = "SPARSE-001",
                    ResourceName = "Sparse",
                    MonthlyForecasts =
                    [
                        new MonthlyForecast { PeriodLabel = "22-07", PeriodStartDate = new DateOnly(2022, 1, 1), Amount = 5m },
                        new MonthlyForecast { PeriodLabel = "23-06", PeriodStartDate = new DateOnly(2022, 12, 1), Amount = 7m }
                    ]
                }
            ]
        };
        var sparseLoadedViewModel = Luna11TestSupport.CreateSeedViewModel();
        Luna11TestSupport.InvokeLoadDataset(sparseLoadedViewModel, sparseLoadedDataset);
        Assert.True(
            sparseLoadedDataset.ForecastPeriods
                .Where(period => period.StartDate?.Year == 2022)
                .Select(period => period.Label)
                .SequenceEqual(FiscalPeriod.BuildContinuousRange(22, 7, 23, 6)));
        Assert.Contains(
            sparseLoadedDataset.ForecastLines.Single().MonthlyForecasts,
            forecast => forecast.PeriodLabel == "22-08" && forecast.Amount == 0m);

        var migratedLine = metadataViewModel.ForecastLines.Single(line => line.TaskNumber == "RAW-001");
        var fallbackLine = metadataViewModel.ForecastLines.Single(line => line.TaskNumber == "MAN-001");
        Assert.Equal("Legacy Category", migratedLine.ReportingCategoryOverride);
        Assert.Equal("Imported task", migratedLine.ReportingCategory);
        Assert.Equal("Manual task", fallbackLine.ReportingCategory);
        metadataViewModel.SetForecastLineReportingCategory(fallbackLine, "Manual Override");
        Assert.Equal("Manual Override", fallbackLine.ReportingCategory);
        metadataViewModel.DeleteProjectCategory(metadataViewModel.ProjectCategories.Single(category => category.Name == "Manual Override"));
        Assert.Equal("Manual task", fallbackLine.ReportingCategory);
        metadataViewModel.ProjectTaskCodes.Add(new ProjectTaskCode { SystemCode = "MAN-002", TaskName = "Manual task", IsManualCode = true });
        metadataViewModel.RefreshTaskCategoryMetadata();
        Assert.Contains(metadataViewModel.ProjectTaskCodes, task => task.TaskName == "Manual task (1)");
        Assert.False(metadataViewModel.ProjectTaskCodes.Single(task => task.SystemCode == "RAW-001").CanEditSystemCode);
        Assert.True(metadataViewModel.ProjectTaskCodes.Single(task => task.SystemCode == "MAN-001").CanEditSystemCode);
    }

    [Fact]
    public void Management_resource_allocations_sync_with_forecast_values_and_persist()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var managementSourceLine = viewModel.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
        var managementResource = viewModel.AddManagementResource(managementSourceLine);
        var managementPeriod = managementResource.MonthlyAllocations.First().PeriodLabel;
        managementResource.HourlyRate = 125m;
        managementResource[managementPeriod] = 50m;
        Assert.Single(viewModel.ManagementResources);
        Assert.Equal(50m, viewModel.ManagementResourceAllocationRows.Single()[managementPeriod]);
        Assert.Equal(80m, viewModel.ManagementResourceHoursRows.Single()[managementPeriod]);
        Assert.Equal(10000m, viewModel.ManagementResourceCostRows.Single()[managementPeriod]);
        managementResource[managementPeriod] = 25m;
        Assert.Equal(5000m, managementSourceLine[managementPeriod]);
        managementSourceLine[managementPeriod] = 2500m;
        viewModel.SynchronizeManagementResourcesFromForecastLines([managementSourceLine]);
        Assert.Equal(12.5m, managementResource[managementPeriod]);
        viewModel.AddManagementResource(managementSourceLine);
        Assert.Single(viewModel.ManagementResources);

        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "management.json");
        var managementDataset = new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Management persistence fixture", CurrentPeriod = managementPeriod },
            ForecastPeriods = managementResource.MonthlyAllocations
                .Select(allocation => new ForecastPeriod
                {
                    Label = allocation.PeriodLabel,
                    StartDate = allocation.PeriodStartDate
                })
                .ToList(),
            ManagementResources = [managementResource]
        };
        new ProjectFileService().Save(path, managementDataset);
        var reloadedManagementResource = new ProjectFileService().Load(path).ManagementResources.Single();
        Assert.Equal(12.5m, reloadedManagementResource[managementPeriod]);
        Assert.Equal(125m, reloadedManagementResource.HourlyRate);
    }

    [Fact]
    public void Management_resource_default_rates_and_batch_notifications_are_deterministic()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var ratePeriods = viewModel.CtcMonthForecastColumns
            .Where(column => !column.IsTotal)
            .Select(column => column.Key)
            .TakeLast(2)
            .ToArray();
        var ratePreviousPeriod = ratePeriods[0];
        var rateLatestPeriod = ratePeriods[1];
        var rateLine = new ForecastLine
        {
            RowNumber = 900001,
            TaskNumber = "RATE-001",
            ResourceName = "Rate Person",
            ProjectCode = "Rate Test"
        };
        rateLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = rateLatestPeriod, Amount = 12000m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 150m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 150m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 999m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Rate Person", UnitRate = 150m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Rate Person", UnitRate = 175m });
        Assert.Equal(150m, viewModel.CalculateManagementResourceDefaultRate(rateLine));
        var rateResource = viewModel.AddManagementResource(rateLine);
        Assert.Equal(150m, rateResource.HourlyRate);
        Assert.Equal(50m, rateResource[rateLatestPeriod]);
        rateResource.OverrideHourlyRate(175m);
        Assert.True(rateResource.IsHourlyRateOverridden);
        viewModel.ResetManagementResourceRate(rateResource);
        Assert.Equal(150m, rateResource.HourlyRate);

        var tieLine = new ForecastLine
        {
            RowNumber = 900002,
            TaskNumber = "RATE-002",
            ResourceName = "Tie Person",
            ProjectCode = "Rate Test"
        };
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Tie Person", UnitRate = 200m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Tie Person", UnitRate = 200m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Tie Person", UnitRate = 250m });
        viewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Tie Person", UnitRate = 250m });
        Assert.Equal(250m, viewModel.CalculateManagementResourceDefaultRate(tieLine));

        var batchedResource = new ManagementResource();
        var indexerNotifications = 0;
        batchedResource.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == "Item[]")
            {
                indexerNotifications++;
            }
        };
        Assert.True(batchedResource.SetAllocations(Enumerable.Range(1, 36)
            .Select(month => new KeyValuePair<string, decimal>($"PERIOD-{month}", month))));
        Assert.Equal(1, indexerNotifications);

        var noOpBatchCollection = new BatchObservableCollection<string>(["One", "Two"]);
        var resets = 0;
        noOpBatchCollection.CollectionChanged += (_, _) => resets++;
        noOpBatchCollection.ReplaceWith(["One", "Two"]);
        Assert.Equal(0, resets);
        noOpBatchCollection.ReplaceWith(["One", "Changed"]);
        Assert.Equal(1, resets);
    }

    [Fact]
    public void Comment_history_curve_presets_and_ledger_drilldown_keep_the_selected_resource_context()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var hoveredLine = viewModel.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, "Flex Projects L", StringComparison.OrdinalIgnoreCase));
        viewModel.SelectedForecastLine = viewModel.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
        var commentLine = viewModel.SelectedForecastLine!;
        commentLine.MonthlyCommentHistory.Clear();
        commentLine.MonthlyCommentHistory.Add(new ForecastMonthlyComment
        {
            PeriodLabel = "26-08",
            MonthLabel = "Feb 26",
            ResourceName = commentLine.ResourceName,
            Text = "Earlier comment",
            RecordedAt = new DateTime(2026, 4, 1)
        });
        viewModel.SaveForecastLineCommentEditor(
            commentLine,
            ResourceCommentMetricPreference.CreateDefaults(),
            "additional cost due to more effort required",
            "month pressure",
            string.Empty);
        var seed = Luna11TestSupport.LoadSeedDataset();
        var currentCommentMonthLabel = seed.ForecastPeriods
            .First(period => string.Equals(period.Label, viewModel.Header.CurrentPeriod, StringComparison.OrdinalIgnoreCase))
            .StartDate
            ?.ToString("MMM yy") ?? string.Empty;
        Assert.StartsWith(
            $"{currentCommentMonthLabel} - FY {viewModel.Header.CurrentPeriod}: Stanley Drake:",
            commentLine.AllMonthComments);
        Assert.Contains("additional cost due to more effort required; month pressure", commentLine.AllMonthComments);
        var commentReportRow = viewModel.MonthlyReportVarianceCommentRows.Single(row =>
            string.Equals(row.ProjectCode, commentLine.ReportingCategory, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Stanley Drake: additional cost due to more effort required", commentReportRow.TotalBudgetVarianceComment);
        Assert.Contains("Stanley Drake: month pressure", commentReportRow.MonthVarianceComment);
        Assert.StartsWith(
            $"{currentCommentMonthLabel} - FY {viewModel.Header.CurrentPeriod}: Stanley Drake:",
            commentReportRow.AllMonthComments);
        viewModel.SaveManualForecastComment(commentLine, "Manual forecast explanation");
        Assert.True(commentLine.UseManualAllMonthComment);
        Assert.Contains("Stanley Drake: Manual forecast explanation", commentLine.AllMonthComments);
        viewModel.SetForecastCommentMode(commentLine, false);
        Assert.False(commentLine.UseManualAllMonthComment);
        Assert.Equal("Manual forecast explanation", commentLine.ManualAllMonthComment);
        viewModel.SetForecastCommentMode(commentLine, true);

        var frontHeavyCurve = ForecastCurvePresets.Apply(ForecastCurvePresets.FrontHeavy, [100m, 100m, 100m, 100m]);
        Assert.Equal(400m, frontHeavyCurve.Sum());
        Assert.True(frontHeavyCurve[0] > frontHeavyCurve[^1]);
        var backHeavyCurve = ForecastCurvePresets.Apply(ForecastCurvePresets.BackHeavy, [100m, 100m, 100m, 100m]);
        Assert.True(backHeavyCurve[0] < backHeavyCurve[^1]);
        var cumulativeCurve = ForecastCurveMath.BuildCumulative([6000m, 6000m, 7200m]);
        Assert.Equal([6000m, 12000m, 19200m], cumulativeCurve);
        var movedCumulativeCurve = ForecastCurveMath.MoveCumulativePoint(cumulativeCurve, 1, 15000m);
        var movedMonthlyCurve = ForecastCurveMath.ToMonthlyValues(movedCumulativeCurve);
        Assert.Equal([6000m, 9000m, 4200m], movedMonthlyCurve);
        Assert.Equal(19200m, movedMonthlyCurve.Sum());
        var clampedCumulativeCurve = ForecastCurveMath.MoveCumulativePoint(cumulativeCurve, 1, 25000m);
        Assert.Equal(19200m, clampedCumulativeCurve[1]);
        var smoothCurve = ForecastCurveMath.AdjustMonthlyCurve([100m, 100m, 100m, 100m, 100m, 100m], 2, 350m, 2);
        Assert.Equal(600m, smoothCurve.Sum());
        Assert.True(smoothCurve[1] != 100m && smoothCurve[2] != 100m && smoothCurve[3] != 100m && smoothCurve[4] != 100m);
        Assert.All(smoothCurve, value => Assert.True(value >= 0));
        var wideCurve = ForecastCurveMath.AdjustMonthlyCurve([100m, 100m, 100m, 100m, 100m, 100m], 2, 350m, 4);
        Assert.True(wideCurve[0] != 100m && wideCurve[5] != 100m);
        var lockedCurve = ForecastCurveMath.AdjustMonthlyCurve(
            [100m, 100m, 100m, 100m],
            1,
            260m,
            4,
            [false, true, false, false]);
        Assert.Equal(100m, lockedCurve[1]);
        Assert.Equal(400m, lockedCurve.Sum());
        var capturedCurveShape = ForecastCurvePresets.CaptureShape([10m, 30m, 60m]);
        Assert.Equal(1m, capturedCurveShape.Sum());
        var userCurvePreset = new UserForecastCurvePreset
        {
            Name = "Test shape",
            MonthCount = 3,
            Weights = capturedCurveShape.ToList()
        };
        var appliedUserCurve = ForecastCurvePresets.ApplyUserPreset(userCurvePreset, [100m, 100m, 100m]);
        Assert.Equal(300m, appliedUserCurve.Sum());
        Assert.True(appliedUserCurve[0] < appliedUserCurve[1] && appliedUserCurve[1] < appliedUserCurve[2]);
        var savedUserPresetCount = viewModel.UserForecastCurvePresets.Count;
        var savedUserPreset = viewModel.SaveForecastCurvePreset(
            "Reusable curve",
            "test note",
            "Stanley Drake",
            300m,
            capturedCurveShape);
        Assert.Equal(savedUserPresetCount + 1, viewModel.UserForecastCurvePresets.Count);
        Assert.Equal("Reusable curve", savedUserPreset.Name);
        viewModel.DeleteForecastCurvePreset(savedUserPreset);
        Assert.Equal(savedUserPresetCount, viewModel.UserForecastCurvePresets.Count);

        viewModel.SetHoveredForecastLine(hoveredLine);
        Assert.Equal("Flex Projects L / WA57102001", viewModel.LedgerTitle);
        viewModel.ClearHoveredForecastLine();
        Assert.Equal("Stanley Drake / WA57102001", viewModel.LedgerTitle);
        Assert.Equal("Oct 25\n26-04", viewModel.LedgerChartXAxisLabels.First().Text);
        Assert.Equal("Oct 26\n27-04", viewModel.LedgerChartXAxisLabels.Last().Text);
        Assert.All(viewModel.LedgerChartXAxisLabels, label => Assert.Contains('\n', label.Text));
        Assert.Equal(13, viewModel.LedgerChartXAxisLabels.Count);
        Assert.True(viewModel.LedgerChartCanvasWidth > 800);
        Assert.All(viewModel.LedgerChartXAxisLabels, label =>
        {
            var calendarLabel = label.Text.Split('\n')[0];
            Assert.False(calendarLabel.EndsWith("27", StringComparison.OrdinalIgnoreCase));
            Assert.False(calendarLabel.EndsWith("28", StringComparison.OrdinalIgnoreCase));
        });
    }
}
