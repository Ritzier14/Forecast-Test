using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11WorkspaceCoverageTests
{
    [Fact]
    public void Forecast_and_detail_workspace_views_preserve_independent_display_and_pivot_state()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        viewModel.ActiveWorkspaceKey = "CTC Forecast";
        var defaultForecastView = viewModel.SelectedWorkspaceView!;
        viewModel.GroupForecastLinesByTask = true;
        viewModel.SetSelectedForecastShowZeroAsBlank(false);
        Assert.True(defaultForecastView.GroupForecastLinesByTask);
        Assert.False(defaultForecastView.ShowZeroAsBlank);
        viewModel.AddWorkspaceViewCommand.Execute(null);
        var customForecastView = viewModel.SelectedWorkspaceView!;
        Assert.True(customForecastView.GroupForecastLinesByTask);
        Assert.False(customForecastView.ShowZeroAsBlank);
        viewModel.GroupForecastLinesByTask = false;
        viewModel.SetSelectedForecastShowZeroAsBlank(true);
        viewModel.SelectedWorkspaceView = defaultForecastView;
        Assert.True(viewModel.GroupForecastLinesByTask);
        Assert.False(viewModel.ShowForecastZeroAsBlank);
        viewModel.SelectedWorkspaceView = customForecastView;
        Assert.False(viewModel.GroupForecastLinesByTask);
        Assert.True(viewModel.ShowForecastZeroAsBlank);

        viewModel.ActiveDetailWorkspaceKey = "Ledger Costs";
        var defaultLedgerCostView = viewModel.SelectedDetailWorkspaceView!;
        viewModel.SetSelectedDetailWorkspaceHiddenColumnKeys(["Supplier", "Narrative 2"]);
        Assert.Equal(["Narrative 2", "Supplier"], defaultLedgerCostView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        viewModel.AddDetailWorkspaceViewCommand.Execute(null);
        var customLedgerCostView = viewModel.SelectedDetailWorkspaceView!;
        Assert.Equal(defaultLedgerCostView.HiddenColumnKeys, customLedgerCostView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        viewModel.SetSelectedDetailWorkspaceHiddenColumnKeys(["ECM Number"]);
        viewModel.SelectedDetailWorkspaceView = defaultLedgerCostView;
        Assert.Equal(["Narrative 2", "Supplier"], defaultLedgerCostView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        viewModel.SelectedDetailWorkspaceView = customLedgerCostView;
        Assert.Equal(["ECM Number"], customLedgerCostView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);

        viewModel.SelectedForecastLine = viewModel.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
        viewModel.SetSelectedDetailWorkspaceContentKey("PivotByMonth");
        Assert.True(viewModel.ShowLedgerCostsPivotByMonth);
        Assert.Equal("26-07", viewModel.LedgerMonthlyPivotPeriods.First());
        Assert.Contains("26-08", viewModel.LedgerMonthlyPivotPeriods);
        Assert.Contains("26-09", viewModel.LedgerMonthlyPivotPeriods);
        Assert.Equal(viewModel.Header.CurrentPeriod, viewModel.LedgerMonthlyPivotPeriods.Last());
        Assert.Equal(15000m, viewModel.LedgerMonthlyPivotRows.Single(row =>
            string.Equals(row.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase)).Total);

        viewModel.ActiveWorkspaceKey = "Raw Transactions";
        viewModel.SetSelectedWorkspaceContentKey("PivotByMonth");
        Assert.True(viewModel.ShowRawTransactionsPivotByMonth);
        Assert.Contains("26-08", viewModel.RawTransactionsMonthlyPivotPeriods);
        Assert.Contains("26-09", viewModel.RawTransactionsMonthlyPivotPeriods);
        Assert.Equal(viewModel.Header.CurrentPeriod, viewModel.RawTransactionsMonthlyPivotPeriods.Last());
        viewModel.SetSelectedWorkspaceContentKey("GroupByMonth");
        Assert.True(viewModel.ShowRawTransactionsGroupedByMonth);
        Assert.False(viewModel.ShowRawTransactionsPivotByMonth);

        var pivotBuilderViewModel = Luna11TestSupport.CreateSeedViewModel();
        var projectCodePivotField = pivotBuilderViewModel.PivotFields.Single(field => field.Key == "ProjectCode");
        Assert.False(pivotBuilderViewModel.TryAddPivotFieldToArea("Filters", projectCodePivotField));
        var projectCodeRowField = pivotBuilderViewModel.PivotRowFields.Single(field => field.Key == "ProjectCode");
        Assert.True(pivotBuilderViewModel.TryMovePivotFieldToArea("Filters", projectCodeRowField));
        Assert.Contains(pivotBuilderViewModel.PivotFilterFields, field => field.Key == "ProjectCode");
        Assert.DoesNotContain(pivotBuilderViewModel.PivotRowFields, field => field.Key == "ProjectCode");
        Assert.Equal("Sum of Amount", pivotBuilderViewModel.PivotValueFields.Single(field => field.Key == "Amount").DisplayName);
    }
}
