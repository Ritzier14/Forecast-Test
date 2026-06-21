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
    private void RefreshLedgerSelectionSnapshots()
    {
        _activeLedgerTransactions = BuildLedgerTransactionsForCurrentSelection();
        _activeLedgerForecastEntries = BuildLedgerForecastEntriesForCurrentSelection();
        RecalculateLedgerTotals();
    }

    private void RecalculateLedgerTotals()
    {
        var totals = new LedgerTotals
        {
            TransactionCount = _activeLedgerTransactions.Count
        };

        foreach (var transaction in _activeLedgerTransactions)
        {
            totals.TransactionTotal += transaction.Amount;
            totals.UnitsTotal += transaction.Units;
        }

        foreach (var forecast in _activeLedgerForecastEntries)
        {
            totals.ForecastTotal += forecast.Amount;
        }

        totals.ProjectedTotal = totals.TransactionTotal + totals.ForecastTotal;
        totals.AverageRate = totals.UnitsTotal == 0 ? 0 : totals.TransactionTotal / totals.UnitsTotal;
        _ledgerTotals = totals;
    }

    private bool _showLedgerResourceAcrossAllTasks;

    public bool ShowLedgerResourceAcrossAllTasks
    {
        get => _showLedgerResourceAcrossAllTasks;
        set
        {
            if (SetProperty(ref _showLedgerResourceAcrossAllTasks, value))
            {
                NotifyLedgerChanged();
            }
        }
    }

    private IReadOnlyList<CostTransaction> BuildLedgerTransactionsForCurrentSelection()
    {
        var activeForecastLine = GetActiveLedgerForecastLine();
        if (activeForecastLine is not null)
        {
            return Transactions
                .Where(transaction => ShowLedgerResourceAcrossAllTasks
                    ? CalculationService.MatchesForecastResource(transaction, activeForecastLine)
                    : CalculationService.MatchesForecastLine(transaction, activeForecastLine))
                .ToList();
        }

        if (SelectedResourceSummary is not null)
        {
            var selectedResource = CalculationService.Normalise(SelectedResourceSummary.ResourceName);
            return Transactions
                .Where(transaction => string.Equals(
                    CalculationService.Normalise(transaction.LedgerResourceName),
                    selectedResource,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return [];
    }

    private IReadOnlyList<MonthlyForecast> BuildLedgerForecastEntriesForCurrentSelection()
    {
        var activeForecastLine = GetActiveLedgerForecastLine();
        if (activeForecastLine is not null)
        {
            return activeForecastLine.MonthlyForecasts;
        }

        if (SelectedResourceSummary is not null)
        {
            var selectedResource = CalculationService.Normalise(SelectedResourceSummary.ResourceName);
            return ForecastLines
                .Where(line => string.Equals(
                    CalculationService.Normalise(line.ResourceName),
                    selectedResource,
                    StringComparison.OrdinalIgnoreCase))
                .SelectMany(line => line.MonthlyForecasts)
                .ToList();
        }

        return [];
    }

    private void RebuildLedgerChart()
    {
        var actualByMonth = LedgerTransactions
            .Where(tx => tx.DocDate.HasValue)
            .GroupBy(tx =>
            {
                var date = tx.DocDate!.Value;
                return new DateOnly(date.Year, date.Month, 1);
            })
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Sum(tx => tx.Amount));

        var forecastByMonth = _activeLedgerForecastEntries
            .Where(entry => entry.PeriodStartDate.HasValue && entry.Amount != 0m)
            .GroupBy(entry =>
            {
                var date = entry.PeriodStartDate!.Value;
                return new DateOnly(date.Year, date.Month, 1);
            })
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Amount));

        if (GetActiveLedgerForecastLine() is null && SelectedResourceSummary is null)
        {
            ClearLedgerChart("Select a resource to see the spend curve.");
            return;
        }

        if (actualByMonth.Count == 0 && forecastByMonth.Count == 0)
        {
            ClearLedgerChart("No dated actuals or monthly forecast entries are available for this selection.");
            return;
        }

        var keyedMonths = actualByMonth.Keys
            .Concat(forecastByMonth.Keys)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var fullDomainStart = GetLedgerChartStartMonth(keyedMonths);
        var domainEnd = keyedMonths[^1];
        if (domainEnd <= fullDomainStart)
        {
            domainEnd = fullDomainStart.AddMonths(1);
        }

        var domainStart = GetVisibleLedgerChartStart(fullDomainStart, domainEnd);
        var fullTimeline = EnumerateMonthStarts(fullDomainStart, domainEnd).ToList();
        LedgerChartCanvasWidth = CalculateLedgerChartWidth(domainStart, domainEnd);

        var actualPoints = new PointCollection();
        var forecastPoints = new PointCollection();
        var actualRunning = 0m;
        var forecastRunning = 0m;
        var maxValue = 0m;

        actualPoints.Add(new Point(MapChartX(domainStart, domainStart, domainEnd), MapChartY(0, 1)));
        forecastPoints.Add(new Point(MapChartX(domainStart, domainStart, domainEnd), MapChartY(0, 1)));

        foreach (var date in fullTimeline)
        {
            actualRunning += actualByMonth.GetValueOrDefault(date);
            forecastRunning += forecastByMonth.GetValueOrDefault(date);
            if (date < domainStart)
            {
                continue;
            }

            var projected = actualRunning + forecastRunning;
            maxValue = Math.Max(maxValue, Math.Max(actualRunning, projected));
        }

        var yAxisMax = GetNiceAxisMaximum(maxValue);

        actualRunning = 0m;
        forecastRunning = 0m;
        var plottedStart = false;

        foreach (var date in fullTimeline)
        {
            actualRunning += actualByMonth.GetValueOrDefault(date);
            forecastRunning += forecastByMonth.GetValueOrDefault(date);
            if (date < domainStart)
            {
                continue;
            }

            var actualValue = actualRunning;
            var projectedValue = actualRunning + forecastRunning;
            if (!plottedStart)
            {
                actualPoints[0] = new Point(MapChartX(domainStart, domainStart, domainEnd), MapChartY(actualValue, yAxisMax));
                forecastPoints[0] = new Point(MapChartX(domainStart, domainStart, domainEnd), MapChartY(projectedValue, yAxisMax));
                plottedStart = true;
            }

            var x = MapChartX(date, domainStart, domainEnd);
            actualPoints.Add(new Point(x, MapChartY(actualValue, yAxisMax)));
            forecastPoints.Add(new Point(x, MapChartY(projectedValue, yAxisMax)));
        }

        LedgerActualChartPoints = actualPoints;
        LedgerForecastChartPoints = forecastPoints;
        LedgerActualChartGeometry = BuildSmoothChartGeometry(actualPoints);
        LedgerForecastChartGeometry = BuildSmoothChartGeometry(forecastPoints);

        BuildLedgerChartAxes(domainStart, domainEnd, yAxisMax);
        LedgerChartStatusText = $"Showing {SelectedLedgerChartRangeOption?.Name?.ToLowerInvariant() ?? "the selected range"}, ending at the latest month. Solid line is cumulative actual spend by month; dotted line adds forecast.";
    }

    private void ClearLedgerChart(string message)
    {
        LedgerActualChartPoints = [];
        LedgerForecastChartPoints = [];
        LedgerActualChartGeometry = Geometry.Empty;
        LedgerForecastChartGeometry = Geometry.Empty;
        LedgerChartCanvasWidth = LedgerChartMinWidth;
        ReplaceCollection(LedgerChartGridLines, []);
        ReplaceCollection(LedgerChartXAxisLabels, []);
        ReplaceCollection(LedgerChartYAxisLabels, []);
        LedgerChartStatusText = message;
    }

    private ForecastLine? GetActiveLedgerForecastLine() => _hoveredForecastLine ?? SelectedForecastLine;

    private DateOnly GetLedgerChartStartMonth(IReadOnlyList<DateOnly> timeline)
    {
        var firstProjectCost = Transactions
            .Where(tx => tx.DocDate.HasValue)
            .Select(tx => tx.DocDate!.Value)
            .OrderBy(date => date)
            .FirstOrDefault();

        var startDate = firstProjectCost == default
            ? timeline[0]
            : firstProjectCost;

        return new DateOnly(startDate.Year, startDate.Month, 1);
    }

    private DateOnly GetVisibleLedgerChartStart(DateOnly fullDomainStart, DateOnly domainEnd)
    {
        var visibleMonths = SelectedLedgerChartRangeOption?.VisibleMonths;
        if (visibleMonths is null || visibleMonths <= 0)
        {
            return fullDomainStart;
        }

        var requestedStart = new DateOnly(domainEnd.Year, domainEnd.Month, 1)
            .AddMonths(-(visibleMonths.Value - 1));
        return requestedStart > fullDomainStart ? requestedStart : fullDomainStart;
    }

    private void BuildLedgerChartAxes(DateOnly domainStart, DateOnly domainEnd, decimal yAxisMax)
    {
        var plotLeft = LedgerChartLeftPadding;
        var plotTop = LedgerChartTopPadding;
        var plotRight = LedgerChartCanvasWidth - LedgerChartRightPadding;
        var plotBottom = LedgerChartHeight - LedgerChartBottomPadding;
        var gridLines = new List<ChartLineSegment>();
        var yLabels = new List<ChartLabel>();
        var xLabels = new List<ChartLabel>();

        for (var step = 0; step <= 4; step++)
        {
            var ratio = step / 4d;
            var y = plotBottom - ((plotBottom - plotTop) * ratio);
            gridLines.Add(new ChartLineSegment
            {
                X1 = plotLeft,
                Y1 = y,
                X2 = plotRight,
                Y2 = y
            });

            var value = yAxisMax * (decimal)ratio;
            yLabels.Add(new ChartLabel
            {
                X = 4,
                Y = y - 10,
                Text = FormatCompactCurrency(value)
            });
        }

        foreach (var monthStart in EnumerateMonthStarts(domainStart, domainEnd))
        {
            var x = MapChartX(monthStart, domainStart, domainEnd);
            if (x > plotLeft)
            {
                gridLines.Add(new ChartLineSegment
                {
                    X1 = x,
                    Y1 = plotTop,
                    X2 = x,
                    Y2 = plotBottom
                });
            }

            xLabels.Add(new ChartLabel
            {
                X = x - 24,
                Y = plotBottom + 8,
                Text = $"{monthStart:MMM yy}\n{FormatFiscalPeriodForCalendarMonth(monthStart)}"
            });
        }

        gridLines.Add(new ChartLineSegment
        {
            X1 = plotLeft,
            Y1 = plotTop,
            X2 = plotLeft,
            Y2 = plotBottom
        });

        gridLines.Add(new ChartLineSegment
        {
            X1 = plotLeft,
            Y1 = plotBottom,
            X2 = plotRight,
            Y2 = plotBottom
        });

        ReplaceCollection(LedgerChartGridLines, gridLines);
        ReplaceCollection(LedgerChartXAxisLabels, xLabels);
        ReplaceCollection(LedgerChartYAxisLabels, yLabels);
    }

    private double CalculateLedgerChartWidth(DateOnly domainStart, DateOnly domainEnd)
    {
        var monthCount = CountMonthsInclusive(domainStart, domainEnd);
        var monthSpacing = SelectedLedgerChartRangeOption?.MonthSpacing ?? DefaultLedgerChartMonthSpacing;
        var plotWidth = Math.Max(
            LedgerChartMinWidth - LedgerChartLeftPadding - LedgerChartRightPadding,
            Math.Max(1, monthCount - 1) * monthSpacing);

        return LedgerChartLeftPadding + plotWidth + LedgerChartRightPadding;
    }

    private static IEnumerable<DateOnly> EnumerateMonthStarts(DateOnly domainStart, DateOnly domainEnd)
    {
        var cursor = new DateOnly(domainStart.Year, domainStart.Month, 1);
        var lastMonthStart = new DateOnly(domainEnd.Year, domainEnd.Month, 1);
        while (cursor <= lastMonthStart)
        {
            yield return cursor;
            cursor = cursor.AddMonths(1);
        }
    }

    private static Geometry BuildSmoothChartGeometry(PointCollection points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = false,
            IsFilled = false
        };

        if (points.Count == 1)
        {
            figure.Segments.Add(new LineSegment(points[0], true));
        }
        else
        {
            for (var index = 0; index < points.Count - 1; index++)
            {
                var p0 = index == 0 ? points[index] : points[index - 1];
                var p1 = points[index];
                var p2 = points[index + 1];
                var p3 = index + 2 < points.Count ? points[index + 2] : p2;

                var control1 = new Point(
                    p1.X + (p2.X - p0.X) / 6d,
                    p1.Y + (p2.Y - p0.Y) / 6d);
                var control2 = new Point(
                    p2.X - (p3.X - p1.X) / 6d,
                    p2.Y - (p3.Y - p1.Y) / 6d);

                figure.Segments.Add(new BezierSegment(control1, control2, p2, true));
            }
        }

        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }

    private static int CountMonthsInclusive(DateOnly domainStart, DateOnly domainEnd)
    {
        return ((domainEnd.Year - domainStart.Year) * 12) + domainEnd.Month - domainStart.Month + 1;
    }

    private static string FormatFiscalPeriodForCalendarMonth(DateOnly monthStart)
    {
        var fiscalYear = monthStart.Month >= 7 ? monthStart.Year + 1 : monthStart.Year;
        var fiscalMonth = monthStart.Month >= 7 ? monthStart.Month - 6 : monthStart.Month + 6;
        return FiscalPeriod.FormatLabel(fiscalYear, fiscalMonth);
    }

    private double MapChartX(DateOnly date, DateOnly domainStart, DateOnly domainEnd)
    {
        var plotWidth = LedgerChartCanvasWidth - LedgerChartLeftPadding - LedgerChartRightPadding;
        var totalDays = Math.Max(1, domainEnd.DayNumber - domainStart.DayNumber);
        var elapsedDays = date.DayNumber - domainStart.DayNumber;
        return LedgerChartLeftPadding + (plotWidth * elapsedDays / totalDays);
    }

    private static double MapChartY(decimal value, decimal yAxisMax)
    {
        var plotHeight = LedgerChartHeight - LedgerChartTopPadding - LedgerChartBottomPadding;
        var safeMax = yAxisMax <= 0 ? 1 : yAxisMax;
        var ratio = (double)(value / safeMax);
        return LedgerChartHeight - LedgerChartBottomPadding - (plotHeight * ratio);
    }

    private static decimal GetNiceAxisMaximum(decimal value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var raw = (double)value;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var scaled = raw / magnitude;
        var rounded = scaled <= 1
            ? 1
            : scaled <= 2
                ? 2
                : scaled <= 5
                    ? 5
                    : 10;
        return (decimal)(rounded * magnitude);
    }

    private static string FormatCompactCurrency(decimal value)
    {
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000m)
        {
            return $"${value / 1_000_000m:0.#}m";
        }

        if (absolute >= 1_000m)
        {
            return $"${value / 1_000m:0.#}k";
        }

        return $"${value:0}";
    }
}
