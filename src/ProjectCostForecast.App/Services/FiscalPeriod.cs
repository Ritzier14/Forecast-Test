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

    /// <summary>
    /// Converts a July-to-June fiscal period label into the corresponding
    /// calendar-month start. The fiscal year number is the year in which the
    /// fiscal year ends, so period 26-07 is January 2026 and period 27-01 is
    /// July 2026.
    /// </summary>
    public static bool TryGetCalendarMonthStart(string? periodLabel, out DateOnly calendarMonthStart)
    {
        calendarMonthStart = default;
        if (!TryParseLabel(periodLabel, out var fiscalYear, out var fiscalMonth))
        {
            return false;
        }

        var calendarYear = fiscalMonth >= 7 ? fiscalYear : fiscalYear - 1;
        var calendarMonth = ((fiscalMonth + 5) % 12) + 1;
        calendarMonthStart = new DateOnly(calendarYear, calendarMonth, 1);
        return true;
    }

    public static string LabelFromCalendarMonth(DateOnly calendarMonthStart)
    {
        var monthStart = new DateOnly(calendarMonthStart.Year, calendarMonthStart.Month, 1);
        var fiscalYear = monthStart.Month >= 7 ? monthStart.Year + 1 : monthStart.Year;
        var fiscalMonth = ((monthStart.Month + 5) % 12) + 1;
        return FormatLabel(fiscalYear, fiscalMonth);
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
