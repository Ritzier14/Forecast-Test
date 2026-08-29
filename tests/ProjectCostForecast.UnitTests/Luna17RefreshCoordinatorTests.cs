using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna17RefreshCoordinatorTests
{
    [Fact]
    public void Overlapping_requests_merge_into_one_execution_with_explicit_targets()
    {
        var scheduled = new List<Action>();
        var executed = new List<RefreshRequest>();
        using var coordinator = new RefreshCoordinator(scheduled.Add, executed.Add);

        coordinator.Request(new RefreshRequest(
            RefreshProjection.CalculatedViews | RefreshProjection.ForecastLinesView,
            "forecast edit",
            Recalculate: true));
        coordinator.Request(new RefreshRequest(
            RefreshProjection.RawTransactionsView | RefreshProjection.RawTransactionsPivot,
            "import",
            RebuildFilterLists: true));
        coordinator.Request(new RefreshRequest(
            RefreshProjection.Totals | RefreshProjection.Ledger,
            "workspace selection",
            MarkDirty: true));

        Assert.Single(scheduled);
        Assert.Empty(executed);

        scheduled[0]();

        var request = Assert.Single(executed);
        Assert.Equal(
            RefreshProjection.CalculatedViews
            | RefreshProjection.ForecastLinesView
            | RefreshProjection.RawTransactionsView
            | RefreshProjection.RawTransactionsPivot
            | RefreshProjection.Totals
            | RefreshProjection.Ledger,
            request.Projections);
        Assert.Equal("workspace selection", request.Reason);
        Assert.True(request.Recalculate);
        Assert.True(request.RebuildFilterLists);
        Assert.True(request.MarkDirty);
        var diagnostics = coordinator.Diagnostics.Snapshot();
        Assert.Equal(3, diagnostics.RequestedRefreshes);
        Assert.Equal(2, diagnostics.CoalescedRefreshes);
        Assert.Equal(1, diagnostics.ExecutedRefreshes);
    }

    [Fact]
    public void Batch_requests_are_held_until_the_operation_completes()
    {
        var scheduled = new List<Action>();
        var executed = new List<RefreshRequest>();
        using var coordinator = new RefreshCoordinator(scheduled.Add, executed.Add);

        coordinator.BeginBatch();
        coordinator.Request(new RefreshRequest(RefreshProjection.ForecastLinesView));
        coordinator.Request(new RefreshRequest(RefreshProjection.ForecastGrouping));

        Assert.Empty(scheduled);
        coordinator.EndBatch();

        Assert.Single(scheduled);
        scheduled[0]();
        Assert.Single(executed);
        Assert.Equal(
            RefreshProjection.ForecastLinesView | RefreshProjection.ForecastGrouping,
            executed[0].Projections);
    }

    [Fact]
    public void Full_view_model_refresh_has_one_calculation_and_one_pivot_rebuild_and_preserves_state()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var selectedLine = viewModel.ForecastLines.First();
        viewModel.SelectedForecastLine = selectedLine;
        viewModel.SelectedProjectCode = selectedLine.ProjectCode;
        viewModel.SelectedMonthlyVarianceFilter = "Any variance";
        viewModel.SelectedBudgetVarianceFilter = "Under budget";
        viewModel.IsDirty = false;

        var before = viewModel.RefreshDiagnostics;
        viewModel.RecalculateCommand.Execute(null);
        viewModel.FlushPendingRefreshes();
        var after = viewModel.RefreshDiagnostics;

        Assert.Same(selectedLine, viewModel.SelectedForecastLine);
        Assert.Equal(selectedLine.ProjectCode, viewModel.SelectedProjectCode);
        Assert.Equal("Any variance", viewModel.SelectedMonthlyVarianceFilter);
        Assert.Equal("Under budget", viewModel.SelectedBudgetVarianceFilter);
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.Calculation) - before.GetPhaseCount(RefreshPhase.Calculation));
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.CalculatedViews) - before.GetPhaseCount(RefreshPhase.CalculatedViews));
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.CollectionViews) - before.GetPhaseCount(RefreshPhase.CollectionViews));
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.RawTransactionsPivot) - before.GetPhaseCount(RefreshPhase.RawTransactionsPivot));
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.Totals) - before.GetPhaseCount(RefreshPhase.Totals));
        Assert.Equal(1, after.GetPhaseCount(RefreshPhase.Ledger) - before.GetPhaseCount(RefreshPhase.Ledger));

        var selectedResource = viewModel.ResourceSummaries.First();
        viewModel.SelectedResourceSummary = selectedResource;
        var resourceName = selectedResource.ResourceName;
        viewModel.RecalculateCommand.Execute(null);
        viewModel.FlushPendingRefreshes();

        Assert.NotNull(viewModel.SelectedResourceSummary);
        Assert.Equal(resourceName, viewModel.SelectedResourceSummary!.ResourceName);
    }
}
