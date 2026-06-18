using System.Globalization;

namespace ProjectCostForecast.App.Services;

public static class KpiComparisonFormatter
{
    public static string Format(decimal currentValue, decimal previousValue)
    {
        if (previousValue == 0)
        {
            return string.Empty;
        }

        var percentage = Math.Abs((currentValue - previousValue) / previousValue * 100m);
        var direction = currentValue > previousValue ? "↑" : currentValue < previousValue ? "↓" : "→";
        return $"{direction} {percentage.ToString("0.#", CultureInfo.CurrentCulture)}% vs last month";
    }

    public static string GetDirection(decimal currentValue, decimal previousValue)
    {
        return currentValue > previousValue ? "Up" : currentValue < previousValue ? "Down" : "Flat";
    }
}
