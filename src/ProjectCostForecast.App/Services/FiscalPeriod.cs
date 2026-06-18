namespace ProjectCostForecast.App.Services;

public static class FiscalPeriod
{
    public static string NormaliseLabel(string? periodLabel)
    {
        return string.IsNullOrWhiteSpace(periodLabel)
            ? string.Empty
            : periodLabel.Trim();
    }

    public static string FormatLabel(int year, int month)
    {
        return $"{year % 100:00}-{month:00}";
    }

    public static bool TryParseLabel(string? periodLabel, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (string.IsNullOrWhiteSpace(periodLabel))
        {
            return false;
        }

        var parts = periodLabel.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var shortYear)
            || !int.TryParse(parts[1], out month)
            || month is < 1 or > 12)
        {
            return false;
        }

        year = shortYear >= 70 ? 1900 + shortYear : 2000 + shortYear;
        return true;
    }

    public static int SortKey(string? periodLabel)
    {
        if (TryParseLabel(periodLabel, out var year, out var month))
        {
            return (year * 100) + month;
        }

        return int.MaxValue;
    }

    public static List<string> BuildContinuousRange(int startYear, int startMonth, int endYear, int endMonth)
    {
        var periods = new List<string>();
        var year = startYear;
        var month = startMonth;
        while (year < endYear || (year == endYear && month <= endMonth))
        {
            periods.Add(FormatLabel(year, month));
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        return periods;
    }

    public static string FiscalYearFromPeriodLabel(string? periodLabel)
    {
        var normalised = CalculationService.Normalise(periodLabel);
        if (normalised.Length < 2)
        {
            return string.Empty;
        }

        var yearPart = normalised.StartsWith("FY", StringComparison.OrdinalIgnoreCase)
            ? normalised[2..]
            : normalised[..2];

        return int.TryParse(yearPart, out _) ? $"FY{yearPart}" : string.Empty;
    }

    public static bool TryParseFiscalYearNumber(string? fiscalYear, out int year)
    {
        var normalised = CalculationService.Normalise(fiscalYear);
        if (normalised.StartsWith("FY", StringComparison.OrdinalIgnoreCase))
        {
            normalised = normalised[2..];
        }

        return int.TryParse(normalised, out year);
    }
}
