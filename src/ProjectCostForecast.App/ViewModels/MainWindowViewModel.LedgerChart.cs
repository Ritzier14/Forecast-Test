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
    private sealed record LedgerChartBucket(DateOnly StartDate, DateOnly EndDate, IReadOnlyList<DateOnly> Months, string Label);

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
            .ToDictionary(group => group.Key, group => group.Sum(tx => tx.Amount));

        var forecastByMonth = _activeLedgerForecastEntries
            .Where(entry => entry.PeriodStartDate.HasValue && entry.Amount != 0m)
            .GroupBy(entry =>
            {
                var date = entry.PeriodStartDate!.Value;
                return new DateOnly(date.Year, date.Month, 1);
            })
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
        var fullBuckets = BuildLedgerChartBuckets(fullTimeline);
        var visibleBuckets = fullBuckets
            .Where(bucket => bucket.EndDate >= domainStart && bucket.StartDate <= domainEnd)
            .ToList();
        if (visibleBuckets.Count == 0)
        {
            ClearLedgerChart("No periods are available for this selection.");
            return;
        }

        LedgerChartCanvasWidth = CalculateLedgerChartWidth(visibleBuckets.Count);
        var budgetTotal = GetActiveLedgerBudgetTotal();
        var budgetPerMonth = fullTimeline.Count == 0 ? 0m : budgetTotal / fullTimeline.Count;
        var actualPoints = new PointCollection();
        var forecastPoints = new PointCollection();
        var budgetPoints = new PointCollection();
        var actualRunning = 0m;
        var forecastRunning = 0m;
        var budgetRunning = 0m;
        var visibleIndex = 0;
        var maxValue = 0m;
        var plottedStart = false;

        foreach (var bucket in fullBuckets)
        {
            foreach (var month in bucket.Months)
            {
                actualRunning += actualByMonth.GetValueOrDefault(month);
                forecastRunning += forecastByMonth.GetValueOrDefault(month);
                budgetRunning += budgetPerMonth;
            }

            if (!visibleBuckets.Contains(bucket))
            {
                continue;
            }

            var actualValue = actualRunning;
            var projectedValue = actualRunning + forecastRunning;
            var budgetValue = budgetRunning;
            if (!plottedStart)
            {
                actualPoints.Add(new Point(MapChartX(0, visibleBuckets.Count), MapChartY(actualValue, 1)));
                forecastPoints.Add(new Point(MapChartX(0, visibleBuckets.Count), MapChartY(projectedValue, 1)));
                budgetPoints.Add(new Point(MapChartX(0, visibleBuckets.Count), MapChartY(budgetValue, 1)));
                plottedStart = true;
            }

            maxValue = Math.Max(maxValue, GetVisibleLedgerChartMaximum(actualValue, projectedValue, budgetValue));
            visibleIndex++;
        }

        var yAxisMax = GetNiceAxisMaximum(maxValue);
        actualRunning = 0m;
        forecastRunning = 0m;
        budgetRunning = 0m;
        visibleIndex = 0;
        actualPoints.Clear();
        forecastPoints.Clear();
        budgetPoints.Clear();
        var hasPlottedStart = false;

        foreach (var bucket in fullBuckets)
        {
            foreach (var month in bucket.Months)
            {
                actualRunning += actualByMonth.GetValueOrDefault(month);
                forecastRunning += forecastByMonth.GetValueOrDefault(month);
                budgetRunning += budgetPerMonth;
            }

            if (!visibleBuckets.Contains(bucket))
            {
                continue;
            }

            var x = MapChartX(visibleIndex, visibleBuckets.Count);
            var actualValue = actualRunning;
            var projectedValue = actualRunning + forecastRunning;
            if (!hasPlottedStart)
            {
                actualPoints.Add(new Point(x, MapChartY(actualValue, yAxisMax)));
                forecastPoints.Add(new Point(x, MapChartY(projectedValue, yAxisMax)));
                budgetPoints.Add(new Point(x, MapChartY(budgetRunning, yAxisMax)));
                hasPlottedStart = true;
            }

            actualPoints.Add(new Point(x, MapChartY(actualValue, yAxisMax)));
            forecastPoints.Add(new Point(x, MapChartY(projectedValue, yAxisMax)));
            budgetPoints.Add(new Point(x, MapChartY(budgetRunning, yAxisMax)));
            visibleIndex++;
        }

        LedgerActualChartPoints = actualPoints;
        LedgerForecastChartPoints = forecastPoints;
        LedgerBudgetChartPoints = budgetPoints;
        LedgerActualChartGeometry = BuildSmoothChartGeometry(actualPoints);
        LedgerForecastChartGeometry = BuildSmoothChartGeometry(forecastPoints);
        LedgerBudgetChartGeometry = BuildSmoothChartGeometry(budgetPoints);

        BuildLedgerChartAxes(visibleBuckets, yAxisMax);
        LedgerChartStatusText = $"Showing {SelectedLedgerChartRangeOption?.Name?.ToLowerInvariant() ?? "the selected range"} at {LedgerChartTimeScale.ToString().ToLowerInvariant()} scale. Solid line is cumulative actual spend; dotted line adds forecast; dashed gray line is budget.";
    }

    private decimal GetActiveLedgerBudgetTotal()
    {
        var activeLine = GetActiveLedgerForecastLine();
        if (activeLine is not null)
        {
            return Math.Max(0m, activeLine.Budget);
        }

        if (SelectedResourceSummary is null)
        {
            return 0m;
        }

        var resource = CalculationService.Normalise(SelectedResourceSummary.ResourceName);
        return Math.Max(0m, ForecastLines
            .Where(line => string.Equals(CalculationService.Normalise(line.ResourceName), resource, StringComparison.OrdinalIgnoreCase))
            .Sum(line => line.Budget));
    }

    private decimal GetVisibleLedgerChartMaximum(decimal actual, decimal projected, decimal budget)
    {
        var values = new List<decimal>();
        if (ShowLedgerActualSeries)
        {
            values.Add(actual);
        }

        if (ShowLedgerForecastSeries)
        {
            values.Add(projected);
        }

        if (ShowLedgerBudgetSeries)
        {
            values.Add(budget);
        }

        return values.Count == 0 ? 0m : Math.Max(0m, values.Max());
    }

    private void ClearLedgerChart(string message)
    {
        LedgerActualChartPoints = [];
        LedgerForecastChartPoints = [];
        LedgerBudgetChartPoints = [];
        LedgerActualChartGeometry = Geometry.Empty;
        LedgerForecastChartGeometry = Geometry.Empty;
        LedgerBudgetChartGeometry = Geometry.Empty;
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

    private void BuildLedgerChartAxes(IReadOnlyList<LedgerChartBucket> buckets, decimal yAxisMax)
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

        for (var index = 0; index < buckets.Count; index++)
        {
            var bucket = buckets[index];
            var x = MapChartX(index, buckets.Count);
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
                Text = bucket.Label
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

    private double CalculateLedgerChartWidth(int bucketCount)
    {
        var monthSpacing = SelectedLedgerChartRangeOption?.MonthSpacing ?? DefaultLedgerChartMonthSpacing;
        var scaleMultiplier = LedgerChartTimeScale switch
        {
            LedgerChartTimeScale.Month => 1d,
            LedgerChartTimeScale.Quarter => 1.1d,
            LedgerChartTimeScale.HalfYear => 1.2d,
            LedgerChartTimeScale.Year => 1.35d,
            _ => 1d
        };
        var plotWidth = Math.Max(
            LedgerChartMinWidth - LedgerChartLeftPadding - LedgerChartRightPadding,
            Math.Max(1, bucketCount - 1) * monthSpacing * scaleMultiplier);

        return LedgerChartLeftPadding + plotWidth + LedgerChartRightPadding;
    }

    private IReadOnlyList<LedgerChartBucket> BuildLedgerChartBuckets(IReadOnlyList<DateOnly> timeline)
    {
        if (timeline.Count == 0)
        {
            return [];
        }

        var buckets = new List<LedgerChartBucket>();
        var currentMonths = new List<DateOnly>();
        string? currentKey = null;

        foreach (var month in timeline)
        {
            var key = LedgerChartTimeScale switch
            {
                LedgerChartTimeScale.Quarter => $"{month.Year}-Q{((month.Month - 1) / 3) + 1}",
                LedgerChartTimeScale.HalfYear => $"{month.Year}-H{(month.Month <= 6 ? 1 : 2)}",
                LedgerChartTimeScale.Year => month.Year.ToString(),
                _ => $"{month.Year}-{month.Month:00}"
            };

            if (currentKey is not null && !string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                buckets.Add(CreateLedgerChartBucket(currentMonths, LedgerChartTimeScale));
                currentMonths = [];
            }

            currentKey = key;
            currentMonths.Add(month);
        }

        if (currentMonths.Count > 0)
        {
            buckets.Add(CreateLedgerChartBucket(currentMonths, LedgerChartTimeScale));
        }

        return buckets;
    }

    private static LedgerChartBucket CreateLedgerChartBucket(IReadOnlyList<DateOnly> months, LedgerChartTimeScale scale)
    {
        var start = months[0];
        var end = months[^1];
        var label = MainWindowViewModelChartLabel(start, scale);
        return new LedgerChartBucket(start, end, months.ToList(), label);
    }

    private static string MainWindowViewModelChartLabel(DateOnly start, LedgerChartTimeScale scale)
    {
        return scale switch
        {
            LedgerChartTimeScale.Quarter => $"Q{((start.Month - 1) / 3) + 1} {start.Year}",
            LedgerChartTimeScale.HalfYear => $"H{(start.Month <= 6 ? 1 : 2)} {start.Year}",
            LedgerChartTimeScale.Year => start.Year.ToString(),
            _ => $"{start:MMM yy}\n{FormatFiscalPeriodForCalendarMonth(start)}"
        };
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

    private double MapChartX(int index, int count)
    {
        var plotWidth = LedgerChartCanvasWidth - LedgerChartLeftPadding - LedgerChartRightPadding;
        var ratio = count <= 1 ? 0d : Math.Clamp(index / (double)(count - 1), 0d, 1d);
        return LedgerChartLeftPadding + plotWidth * ratio;
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
