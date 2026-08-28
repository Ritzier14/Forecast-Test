using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11CurveCoverageTests
{
    [Fact]
    public void Forecast_curve_profiles_preserve_totals_and_expected_shapes()
    {
        var linearSpread = ForecastCurveService.Distribute(1000m, 4, ForecastCurveProfile.Linear);
        Assert.Equal(4, linearSpread.Count);
        Assert.Equal(1000m, linearSpread.Sum());
        Assert.Equal(250m, linearSpread[0]);

        var frontLoaded = ForecastCurveService.Distribute(1200m, 6, ForecastCurveProfile.FrontLoaded);
        Assert.Equal(1200m, frontLoaded.Sum());
        Assert.True(frontLoaded[0] > frontLoaded[5]);
        var backLoaded = ForecastCurveService.Distribute(1200m, 6, ForecastCurveProfile.BackLoaded);
        Assert.True(backLoaded[5] > backLoaded[0]);

        var sCurve = ForecastCurveService.Distribute(10000m, 10, ForecastCurveProfile.SCurve);
        Assert.Equal(10000m, sCurve.Sum());
        Assert.True(sCurve[4] > sCurve[0] && sCurve[4] > sCurve[9]);
        Assert.True(Math.Abs(sCurve[0] - sCurve[9]) < 50m);

        var bell = ForecastCurveService.Distribute(999.99m, 5, ForecastCurveProfile.Bell);
        Assert.Equal(999.99m, bell.Sum());
        Assert.True(bell[2] > bell[0]);
        var singlePeriod = ForecastCurveService.Distribute(500m, 1, ForecastCurveProfile.SCurve);
        Assert.Equal(500m, singlePeriod[0]);
        Assert.Empty(ForecastCurveService.Distribute(500m, 0, ForecastCurveProfile.Linear));
    }

    [Fact]
    public void Forecast_curve_application_respects_locked_months_and_preserves_open_total()
    {
        var curveLine = new ForecastLine { RowNumber = 999, ResourceName = "Curve test" };
        curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "26-11", Amount = 1m });
        curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "26-12", Amount = 2m, IsLocked = true });
        curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "27-01", Amount = 3m });
        var applied = new ForecastCurveService().ApplyCurve(
            curveLine,
            curveLine.MonthlyForecasts,
            600m,
            ForecastCurveProfile.Linear);
        Assert.Equal(2, applied);
        Assert.Equal(2m, curveLine.MonthlyForecasts[1].Amount);
        Assert.Equal(600m, curveLine.MonthlyForecasts[0].Amount + curveLine.MonthlyForecasts[2].Amount);
    }
}
