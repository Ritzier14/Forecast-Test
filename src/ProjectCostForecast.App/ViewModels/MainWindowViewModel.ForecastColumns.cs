using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RebuildCtcMonthForecastYearOptions()
    {
        ApplyClosedForecastPeriodRule();

        var years = _dataset.ForecastPeriods
            .Where(period => period.StartDate.HasValue)
            .Select(period => period.StartDate!.Value.Year)
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        ReplaceCollection(AvailableCtcMonthForecastYears, years);
        OnPropertyChanged(nameof(IsCtcMonthForecastSelectionAvailable));

        if (AvailableCtcMonthForecastYears.Count == 0)
        {
            _selectedCtcMonthForecastYear = 0;
            _selectedCtcMonthForecastYears.Clear();
            _dataset.SelectedCtcMonthForecastYears = [];
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
            ReplaceCollection(CtcMonthForecastColumns, []);
            return;
        }

        var savedYears = (_dataset.SelectedCtcMonthForecastYears ?? [])
            .Where(year => AvailableCtcMonthForecastYears.Contains(year))
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        if (savedYears.Count > 0)
        {
            _selectedCtcMonthForecastYears.Clear();
            foreach (var year in savedYears)
            {
                _selectedCtcMonthForecastYears.Add(year);
            }

            _selectedCtcMonthForecastYear = savedYears.First();
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYear));
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
        }
        else if (!AvailableCtcMonthForecastYears.Contains(SelectedCtcMonthForecastYear))
        {
            var preferredYear = GetCurrentForecastCalendarYear();
            if (preferredYear is null || !AvailableCtcMonthForecastYears.Contains(preferredYear.Value))
            {
                preferredYear = AvailableCtcMonthForecastYears.First();
            }

            _selectedCtcMonthForecastYear = preferredYear.Value;
            _selectedCtcMonthForecastYears.Clear();
            _selectedCtcMonthForecastYears.Add(preferredYear.Value);
            _dataset.SelectedCtcMonthForecastYears = [preferredYear.Value];
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYear));
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
        }
        else if (_selectedCtcMonthForecastYears.Count == 0)
        {
            _selectedCtcMonthForecastYears.Add(SelectedCtcMonthForecastYear);
            _dataset.SelectedCtcMonthForecastYears = [SelectedCtcMonthForecastYear];
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
        }

        RebuildCtcMonthForecastColumns();
    }

    private void RebuildCtcMonthForecastColumns()
    {
        var selectedYears = _selectedCtcMonthForecastYears
            .Where(year => AvailableCtcMonthForecastYears.Contains(year))
            .OrderBy(year => year)
            .ToList();

        if (selectedYears.Count == 0)
        {
            ReplaceCollection(CtcMonthForecastColumns, []);
            return;
        }

        _dataset.SelectedCtcMonthForecastYears = selectedYears;

        var selectedPeriods = _dataset.ForecastPeriods
            .Where(period => period.StartDate is { } startDate && selectedYears.Contains(startDate.Year))
            .OrderBy(period => period.StartDate)
            .ToList();

        var calendarYearOrder = selectedPeriods
            .Select(period => period.StartDate!.Value.Year)
            .Distinct()
            .OrderBy(year => year)
            .ToList();
        var fiscalYearOrder = selectedPeriods
            .Select(period => FiscalPeriod.FiscalYearFromPeriodLabel(period.Label))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var periodColumns = selectedPeriods
            .Select((period, index) =>
            {
                var calendarYear = period.StartDate!.Value.Year;
                var isLocked = IsForecastPeriodLocked(period.StartDate);
                var fiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(period.Label);
                var monthLabel = period.StartDate?.ToString("MMM") ?? string.Empty;
                var primaryLabel = ShowMonthNameAboveFiscalPeriod ? monthLabel : period.Label;
                var secondaryLabel = ShowMonthNameAboveFiscalPeriod ? period.Label : monthLabel;
                var calendarBandIndex = calendarYearOrder.IndexOf(calendarYear);
                var fiscalBandIndex = fiscalYearOrder.FindIndex(label => string.Equals(label, fiscalYear, StringComparison.OrdinalIgnoreCase));
                var monthRowBackground = GetForecastCalendarYearHeaderBrush(calendarYear.ToString(), calendarBandIndex);
                var fiscalRowBackground = GetForecastFiscalYearHeaderBrush(fiscalYear, fiscalBandIndex);
                var primaryBackground = ShowMonthNameAboveFiscalPeriod
                    ? monthRowBackground
                    : fiscalRowBackground;
                var secondaryBackground = ShowMonthNameAboveFiscalPeriod
                    ? fiscalRowBackground
                    : monthRowBackground;
                var previousFiscalYear = index > 0
                    ? FiscalPeriod.FiscalYearFromPeriodLabel(selectedPeriods[index - 1].Label)
                    : string.Empty;

                return new ForecastMonthColumnDefinition
                {
                    Key = period.Label,
                    YearLabel = calendarYear.ToString(),
                    PrimaryLabel = primaryLabel,
                    SecondaryLabel = secondaryLabel,
                    PrimaryBackground = primaryBackground,
                    SecondaryBackground = secondaryBackground,
                    ValueBackground = isLocked ? BrushFactory.Frozen(0xF3, 0xF4, 0xF6) : Brushes.White,
                    ValueForeground = isLocked ? BrushFactory.Frozen(0x94, 0xA3, 0xB8) : BrushFactory.Frozen(0x0F, 0x17, 0x2A),
                    LeftDashedSeparatorVisibility = string.Equals(previousFiscalYear, fiscalYear, StringComparison.OrdinalIgnoreCase) || index == 0
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                    RightDashedSeparatorVisibility = index + 1 < selectedPeriods.Count
                        && !string.Equals(
                            fiscalYear,
                            FiscalPeriod.FiscalYearFromPeriodLabel(selectedPeriods[index + 1].Label),
                            StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed,
                    IsEditable = !isLocked
                };
            })
            .ToList();

        if (ShowCtcMonthForecastYearTotals)
        {
            foreach (var calendarYear in selectedYears)
            {
                var insertIndex = periodColumns.FindLastIndex(column =>
                    !column.IsTotal
                    && string.Equals(column.YearLabel, calendarYear.ToString(), StringComparison.OrdinalIgnoreCase)) + 1;
                var totalColumn = new ForecastMonthColumnDefinition
                {
                    Key = $"TOTAL:{calendarYear}",
                    YearLabel = calendarYear.ToString(),
                    PrimaryLabel = calendarYear.ToString(),
                    SecondaryLabel = "Total",
                    PrimaryBackground = Brushes.White,
                    SecondaryBackground = Brushes.White,
                    ValueBackground = Brushes.White,
                    ValueForeground = BrushFactory.Frozen(0x0F, 0x17, 0x2A),
                    LeftSolidSeparatorVisibility = Visibility.Visible,
                    RightSolidSeparatorVisibility = Visibility.Visible,
                    IsEditable = false,
                    IsTotal = true
                };

                if (insertIndex <= 0 || insertIndex > periodColumns.Count)
                {
                    periodColumns.Add(totalColumn);
                }
                else
                {
                    periodColumns.Insert(insertIndex, totalColumn);
                }
            }
        }

        ReplaceCollection(CtcMonthForecastColumns, periodColumns);
    }

    public Brush GetForecastCalendarYearHeaderBrush(string yearLabel, int bandIndex = 0)
    {
        if (_dataset.ForecastCalendarYearHeaderColorHexes.TryGetValue(yearLabel, out var overrideHex)
            && IsValidHexColour(overrideHex))
        {
            return BrushFactory.FrozenHeaderGradient(overrideHex);
        }

        return bandIndex % 2 == 0
            ? BrushFactory.FrozenVerticalGradient("#CFE5FA", "#A6C8E8")
            : BrushFactory.FrozenVerticalGradient("#F9E1C4", "#EFC18C");
    }

    public Brush GetForecastFiscalYearHeaderBrush(string fiscalYearLabel, int bandIndex = 0)
    {
        if (_dataset.ForecastFiscalYearHeaderColorHexes.TryGetValue(fiscalYearLabel, out var overrideHex)
            && IsValidHexColour(overrideHex))
        {
            return BrushFactory.FrozenHeaderGradient(overrideHex);
        }

        return bandIndex % 2 == 0
            ? BrushFactory.FrozenVerticalGradient("#D7ECCF", "#B4D49A")
            : BrushFactory.FrozenVerticalGradient("#FFF0B8", "#F0D37A");
    }

    public string? GetForecastCalendarYearHeaderColorHex(string yearLabel)
    {
        return _dataset.ForecastCalendarYearHeaderColorHexes.GetValueOrDefault(yearLabel);
    }

    public string? GetForecastFiscalYearHeaderColorHex(string fiscalYearLabel)
    {
        return _dataset.ForecastFiscalYearHeaderColorHexes.GetValueOrDefault(fiscalYearLabel);
    }

    public void SetForecastCalendarYearHeaderColor(string yearLabel, string? colorHex)
    {
        SetHeaderColor(_dataset.ForecastCalendarYearHeaderColorHexes, yearLabel, colorHex);
    }

    public void SetForecastFiscalYearHeaderColor(string fiscalYearLabel, string? colorHex)
    {
        SetHeaderColor(_dataset.ForecastFiscalYearHeaderColorHexes, fiscalYearLabel, colorHex);
    }

    private void SetHeaderColor(Dictionary<string, string> target, string key, string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(colorHex) || !IsValidHexColour(colorHex))
        {
            target.Remove(key);
        }
        else
        {
            target[key] = colorHex;
        }

        IsDirty = true;
        RebuildCtcMonthForecastColumns();
    }

    private static bool IsValidHexColour(string value)
    {
        return BrushFactory.IsValidHeaderGradientSpec(value)
               || BrushFactory.TryParseHexColor(value, out _);
    }

    public bool IsForecastPeriodLocked(DateOnly? periodStartDate)
    {
        return _forecastEditLockCutoffDate.HasValue
            && periodStartDate.HasValue
            && periodStartDate.Value < _forecastEditLockCutoffDate.Value;
    }

    public static ForecastPeriod? DetermineClosedForecastPeriod(IEnumerable<ForecastPeriod> periods, DateOnly asOfDate)
    {
        var cutoffMonthStart = new DateOnly(asOfDate.Year, asOfDate.Month, 1).AddMonths(-1);
        var datedPeriods = periods
            .Where(period => period.StartDate.HasValue)
            .OrderBy(period => period.StartDate)
            .ToList();

        if (datedPeriods.Count == 0)
        {
            return null;
        }

        return datedPeriods.LastOrDefault(period => period.StartDate!.Value <= cutoffMonthStart)
            ?? datedPeriods.First();
    }

    public static ForecastPeriod? DetermineExpectedWorkingPeriod(IEnumerable<ForecastPeriod> periods, DateOnly asOfDate)
    {
        return DetermineClosedForecastPeriod(periods, asOfDate);
    }

    private void ApplyClosedForecastPeriodRule()
    {
        var savedPeriod = _dataset.ForecastPeriods
            .Where(period => !string.IsNullOrWhiteSpace(period.Label))
            .FirstOrDefault(period => string.Equals(period.Label, Header.CurrentPeriod, StringComparison.OrdinalIgnoreCase));
        _forecastEditLockCutoffDate = savedPeriod?.StartDate;
        RefreshActivePeriodWarnings();
    }

    private void RefreshActivePeriodWarnings()
    {
        var warnings = BuildActivePeriodWarnings(_dataset.ForecastPeriods, Header.CurrentPeriod, DateOnly.FromDateTime(DateTime.Today));
        ReplaceCollection(ActivePeriodWarnings, warnings);
        OnPropertyChanged(nameof(ActivePeriodWarnings));
    }

    public static IReadOnlyList<string> BuildActivePeriodWarnings(IEnumerable<ForecastPeriod> periods, string? savedCurrentPeriod, DateOnly asOfDate)
    {
        var currentPeriod = FiscalPeriod.NormaliseLabel(savedCurrentPeriod);
        if (string.IsNullOrWhiteSpace(currentPeriod))
        {
            return ["Saved current period is missing; please save and create a new month."];
        }

        var periodList = periods
            .Where(period => !string.IsNullOrWhiteSpace(period.Label))
            .OrderBy(period => period.StartDate ?? DateOnly.MaxValue)
            .ThenBy(period => period.Label)
            .ToList();
        var savedPeriod = periodList.FirstOrDefault(period => string.Equals(period.Label, currentPeriod, StringComparison.OrdinalIgnoreCase));
        if (savedPeriod is null)
        {
            return [$"Saved current period {currentPeriod} was not found; please save and create a new month."];
        }

        var expectedWorkingPeriod = DetermineExpectedWorkingPeriod(periodList, asOfDate);
        if (expectedWorkingPeriod is null
            || string.Equals(savedPeriod.Label, expectedWorkingPeriod.Label, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return [$"Current active period is incorrect; please save and create a new month. Expected working period: {expectedWorkingPeriod.Label}; saved project period: {savedPeriod.Label}."];
    }

    private void ApplyForecastPeriodLockStates()
    {
        foreach (var forecast in ForecastLines.SelectMany(line => line.MonthlyForecasts))
        {
            forecast.IsLocked = IsForecastPeriodLocked(forecast.PeriodStartDate);
        }

        foreach (var forecast in _dataset.ForecastLines.SelectMany(line => line.MonthlyForecasts))
        {
            forecast.IsLocked = IsForecastPeriodLocked(forecast.PeriodStartDate);
        }
    }


    private int? GetCurrentForecastCalendarYear()
    {
        var currentPeriod = CalculationService.Normalise(Header.CurrentPeriod);
        if (string.IsNullOrWhiteSpace(currentPeriod))
        {
            return null;
        }

        return _dataset.ForecastPeriods
            .FirstOrDefault(period => string.Equals(period.Label, currentPeriod, StringComparison.OrdinalIgnoreCase))
            ?.StartDate
            ?.Year;
    }

    private string FindProjectCodeForTask(string taskNumber)
    {
        return _projectCodeByTaskNumber.TryGetValue(CalculationService.Normalise(taskNumber), out var projectCode)
            ? projectCode
            : string.Empty;
    }

    private void RebuildForecastLineLookups()
    {
        _projectCodeByTaskNumber.Clear();
        _taskNumbersByProjectCode.Clear();

        foreach (var line in ForecastLines)
        {
            var taskNumber = CalculationService.Normalise(line.TaskNumber);
            if (string.IsNullOrWhiteSpace(taskNumber))
            {
                continue;
            }

            if (!_projectCodeByTaskNumber.ContainsKey(taskNumber))
            {
                _projectCodeByTaskNumber[taskNumber] = line.ProjectCode;
            }

            var projectCode = CalculationService.Normalise(line.ProjectCode);
            if (string.IsNullOrWhiteSpace(projectCode))
            {
                continue;
            }

            if (!_taskNumbersByProjectCode.TryGetValue(projectCode, out var taskNumbers))
            {
                taskNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _taskNumbersByProjectCode[projectCode] = taskNumbers;
            }

            taskNumbers.Add(taskNumber);
        }
    }
}
