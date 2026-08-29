using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna20PerformanceContractTests
{
    [Fact]
    public void Forecast_only_edit_rebuilds_calculated_views_without_rebuilding_raw_transactions_pivot()
    {
        using var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var forecast = viewModel.ForecastLines
            .SelectMany(line => line.MonthlyForecasts)
            .First(month => month.IsEditable);
        var before = viewModel.RefreshDiagnostics;

        forecast.Amount += 1m;
        viewModel.FlushPendingRefreshes();

        var after = viewModel.RefreshDiagnostics;
        Assert.Equal(
            1,
            after.GetPhaseCount(RefreshPhase.CalculatedViews)
            - before.GetPhaseCount(RefreshPhase.CalculatedViews));
        Assert.Equal(
            before.GetPhaseCount(RefreshPhase.RawTransactionsPivot),
            after.GetPhaseCount(RefreshPhase.RawTransactionsPivot));
    }
}
