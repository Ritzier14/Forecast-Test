using System;

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
    public bool ShowCurrencySymbols { get; set; }
    public int ForecastMonthMillionDecimals { get; set; } = 2;
    public List<int> SelectedCtcMonthForecastYears { get; set; } = [];
    public string ForecastFreezeColumnKey { get; set; } = ViewModels.MainWindowViewModel.DefaultForecastFreezeColumnKey;
    public bool KeepColumnHighlightsAcrossTabs { get; set; }
    public bool ShowVarianceIndicators { get; set; }
    public string SelectedCategorySortOptionKey { get; set; } = "Alphabetical";
    public string SelectedLedgerChartRangeKey { get; set; } = "Last24";
    public List<string> KpiPillKeys { get; set; } = [];
    public Dictionary<string, string> KpiIconKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> KpiIconColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> WorkspaceTabIconKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> WorkspaceTabIconColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
