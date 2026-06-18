namespace ProjectCostForecast.App.Models;

public sealed class AppUserPreferences
{
    public bool StartMaximized { get; set; } = true;
    public bool ShowImportAutoCreatePreview { get; set; } = true;
    public bool DetailPanelCollapsed { get; set; }
    public string SelectedProjectCode { get; set; } = "All";
    public string SelectedPeriod { get; set; } = "All";
    public bool ShowOnlyLinesWithActualCost { get; set; }
    public bool ShowCostThisMonthOnly { get; set; }
    public bool ShowOnlyLinesWithRemainingForecast { get; set; }
    public string SelectedMonthlyVarianceFilter { get; set; } = "All";
    public string SelectedBudgetVarianceFilter { get; set; } = "All";
    public bool ShowCtcMonthForecastColumns { get; set; } = true;
    public bool ShowMonthNameAboveFiscalPeriod { get; set; }
    public bool ShowCtcMonthForecastYearTotals { get; set; }
    public List<int> SelectedCtcMonthForecastYears { get; set; } = [];
    public string ForecastFreezeColumnKey { get; set; } = ViewModels.MainWindowViewModel.DefaultForecastFreezeColumnKey;
    public bool KeepColumnHighlightsAcrossTabs { get; set; }
    public bool ShowVarianceIndicators { get; set; }
    public string SelectedCategorySortOptionKey { get; set; } = "Alphabetical";
    public string SelectedLedgerChartRangeKey { get; set; } = "Last24";
}
