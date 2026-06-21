using System;

namespace ProjectCostForecast.App.Models;

public sealed class AppUserPreferences
{
    public bool StartMaximized { get; set; } = true;
    public bool ShowImportAutoCreatePreview { get; set; } = true;
    public bool DetailPanelCollapsed { get; set; }
    public bool DetailPanelPinned { get; set; }
    public double DetailPanelRailWidth { get; set; } = 44;
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
    public List<UserForecastCurvePreset> ForecastCurvePresets { get; set; } = [];
}

public sealed class UserForecastCurvePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Custom curve";
    public string Note { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public decimal ReferenceTotal { get; set; }
    public int MonthCount { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<decimal> Weights { get; set; } = [];
}
