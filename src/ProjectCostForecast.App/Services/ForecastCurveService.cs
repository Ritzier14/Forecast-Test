using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public enum ForecastCurveProfile
{
    Linear,
    SCurve,
    FrontLoaded,
    BackLoaded,
    Bell
}

public sealed class ForecastCurveService
{
    public static IReadOnlyList<ForecastCurveProfile> Profiles { get; } =
        [ForecastCurveProfile.Linear, ForecastCurveProfile.SCurve, ForecastCurveProfile.FrontLoaded, ForecastCurveProfile.BackLoaded, ForecastCurveProfile.Bell];

    public static string DescribeProfile(ForecastCurveProfile profile) => profile switch
    {
        ForecastCurveProfile.SCurve => "S-curve (slow start, fast middle, slow finish)",
        ForecastCurveProfile.FrontLoaded => "Front loaded (heaviest spend first)",
        ForecastCurveProfile.BackLoaded => "Back loaded (heaviest spend last)",
        ForecastCurveProfile.Bell => "Bell (peak in the middle)",
        _ => "Linear (even spread)"
    };

    /// <summary>
    /// Distributes a total across a number of periods following the given profile.
    /// Amounts are rounded to 2dp and the residual is folded into the largest period
    /// so the result always sums exactly to the requested total.
    /// </summary>
    public static List<decimal> Distribute(decimal total, int periodCount, ForecastCurveProfile profile)
        => ForecastCurveMath.Distribute(total, periodCount, profile).ToList();

    public int ApplyCurve(
        ForecastLine line,
        IReadOnlyList<MonthlyForecast> targetMonths,
        decimal total,
        ForecastCurveProfile profile)
    {
        var editableMonths = targetMonths.Where(month => !month.IsLocked).ToList();
        if (editableMonths.Count == 0)
        {
            return 0;
        }

        var amounts = Distribute(total, editableMonths.Count, profile);
        for (var index = 0; index < editableMonths.Count; index++)
        {
            editableMonths[index].Amount = amounts[index];
        }

        line.NotifyMonthForecastValuesChanged();
        return editableMonths.Count;
    }
}
