using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public const string P3mBudgetLineKey = "P3M";
    public const string LtpApBudgetLineKey = "LTP_AP";

    private const double BudgetChartMinWidth = 920;
    private const double BudgetChartHeight = 300;
    private const double BudgetChartLeftPadding = 58;
    private const double BudgetChartTopPadding = 18;
    private const double BudgetChartRightPadding = 20;
    private const double BudgetChartBottomPadding = 34;

    private bool _suppressBudgetChanges;
    private Geometry _budgetActualChartGeometry = Geometry.Empty;
    private Geometry _budgetForecastChartGeometry = Geometry.Empty;
    private Geometry _budgetPlanChartGeometry = Geometry.Empty;
    private double _budgetChartCanvasWidth = BudgetChartMinWidth;
    private string _budgetChartStatusText = "Enter a budget to compare it with spend and forecast.";

    public ObservableCollection<FiscalYearBudgetLine> BudgetLines { get; private set; } = null!;
    public ObservableCollection<string> BudgetFiscalYears { get; private set; } = null!;
    public ObservableCollection<ChartLineSegment> BudgetChartGridLines { get; private set; } = null!;
    public ObservableCollection<ChartLabel> BudgetChartXAxisLabels { get; private set; } = null!;
    public ObservableCollection<ChartLabel> BudgetChartYAxisLabels { get; private set; } = null!;

    public Geometry BudgetActualChartGeometry
    {
        get => _budgetActualChartGeometry;
        private set => SetProperty(ref _budgetActualChartGeometry, value);
    }

    public Geometry BudgetForecastChartGeometry
    {
        get => _budgetForecastChartGeometry;
        private set => SetProperty(ref _budgetForecastChartGeometry, value);
    }

    public Geometry BudgetPlanChartGeometry
    {
        get => _budgetPlanChartGeometry;
        private set => SetProperty(ref _budgetPlanChartGeometry, value);
    }

    public double BudgetChartCanvasWidth
    {
        get => _budgetChartCanvasWidth;
        private set => SetProperty(ref _budgetChartCanvasWidth, value);
    }

    public string BudgetChartStatusText
    {
        get => _budgetChartStatusText;
        private set => SetProperty(ref _budgetChartStatusText, value);
    }

    public string ActiveBudgetLineName => BudgetLines?.FirstOrDefault(line => line.IsActive)?.Name ?? "None";

    private void InitializeBudgetCollections()
    {
        BudgetLines = CreateCollection<FiscalYearBudgetLine>();
        BudgetFiscalYears = CreateCollection<string>();
        BudgetChartGridLines = CreateCollection<ChartLineSegment>();
        BudgetChartXAxisLabels = CreateCollection<ChartLabel>();
        BudgetChartYAxisLabels = CreateCollection<ChartLabel>();
    }

    private void LoadBudgetLinesFromDataset()
    {
        UnsubscribeBudgetLineChanges();
        _suppressBudgetChanges = true;
        try
        {
            var fiscalYears = BuildBudgetFiscalYearList();
            ReplaceCollection(BudgetFiscalYears, fiscalYears);

            var persistedLines = _dataset.BudgetLines ?? [];
            var lines = persistedLines
                .Where(line => !string.IsNullOrWhiteSpace(line.Key))
                .ToList();

            var p3m = lines.FirstOrDefault(line => string.Equals(line.Key, P3mBudgetLineKey, StringComparison.OrdinalIgnoreCase));
            if (p3m is null)
            {
                p3m = new FiscalYearBudgetLine { Key = P3mBudgetLineKey, Name = "P3M" };
                lines.Insert(0, p3m);
            }

            var ltpAp = lines.FirstOrDefault(line => string.Equals(line.Key, LtpApBudgetLineKey, StringComparison.OrdinalIgnoreCase));
            if (ltpAp is null)
            {
                ltpAp = new FiscalYearBudgetLine
                {
                    Key = LtpApBudgetLineKey,
                    Name = "LTP/AP",
                    Amounts = (_dataset.FiscalYearBudgets ?? [])
                        .Select(budget => new FiscalYearBudgetAmount
                        {
                            FiscalYear = NormaliseBudgetFiscalYear(budget.FiscalYear),
                            Amount = budget.Budget
                        })
                        .Where(amount => !string.IsNullOrWhiteSpace(amount.FiscalYear))
                        .ToList()
                };
                lines.Add(ltpAp);
            }

            p3m.Name = "P3M";
            ltpAp.Name = "LTP/AP";
            EnsureBudgetAmounts(p3m, fiscalYears);
            EnsureBudgetAmounts(ltpAp, fiscalYears);

            var activeKey = lines.Any(line => string.Equals(line.Key, _dataset.ActiveBudgetLineKey, StringComparison.OrdinalIgnoreCase))
                ? _dataset.ActiveBudgetLineKey
                : LtpApBudgetLineKey;
            foreach (var line in lines)
            {
                line.IsActive = string.Equals(line.Key, activeKey, StringComparison.OrdinalIgnoreCase);
            }

            ReplaceCollection(BudgetLines, new[] { p3m, ltpAp });
            _dataset.BudgetLines = BudgetLines.ToList();
            _dataset.ActiveBudgetLineKey = activeKey;
            SyncLegacyFiscalYearBudgets();
        }
        finally
        {
            _suppressBudgetChanges = false;
        }

        SubscribeBudgetLineChanges();
        OnPropertyChanged(nameof(ActiveBudgetLineName));
        RebuildBudgetChart();
    }

    private List<string> BuildBudgetFiscalYearList()
    {
        var fiscalYears = _dataset.ForecastPeriods
            .Select(period => FiscalPeriod.FiscalYearFromPeriodLabel(period.Label))
            .Concat(_dataset.ForecastLines.SelectMany(line => line.MonthlyForecasts)
                .Select(forecast => FiscalPeriod.FiscalYearFromPeriodLabel(forecast.PeriodLabel)))
            .Concat(_dataset.Transactions.Select(transaction => FiscalPeriod.FiscalYearFromPeriodLabel(transaction.FyPeriod)))
            .Concat((_dataset.FiscalYearBudgets ?? []).Select(budget => NormaliseBudgetFiscalYear(budget.FiscalYear)))
            .Concat((_dataset.BudgetLines ?? []).SelectMany(line => line.Amounts ?? [])
                .Select(amount => NormaliseBudgetFiscalYear(amount.FiscalYear)))
            .Where(fiscalYear => !string.IsNullOrWhiteSpace(fiscalYear))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetBudgetFiscalYearSortKey)
            .ToList();

        var currentFiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(Header.CurrentPeriod);
        if (!string.IsNullOrWhiteSpace(currentFiscalYear)
            && !fiscalYears.Contains(currentFiscalYear, StringComparer.OrdinalIgnoreCase))
        {
            fiscalYears.Add(currentFiscalYear);
            fiscalYears = fiscalYears.OrderBy(GetBudgetFiscalYearSortKey).ToList();
        }

        return fiscalYears;
    }

    private static void EnsureBudgetAmounts(FiscalYearBudgetLine line, IReadOnlyList<string> fiscalYears)
    {
        line.Amounts ??= [];
        var amountsByYear = line.Amounts
            .Where(amount => !string.IsNullOrWhiteSpace(amount.FiscalYear))
            .GroupBy(amount => NormaliseBudgetFiscalYear(amount.FiscalYear), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Amount, StringComparer.OrdinalIgnoreCase);

        line.Amounts = fiscalYears
            .Select(fiscalYear => new FiscalYearBudgetAmount
            {
                FiscalYear = fiscalYear,
                Amount = amountsByYear.GetValueOrDefault(fiscalYear)
            })
            .ToList();
        line.NotifyTotalChanged();
    }

    private static string NormaliseBudgetFiscalYear(string? fiscalYear)
    {
        if (!FiscalPeriod.TryParseFiscalYearNumber(fiscalYear, out var year))
        {
            return string.Empty;
        }

        return $"FY{year % 100:00}";
    }

    private static int GetBudgetFiscalYearSortKey(string fiscalYear) =>
        FiscalPeriod.TryParseFiscalYearNumber(fiscalYear, out var year) ? year : int.MaxValue;

    private void SubscribeBudgetLineChanges()
    {
        foreach (var amount in BudgetLines.SelectMany(line => line.Amounts))
        {
            amount.PropertyChanged += BudgetAmountPropertyChanged;
        }
    }

    private void UnsubscribeBudgetLineChanges()
    {
        if (BudgetLines is null)
        {
            return;
        }

        foreach (var amount in BudgetLines.SelectMany(line => line.Amounts ?? []))
        {
            amount.PropertyChanged -= BudgetAmountPropertyChanged;
        }
    }

    private void BudgetAmountPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressBudgetChanges || e.PropertyName != nameof(FiscalYearBudgetAmount.Amount))
        {
            return;
        }

        var line = sender is FiscalYearBudgetAmount changedAmount
            ? BudgetLines.FirstOrDefault(candidate => candidate.Amounts.Contains(changedAmount))
            : null;
        line?.NotifyTotalChanged();
        SyncBudgetLinesToDataset();
        ReplaceCollection(FiscalYearReportLines, _calculationService.BuildFiscalYearReport(_dataset));
        RebuildMonthlyReport();
        RebuildBudgetChart();
        NotifyTotalsChanged();
        IsDirty = true;
        StatusText = $"{line?.Name ?? "Budget"} budget updated.";
    }

    public void SetActiveBudgetLine(FiscalYearBudgetLine? selectedLine)
    {
        if (selectedLine is null || IsViewingSavedMonth || selectedLine.IsActive)
        {
            return;
        }

        var previous = BudgetLines.FirstOrDefault(line => line.IsActive)?.Name ?? "None";
        foreach (var line in BudgetLines)
        {
            line.IsActive = ReferenceEquals(line, selectedLine);
        }

        SyncBudgetLinesToDataset();
        ReplaceCollection(FiscalYearReportLines, _calculationService.BuildFiscalYearReport(_dataset));
        RebuildMonthlyReport();
        RebuildBudgetChart();
        NotifyTotalsChanged();
        OnPropertyChanged(nameof(ActiveBudgetLineName));
        AddAuditEvent("Budget", selectedLine.Key, "ActiveBudget", previous, selectedLine.Name, "Selected active budget line");
        IsDirty = true;
        StatusText = $"{selectedLine.Name} is now the active budget.";
    }

    private void SyncBudgetLinesToDataset()
    {
        if (BudgetLines is null)
        {
            return;
        }

        _dataset.BudgetLines = BudgetLines.ToList();
        _dataset.ActiveBudgetLineKey = BudgetLines.FirstOrDefault(line => line.IsActive)?.Key ?? LtpApBudgetLineKey;
        SyncLegacyFiscalYearBudgets();
    }

    private void SyncLegacyFiscalYearBudgets()
    {
        var activeLine = BudgetLines.FirstOrDefault(line => line.IsActive)
            ?? BudgetLines.FirstOrDefault(line => string.Equals(line.Key, LtpApBudgetLineKey, StringComparison.OrdinalIgnoreCase));
        _dataset.FiscalYearBudgets = activeLine?.Amounts
            .Select(amount => new FiscalYearBudget { FiscalYear = amount.FiscalYear, Budget = amount.Amount })
            .ToList() ?? [];
    }

    private void RebuildBudgetChart()
    {
        if (BudgetLines is null)
        {
            return;
        }

        var actualByMonth = Transactions
            .Select(transaction => new { Month = GetCalendarMonthFromFiscalPeriod(transaction.FyPeriod), transaction.Amount })
            .Where(item => item.Month.HasValue)
            .GroupBy(item => item.Month!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        IEnumerable<ForecastLine> forecastLines = IsViewingSavedMonth
            ? _workingForecastLinesBeforeSavedMonthView ?? []
            : ForecastLines;
        var forecastByMonth = forecastLines
            .SelectMany(line => line.MonthlyForecasts)
            .Select(forecast => new
            {
                Month = forecast.PeriodStartDate is { } date
                    ? new DateOnly(date.Year, date.Month, 1)
                    : GetCalendarMonthFromFiscalPeriod(forecast.PeriodLabel),
                forecast.Amount
            })
            .Where(item => item.Month.HasValue && item.Amount != 0m)
            .GroupBy(item => item.Month!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var activeBudget = BudgetLines.FirstOrDefault(line => line.IsActive);
        var budgetByMonth = new Dictionary<DateOnly, decimal>();
        if (activeBudget is not null)
        {
            foreach (var annualAmount in activeBudget.Amounts)
            {
                if (!TryGetFiscalYearStart(annualAmount.FiscalYear, out var fiscalYearStart))
                {
                    continue;
                }

                var monthlyAmount = annualAmount.Amount / 12m;
                for (var monthOffset = 0; monthOffset < 12; monthOffset++)
                {
                    budgetByMonth[fiscalYearStart.AddMonths(monthOffset)] = monthlyAmount;
                }
            }
        }

        var months = actualByMonth.Keys
            .Concat(forecastByMonth.Keys)
            .Concat(budgetByMonth.Keys)
            .Distinct()
            .OrderBy(month => month)
            .ToList();
        if (months.Count == 0)
        {
            ClearBudgetChart("No dated spend, forecast, or budget data is available yet.");
            return;
        }

        var domainStart = months[0];
        var domainEnd = months[^1];
        if (domainEnd <= domainStart)
        {
            domainEnd = domainStart.AddMonths(1);
        }

        BudgetChartCanvasWidth = CalculateBudgetChartWidth(domainStart, domainEnd);
        var timeline = EnumerateMonthStarts(domainStart, domainEnd).ToList();
        var actualRunning = 0m;
        var forecastRunning = 0m;
        var budgetRunning = 0m;
        var values = new List<(DateOnly Month, decimal Actual, decimal Forecast, decimal Budget)>();
        foreach (var month in timeline)
        {
            actualRunning += actualByMonth.GetValueOrDefault(month);
            forecastRunning += forecastByMonth.GetValueOrDefault(month);
            budgetRunning += budgetByMonth.GetValueOrDefault(month);
            values.Add((month, actualRunning, actualRunning + forecastRunning, budgetRunning));
        }

        var axisMaximum = GetNiceAxisMaximum(values.SelectMany(value => new[] { value.Actual, value.Forecast, value.Budget }).DefaultIfEmpty(0m).Max());
        var actualPoints = new PointCollection(values.Select(value => new Point(
            MapBudgetChartX(value.Month, domainStart, domainEnd),
            MapBudgetChartY(value.Actual, axisMaximum))));
        var forecastPoints = new PointCollection(values.Select(value => new Point(
            MapBudgetChartX(value.Month, domainStart, domainEnd),
            MapBudgetChartY(value.Forecast, axisMaximum))));
        var budgetPoints = new PointCollection(values.Select(value => new Point(
            MapBudgetChartX(value.Month, domainStart, domainEnd),
            MapBudgetChartY(value.Budget, axisMaximum))));

        BudgetActualChartGeometry = BuildSmoothChartGeometry(actualPoints);
        BudgetForecastChartGeometry = BuildSmoothChartGeometry(forecastPoints);
        BudgetPlanChartGeometry = BuildSmoothChartGeometry(budgetPoints);
        BuildBudgetChartAxes(domainStart, domainEnd, axisMaximum);
        BudgetChartStatusText = $"Solid blue is cumulative spend to date, dotted teal is spend plus forecast, and purple is the active {activeBudget?.Name ?? "budget"} line.";
    }

    private void ClearBudgetChart(string message)
    {
        BudgetActualChartGeometry = Geometry.Empty;
        BudgetForecastChartGeometry = Geometry.Empty;
        BudgetPlanChartGeometry = Geometry.Empty;
        BudgetChartCanvasWidth = BudgetChartMinWidth;
        ReplaceCollection(BudgetChartGridLines, []);
        ReplaceCollection(BudgetChartXAxisLabels, []);
        ReplaceCollection(BudgetChartYAxisLabels, []);
        BudgetChartStatusText = message;
    }

    private void BuildBudgetChartAxes(DateOnly domainStart, DateOnly domainEnd, decimal yAxisMaximum)
    {
        var plotRight = BudgetChartCanvasWidth - BudgetChartRightPadding;
        var plotBottom = BudgetChartHeight - BudgetChartBottomPadding;
        var gridLines = new List<ChartLineSegment>();
        var xLabels = new List<ChartLabel>();
        var yLabels = new List<ChartLabel>();

        for (var step = 0; step <= 4; step++)
        {
            var ratio = step / 4d;
            var y = plotBottom - ((plotBottom - BudgetChartTopPadding) * ratio);
            gridLines.Add(new ChartLineSegment { X1 = BudgetChartLeftPadding, Y1 = y, X2 = plotRight, Y2 = y });
            yLabels.Add(new ChartLabel { X = 4, Y = y - 10, Text = FormatCompactCurrency(yAxisMaximum * (decimal)ratio) });
        }

        foreach (var month in EnumerateMonthStarts(domainStart, domainEnd))
        {
            if (month.Month != 7 && month != domainStart && month != domainEnd)
            {
                continue;
            }

            var x = MapBudgetChartX(month, domainStart, domainEnd);
            gridLines.Add(new ChartLineSegment { X1 = x, Y1 = BudgetChartTopPadding, X2 = x, Y2 = plotBottom });
            xLabels.Add(new ChartLabel
            {
                X = x - 25,
                Y = plotBottom + 8,
                Text = month.Month == 7 ? $"FY{(month.Year + 1) % 100:00}" : month.ToString("MMM yy")
            });
        }

        ReplaceCollection(BudgetChartGridLines, gridLines);
        ReplaceCollection(BudgetChartXAxisLabels, xLabels);
        ReplaceCollection(BudgetChartYAxisLabels, yLabels);
    }

    private double CalculateBudgetChartWidth(DateOnly domainStart, DateOnly domainEnd)
    {
        var plotWidth = Math.Max(
            BudgetChartMinWidth - BudgetChartLeftPadding - BudgetChartRightPadding,
            Math.Max(1, CountMonthsInclusive(domainStart, domainEnd) - 1) * 42d);
        return BudgetChartLeftPadding + plotWidth + BudgetChartRightPadding;
    }

    private double MapBudgetChartX(DateOnly date, DateOnly domainStart, DateOnly domainEnd)
    {
        var plotWidth = BudgetChartCanvasWidth - BudgetChartLeftPadding - BudgetChartRightPadding;
        var totalDays = Math.Max(1, domainEnd.DayNumber - domainStart.DayNumber);
        return BudgetChartLeftPadding + (plotWidth * (date.DayNumber - domainStart.DayNumber) / totalDays);
    }

    private static double MapBudgetChartY(decimal value, decimal axisMaximum)
    {
        var plotHeight = BudgetChartHeight - BudgetChartTopPadding - BudgetChartBottomPadding;
        var ratio = (double)(value / Math.Max(1m, axisMaximum));
        return BudgetChartHeight - BudgetChartBottomPadding - (plotHeight * ratio);
    }

    private static DateOnly? GetCalendarMonthFromFiscalPeriod(string? periodLabel)
    {
        if (!FiscalPeriod.TryParseLabel(periodLabel, out var fiscalYear, out var fiscalMonth))
        {
            return null;
        }

        return fiscalMonth <= 6
            ? new DateOnly(fiscalYear - 1, fiscalMonth + 6, 1)
            : new DateOnly(fiscalYear, fiscalMonth - 6, 1);
    }

    private static bool TryGetFiscalYearStart(string? fiscalYearLabel, out DateOnly fiscalYearStart)
    {
        fiscalYearStart = default;
        if (!FiscalPeriod.TryParseFiscalYearNumber(fiscalYearLabel, out var fiscalYear))
        {
            return false;
        }

        fiscalYear = fiscalYear < 100 ? 2000 + fiscalYear : fiscalYear;
        fiscalYearStart = new DateOnly(fiscalYear - 1, 7, 1);
        return true;
    }
}
