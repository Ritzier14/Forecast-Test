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
    {
        if (periodCount <= 0)
        {
            return [];
        }

        if (periodCount == 1)
        {
            return [decimal.Round(total, 2, MidpointRounding.AwayFromZero)];
        }

        var weights = new double[periodCount];
        var weightSum = 0d;
        for (var index = 0; index < periodCount; index++)
        {
            weights[index] = Weight(profile, index, periodCount);
            weightSum += weights[index];
        }

        var amounts = new List<decimal>(periodCount);
        decimal allocated = 0;
        var largestIndex = 0;
        for (var index = 0; index < periodCount; index++)
        {
            var amount = decimal.Round(total * (decimal)(weights[index] / weightSum), 2, MidpointRounding.AwayFromZero);
            amounts.Add(amount);
            allocated += amount;
            if (Math.Abs(amount) > Math.Abs(amounts[largestIndex]))
            {
                largestIndex = index;
            }
        }

        amounts[largestIndex] += total - allocated;
        return amounts;
    }

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

    private static double Weight(ForecastCurveProfile profile, int index, int periodCount)
    {
        // Mid-point of the period on a normalised 0..1 timeline.
        var t = (index + 0.5) / periodCount;
        return profile switch
        {
            ForecastCurveProfile.FrontLoaded => 2.0 * (1.0 - t),
            ForecastCurveProfile.BackLoaded => 2.0 * t,
            ForecastCurveProfile.Bell => Math.Sin(Math.PI * t),
            ForecastCurveProfile.SCurve => SCurveWeight(index, periodCount),
            _ => 1.0
        };
    }

    private static double SCurveWeight(int index, int periodCount)
    {
        // Per-period weight is the slice of a logistic cumulative curve, which gives the
        // classic construction S-curve when the amounts are accumulated.
        const double steepness = 8.0;
        var start = Logistic((double)index / periodCount, steepness);
        var end = Logistic((double)(index + 1) / periodCount, steepness);
        return end - start;
    }

    private static double Logistic(double x, double steepness)
    {
        return 1.0 / (1.0 + Math.Exp(-steepness * (x - 0.5)));
    }
}
